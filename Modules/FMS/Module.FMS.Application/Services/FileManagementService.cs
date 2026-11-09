using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.FMS.Application.Auditing;
using SGS.Modules.FMS.Application.Imaging;
using SGS.Modules.FMS.Application.Security;
using SGS.Modules.FMS.Application.Validation;
using SGS.Modules.FMS.Application.Watermark;
using SGS.Modules.FMS.Infrastructure.Dbcontexts;
using SGS.Modules.FMS.Infrastructure.Entities;
using SGS.Modules.FMS.Infrastructure.Options;
using SGS.Modules.FMS.Infrastructure.Storage;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Core.FileManagement;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.FMS.Application.Services;

/// <summary>
/// 檔案管理服務（IFileManagementService 實作，契約位於 SGSFramework.Core.FileManagement）。
/// <para>
/// 無狀態安全儲存管線：檔案全程於記憶體串流處理，不寫入 Web 伺服器的 wwwroot、本機目錄或 Temp；
/// 儲存必加密（每檔 DEK + AES-256-GCM 分塊，DEK 以 Data Protection 包裝）；存取一律寫入 fms.FileAccessLogs（非同步、Ledger）。
/// </para>
/// 以本模組專屬的 FMSDbContext（schema fms）保存中繼資料，載入時以 AddScoped 覆蓋 Core 的 NullFileManagementService。
/// </summary>
public sealed class FileManagementService(
    FMSDbContext dbContext,
    IStorageFactory storageFactory,
    IFileSignatureInspector inspector,
    IImageProcessor imageProcessor,
    IImageCacheService imageCache,
    IFileKeyProtector keyProtector,
    IWatermarkRenderer watermarkRenderer,
    IFileAccessAuditor auditor,
    UploadBufferLimiter bufferLimiter,
    ICurrentUserService currentUser,
    IOptionsMonitor<FileManagementOptions> options,
    TimeProvider timeProvider,
    ILogger<FileManagementService> logger) : IFileManagementService
{
    private const int MaxOriginalFileNameLength = 255;

    /// <summary>預讀檔頭大小：簽章檢查與純文字取樣 8KB，多 1 位元組用以判定是否還有後續內容</summary>
    private const int HeadSize = 8 * 1024 + 1;

    private DbSet<FileMetadata> Files => dbContext.FileMetadatas;

    private FileManagementOptions Settings => options.CurrentValue;

    private void EnsureEnabled()
    {
        if (!Settings.Enabled) throw new FileRejectedException("檔案管理功能未啟用（FileManagement:Enabled）。", 503);
    }

    // ===== 上傳 =====

    public async Task<FileMetadataDto> UploadAsync(FileUploadRequest request, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);

        ResolvedCategory? category = null;
        try
        {
            category = ResolveCategory(request.Category);
            if (scope == FileAccessScope.GenericApi && !category.AllowGenericApi)
                throw new FileRejectedException($"檔案分類「{category.Code}」僅限業務模組上傳。", 403);

            var expectedSha256 = NormalizeSha256(request.ExpectedSha256);
            var originalFileName = SanitizeFileName(request.FileName);
            var result = await UploadCoreAsync(request, category, originalFileName, expectedSha256, cancellationToken).ConfigureAwait(false);

            await auditor.RecordAsync(FileAccessActions.Upload, true, result.Id, result.Category,
                $"{result.OriginalFileName}（{result.FileSize:N0} 位元組{(result.IsEncrypted ? "，已加密" : string.Empty)}）", cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (FileRejectedException ex)
        {
            await auditor.RecordAsync(FileAccessActions.Denied, false, null, category?.Code ?? request.Category,
                $"上傳被拒（{ex.StatusCode}）：{ex.Message}", cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<FileMetadataDto> UploadCoreAsync(FileUploadRequest request, ResolvedCategory category, string originalFileName, string? expectedSha256, CancellationToken cancellationToken)
    {
        var settings = Settings;

        // 1. 預讀檔頭（記憶體）：簽章檢查只需檔頭，其餘內容後續以串流直送儲存體
        var head = new byte[HeadSize];
        var headLength = await request.Content.ReadAtLeastAsync(head, HeadSize, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        if (headLength == 0) throw new FileRejectedException("檔案內容為空。");

        // 2. 判定是否需要完整內容：OOXML（須讀 ZIP 目錄）與需重新編碼的圖片
        FileInspectionResult inspection;
        var isZip = head.AsSpan(0, headLength).StartsWith("PK\x03\x04"u8);
        if (!isZip)
        {
            inspection = await InspectAsync(new MemoryStream(head, 0, headLength, writable: false), originalFileName, category, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            inspection = default!; // 於完整緩衝後檢查
        }

        var needsFullContent = isZip || (inspection.Format!.IsImage && settings.Image.ReencodeOnUpload);

        IDisposable? bufferLease = null;
        MemoryStream? buffered = null;
        MemoryStream? reencoded = null;
        try
        {
            Stream plainSource;
            string? originalSha256 = null;
            int? width = null, height = null;

            if (needsFullContent)
            {
                // 3a. 需完整內容的格式：記憶體緩衝（有上限、限制併發），不落地暫存檔
                var limit = Math.Min(category.MaxFileSizeBytes, settings.Upload.MaxInMemoryBufferBytes);
                bufferLease = await bufferLimiter.AcquireAsync(cancellationToken).ConfigureAwait(false);
                buffered = await BufferAsync(new PrefixedStream(head, headLength, request.Content), limit, cancellationToken).ConfigureAwait(false);
                originalSha256 = Convert.ToHexStringLower(SHA256.HashData(buffered.GetBuffer().AsSpan(0, (int)buffered.Length)));

                if (isZip)
                {
                    inspection = await InspectAsync(buffered, originalFileName, category, cancellationToken).ConfigureAwait(false);
                }

                if (inspection.Format!.IsImage && settings.Image.ReencodeOnUpload)
                {
                    // 二次解碼與重新編碼：洗除 Polyglot 夾帶代碼與 EXIF / GPS
                    reencoded = new MemoryStream();
                    try
                    {
                        var image = await imageProcessor.ReencodeAsync(buffered, reencoded, cancellationToken).ConfigureAwait(false);
                        (width, height) = (image.Width, image.Height);
                    }
                    catch (ImageValidationException ex)
                    {
                        logger.LogWarning(ex, "[FileManagement] 圖片重新編碼失敗，拒絕上傳（分類 {Category}）。", category.Code);
                        throw new FileRejectedException(ex.Message, 415);
                    }

                    reencoded.Position = 0;
                    await buffered.DisposeAsync().ConfigureAwait(false); // 原始內容不再需要，及早釋放記憶體
                    buffered = null;
                    plainSource = reencoded;
                }
                else
                {
                    buffered.Position = 0;
                    plainSource = buffered;
                }
            }
            else
            {
                // 3b. 其餘格式：檔頭 + 剩餘請求串流，邊收邊雜湊、邊加密直送儲存體
                plainSource = new PrefixedStream(head, headLength, request.Content);
            }

            var format = inspection.Format!;

            // 4. 傳輸簽章：串流模式於上傳完成後比對；緩衝模式可先比對，避免無謂寫入
            if (expectedSha256 is not null && originalSha256 is not null && !string.Equals(expectedSha256, originalSha256, StringComparison.Ordinal))
                throw new FileRejectedException("傳輸完整性驗證失敗：內容雜湊與 X-Content-SHA256 不符。", 400);

            // 5. 名稱混淆：Guid + 安全副檔名，依年月分目錄
            var now = timeProvider.GetUtcNow();
            var entity = new FileMetadata
            {
                OriginalFileName = originalFileName,
                Extension = format.Extension,
                ContentType = format.ContentType,
                Category = category.Code,
                StorageTarget = category.Target,
                ContainerName = category.Container,
                ReferenceType = NullIfEmpty(request.ReferenceType),
                ReferenceId = NullIfEmpty(request.ReferenceId),
                ImageWidth = width,
                ImageHeight = height,
                CreatedBy = CurrentActor(),
                CreatedAtUtc = now
            };
            entity.StoredKey = $"{now:yyyy}/{now:MM}/{entity.Id:N}{(category.Encrypt ? ".sgse" : format.Extension)}";

            // 6. 串流計量（SHA-256 / 長度 / 上限）→ AES-256-GCM 分塊加密 → 儲存體 SDK（記憶體內，不落地）
            var provider = storageFactory.GetProvider(category.Target);
            await using var metered = new MeteredReadStream(plainSource, category.MaxFileSizeBytes);
            byte[]? dataKey = null;
            EncryptingReadStream? encrypting = null;
            try
            {
                Stream uploadStream = metered;
                if (category.Encrypt)
                {
                    (dataKey, entity.WrappedKey) = keyProtector.CreateKey();
                    encrypting = new EncryptingReadStream(metered, dataKey, settings.Upload.EncryptionChunkSize, leaveOpen: true);
                    entity.EncryptionVersion = FileCipherFormat.Version;
                    uploadStream = encrypting;
                }

                try
                {
                    await provider.UploadAsync(entity.ContainerName, entity.StoredKey, uploadStream, category.Encrypt ? "application/octet-stream" : entity.ContentType, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await DeleteStoredQuietlyAsync(provider, entity).ConfigureAwait(false);
                    throw;
                }

                entity.FileSize = metered.BytesRead;
                entity.Sha256 = metered.Sha256Hex;
                entity.StoredSize = encrypting?.BytesWritten ?? metered.BytesRead;
            }
            finally
            {
                if (encrypting is not null) await encrypting.DisposeAsync().ConfigureAwait(false);
                if (dataKey is not null) CryptographicOperations.ZeroMemory(dataKey);
            }

            var transferSha256 = originalSha256 ?? entity.Sha256;
            if (entity.FileSize == 0 || (expectedSha256 is not null && !string.Equals(expectedSha256, transferSha256, StringComparison.Ordinal)))
            {
                await DeleteStoredQuietlyAsync(provider, entity).ConfigureAwait(false);
                throw entity.FileSize == 0
                    ? new FileRejectedException("檔案內容為空。")
                    : new FileRejectedException("傳輸完整性驗證失敗：內容雜湊與 X-Content-SHA256 不符。", 400);
            }

            // 7. 中繼資料；失敗時補償刪除實體檔案，避免孤兒檔案
            try
            {
                Files.Add(entity);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await DeleteStoredQuietlyAsync(provider, entity).ConfigureAwait(false);
                throw;
            }

            logger.LogInformation("[FileManagement] 檔案已上傳 {FileId}（分類 {Category}、目標 {Target}、{Size:N0} 位元組、加密 {Encrypted}）。",
                entity.Id, entity.Category, entity.StorageTarget, entity.FileSize, category.Encrypt);
            return entity.ToDto();
        }
        finally
        {
            if (buffered is not null) await buffered.DisposeAsync().ConfigureAwait(false);
            if (reencoded is not null) await reencoded.DisposeAsync().ConfigureAwait(false);
            bufferLease?.Dispose();
        }
    }

    private async Task<FileInspectionResult> InspectAsync(Stream content, string fileName, ResolvedCategory category, CancellationToken cancellationToken)
    {
        var inspection = await inspector.InspectAsync(content, fileName, category.AllowedExtensions, cancellationToken).ConfigureAwait(false);
        if (inspection.IsValid) return inspection;

        logger.LogWarning("[FileManagement] 拒絕上傳（分類 {Category}）：{Reason}", category.Code, inspection.Error);
        throw new FileRejectedException(inspection.Error!, 415);
    }

    /// <summary>完整緩衝至記憶體（超過上限立即中止並回應 413）</summary>
    private async Task<MemoryStream> BufferAsync(Stream source, long limit, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        try
        {
            await using var metered = new MeteredReadStream(source, limit);
            await metered.CopyToAsync(buffer, Settings.StreamBufferSize, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;
            return buffer;
        }
        catch (FileRejectedException ex) when (ex.StatusCode == 413)
        {
            await buffer.DisposeAsync().ConfigureAwait(false);
            throw new FileRejectedException($"此格式需完整載入記憶體驗證，大小上限為 {limit / 1024 / 1024:N0} MB。", 413);
        }
        catch
        {
            await buffer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    // ===== 查詢 =====

    public async Task<FileMetadataDto?> GetAsync(Guid id, bool includeDeleted = false, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var entity = await Files.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        return entity is null || (entity.IsDeleted && !includeDeleted) || IsHidden(entity.Category, scope) ? null : entity.ToDto();
    }

    public async Task<PagedResult<FileMetadataDto>> SearchAsync(FileQueryRequest request, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ArgumentNullException.ThrowIfNull(request);

        var query = Files.AsNoTracking();
        var filter = request.Filter ?? new FileQueryFilter();

        if (!filter.IncludeDeleted) query = query.Where(x => !x.IsDeleted);
        if (scope == FileAccessScope.GenericApi)
        {
            var hidden = Settings.Categories.Where(c => !c.Value.AllowGenericApi).Select(c => c.Key).ToList();
            if (hidden.Count > 0) query = query.Where(x => !hidden.Contains(x.Category));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm)) query = query.Where(x => x.OriginalFileName.Contains(request.SearchTerm));
        if (!string.IsNullOrWhiteSpace(filter.Category)) query = query.Where(x => x.Category == filter.Category);
        if (filter.StorageTarget.HasValue) query = query.Where(x => x.StorageTarget == filter.StorageTarget.Value);
        if (!string.IsNullOrWhiteSpace(filter.ReferenceType)) query = query.Where(x => x.ReferenceType == filter.ReferenceType);
        if (!string.IsNullOrWhiteSpace(filter.ReferenceId)) query = query.Where(x => x.ReferenceId == filter.ReferenceId);
        if (!string.IsNullOrWhiteSpace(filter.CreatedBy)) query = query.Where(x => x.CreatedBy == filter.CreatedBy);
        if (!string.IsNullOrWhiteSpace(filter.ContentTypePrefix)) query = query.Where(x => x.ContentType.StartsWith(filter.ContentTypePrefix));
        if (filter.StartTimestamp.HasValue) query = query.Where(x => x.CreatedAtUtc >= filter.StartTimestamp.Value);
        if (filter.EndTimestamp.HasValue) query = query.Where(x => x.CreatedAtUtc <= filter.EndTimestamp.Value);

        var sortContext = new SortStrategyContext<FileMetadata>()
            .Register("CreatedAtUtc", new ExpressionSortStrategy<FileMetadata>(x => x.CreatedAtUtc))
            .Register("OriginalFileName", new ExpressionSortStrategy<FileMetadata>(x => x.OriginalFileName))
            .Register("FileSize", new ExpressionSortStrategy<FileMetadata>(x => x.FileSize))
            .Register("Category", new ExpressionSortStrategy<FileMetadata>(x => x.Category))
            .Register("Id", new ExpressionSortStrategy<FileMetadata>(x => x.Id));

        var parameters = new PagedQueryParameters<object>
        {
            Type = request.Type,
            PageIndex = request.PageIndex,
            PageSize = Math.Clamp(request.PageSize, 1, 200),
            Sorts = request.Sorts?.Count > 0
                ? request.Sorts
                : [new SortCriteria { SortBy = "CreatedAtUtc", Direction = SortDirection.Descending }],
            LastSortValue = request.LastSortValue,
            LastIdValue = request.LastIdValue
        };

        var paged = await query.ToPagedResultAsync(parameters, sortContext, x => x.Id, cancellationToken).ConfigureAwait(false);
        return new PagedResult<FileMetadataDto>(paged.Items.Select(FileMetadataMapping.ToDto).ToList(), paged.TotalCount, paged.PageIndex, paged.PageSize);
    }

    // ===== 下載 / 檢視 / 縮圖 =====

    public async Task<FileDownload?> OpenDownloadAsync(Guid id, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var entity = await FindActiveAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null || IsHidden(entity.Category, scope)) return null;

        if (scope == FileAccessScope.GenericApi && IsViewOnly(entity.Category))
        {
            await auditor.RecordAsync(FileAccessActions.Denied, false, entity.Id, entity.Category, "僅供檢視的分類不提供原檔下載", cancellationToken).ConfigureAwait(false);
            throw new FileRejectedException("此檔案僅供線上檢視（含浮水印），不提供原檔下載。", 403);
        }

        var provider = storageFactory.GetProvider(entity.StorageTarget);
        var dto = entity.ToDto();
        var maxAge = CacheMaxAgeOf(entity.Category);

        // 未加密的公開資源且儲存體支援預簽名網址：302 導向直連儲存體 / CDN（加密內容一律經 API 解密）
        if (entity.EncryptionVersion is null && entity.StorageTarget == StorageTarget.CloudPublic && provider.SupportsPresignedUrl)
        {
            var url = await provider.GetPresignedUrlAsync(entity.ContainerName, entity.StoredKey, TimeSpan.FromMinutes(30), entity.OriginalFileName, cancellationToken).ConfigureAwait(false);
            await auditor.RecordAsync(FileAccessActions.Download, true, entity.Id, entity.Category, "預簽名網址導向", cancellationToken).ConfigureAwait(false);
            return new FileDownload(dto, null, url, maxAge);
        }

        var stream = await OpenPlainAsync(provider, entity, FileAccessActions.Download, cancellationToken).ConfigureAwait(false);
        if (stream is null) return null;

        await auditor.RecordAsync(FileAccessActions.Download, true, entity.Id, entity.Category, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new FileDownload(dto, stream, null, maxAge);
    }

    public async Task<FileViewContent?> OpenWatermarkedViewAsync(Guid id, string watermarkText, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ArgumentException.ThrowIfNullOrWhiteSpace(watermarkText);

        var entity = await FindActiveAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null || IsHidden(entity.Category, scope)) return null;
        if (!watermarkRenderer.Supports(entity.ContentType))
            throw new FileRejectedException("此檔案格式不支援線上浮水印檢視，請改用下載。", 415);

        var provider = storageFactory.GetProvider(entity.StorageTarget);
        await using var stream = await OpenPlainAsync(provider, entity, FileAccessActions.View, cancellationToken).ConfigureAwait(false);
        if (stream is null) return null;

        // 浮水印需完整內容：記憶體內解密 → 渲染 → 融合浮水印，全程不落地
        byte[] plain;
        using (await bufferLimiter.AcquireAsync(cancellationToken).ConfigureAwait(false))
        {
            using var buffer = new MemoryStream(entity.FileSize > 0 && entity.FileSize < int.MaxValue ? (int)entity.FileSize : 0);
            try
            {
                await stream.CopyToAsync(buffer, Settings.StreamBufferSize, cancellationToken).ConfigureAwait(false);
            }
            catch (CryptographicException)
            {
                throw new FileRejectedException("檔案完整性驗證失敗，金鑰失效或檔案已損毀。", 403);
            }

            plain = buffer.ToArray();
        }

        try
        {
            var (content, contentType) = await watermarkRenderer.RenderAsync(plain, entity.ContentType, watermarkText, cancellationToken).ConfigureAwait(false);
            await auditor.RecordAsync(FileAccessActions.View, true, entity.Id, entity.Category, $"浮水印：{watermarkText}", cancellationToken).ConfigureAwait(false);
            return new FileViewContent(entity.ToDto(), content, contentType);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public async Task<FileImageContent?> GetImageAsync(Guid id, int? width, int? height, string? mode, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var w = width ?? 0;
        var h = height ?? 0;
        if (!IsAllowedSize(w, h))
            throw new FileRejectedException($"縮圖尺寸 {w}x{h} 不在允許清單內（{string.Join(", ", AllowedSizes())}）。");

        if (!Enum.TryParse<ImageResizeMode>(string.IsNullOrWhiteSpace(mode) ? nameof(ImageResizeMode.Max) : mode, ignoreCase: true, out var resizeMode) ||
            !Enum.IsDefined(resizeMode))
            throw new FileRejectedException("縮圖模式僅允許 max、crop、pad。");

        var entity = await FindActiveAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null || IsHidden(entity.Category, scope) || !entity.ContentType.StartsWith("image/", StringComparison.Ordinal)) return null;

        var provider = storageFactory.GetProvider(entity.StorageTarget);
        var missing = false;

        try
        {
            var content = await imageCache.GetOrCreateAsync(new ImageCacheKey(entity.Id, w, h, resizeMode), async ct =>
            {
                await using var origin = await OpenPlainAsync(provider, entity, FileAccessActions.Thumbnail, ct).ConfigureAwait(false);
                if (origin is null)
                {
                    missing = true;
                    return [];
                }

                using var output = new MemoryStream();
                await imageProcessor.ResizeAsync(origin, output, w, h, resizeMode, ct).ConfigureAwait(false);
                return output.ToArray();
            }, cancellationToken).ConfigureAwait(false);

            if (missing) return null;

            await auditor.RecordAsync(FileAccessActions.Thumbnail, true, entity.Id, entity.Category, $"{w}x{h} {resizeMode}", cancellationToken).ConfigureAwait(false);
            return new FileImageContent(entity.ToDto(), new MemoryStream(content, writable: false), $"\"{entity.Sha256[..16]}-{w}x{h}-{resizeMode}\"", CacheMaxAgeOf(entity.Category));
        }
        catch (ImageValidationException ex)
        {
            logger.LogWarning(ex, "[FileManagement] 產生縮圖失敗：{FileId}", entity.Id);
            throw new FileRejectedException(ex.Message, 422);
        }
        catch (CryptographicException)
        {
            throw new FileRejectedException("檔案完整性驗證失敗，金鑰失效或檔案已損毀。", 403);
        }
    }

    /// <summary>
    /// 開啟明文串流：加密檔案於記憶體內逐區塊解密（儲存體串流可定位時支援 Range）；解密金鑰或標頭驗證失敗記錄 IntegrityFailure 並回應 403。
    /// 實體檔案遺失回傳 null。
    /// </summary>
    internal async Task<Stream?> OpenPlainAsync(IStorageProvider provider, FileMetadata entity, string action, CancellationToken cancellationToken)
    {
        Stream stored;
        try
        {
            stored = await provider.OpenReadAsync(entity.ContainerName, entity.StoredKey, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            logger.LogError("[FileManagement] 中繼資料存在但實體檔案遺失：{FileId}（{Target} {Container}/{Key}）。", entity.Id, entity.StorageTarget, entity.ContainerName, entity.StoredKey);
            await auditor.RecordAsync(FileAccessActions.IntegrityFailure, false, entity.Id, entity.Category, $"{action}：實體檔案遺失", cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (entity.EncryptionVersion is null) return stored;

        byte[]? key = null;
        try
        {
            key = keyProtector.Unwrap(entity.WrappedKey ?? throw new CryptographicException("缺少資料金鑰。"));
            var decrypting = await DecryptingReadStream.OpenAsync(stored, key, entity.FileSize, cancellationToken: cancellationToken).ConfigureAwait(false);
            var auditorRef = auditor;
            return new IntegrityGuardStream(decrypting, ex =>
            {
                logger.LogCritical(ex, "[FileManagement] 檔案 {FileId} 解密驗證失敗，疑遭竄改（{Target} {Container}/{Key}）。", entity.Id, entity.StorageTarget, entity.ContainerName, entity.StoredKey);
                return auditorRef.RecordAsync(FileAccessActions.IntegrityFailure, false, entity.Id, entity.Category, $"{action}：區塊驗證失敗", CancellationToken.None).AsTask();
            });
        }
        catch (CryptographicException ex)
        {
            await stored.DisposeAsync().ConfigureAwait(false);
            logger.LogCritical(ex, "[FileManagement] 檔案 {FileId} 解密失敗（金鑰失效或標頭損毀）。", entity.Id);
            await auditor.RecordAsync(FileAccessActions.IntegrityFailure, false, entity.Id, entity.Category, $"{action}：{ex.Message}", cancellationToken).ConfigureAwait(false);
            throw new FileRejectedException("檔案解密失敗，金鑰失效或檔案已損毀。", 403);
        }
        finally
        {
            if (key is not null) CryptographicOperations.ZeroMemory(key);
        }
    }

    // ===== 刪除 / 清除 =====

    public async Task<bool> DeleteAsync(Guid id, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var entity = await Files.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken).ConfigureAwait(false);
        if (entity is null || IsHidden(entity.Category, scope)) return false;

        var now = timeProvider.GetUtcNow();
        var actor = CurrentActor();
        entity.IsDeleted = true;
        entity.DeletedAtUtc = now;
        entity.DeletedBy = actor;
        entity.UpdatedAtUtc = now;
        entity.UpdatedBy = actor;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 縮圖屬衍生資料，可立即移除（原檔保留至清除排程）
        imageCache.Remove(entity.Id);
        await auditor.RecordAsync(FileAccessActions.Delete, true, entity.Id, entity.Category, cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[FileManagement] 檔案已虛擬刪除 {FileId}。", entity.Id);
        return true;
    }

    public async Task<FilePurgeResult> PurgeDeletedAsync(CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var settings = Settings.Purge;
        var startedAt = timeProvider.GetUtcNow();
        var threshold = startedAt.AddDays(-settings.RetentionDays);
        var failedIds = new List<Guid>();
        int purged = 0, thumbnails = 0;

        for (var batch = 0; batch < settings.MaxBatchesPerRun && !cancellationToken.IsCancellationRequested; batch++)
        {
            var candidates = await Files
                .Where(x => x.IsDeleted && x.PurgedAtUtc == null && x.DeletedAtUtc <= threshold && !failedIds.Contains(x.Id))
                .OrderBy(x => x.DeletedAtUtc)
                .Take(settings.BatchSize)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (candidates.Count == 0) break;

            foreach (var file in candidates)
            {
                try
                {
                    // 冪等：實體檔案已不存在仍視為清除完成
                    await storageFactory.GetProvider(file.StorageTarget).DeleteAsync(file.ContainerName, file.StoredKey, cancellationToken).ConfigureAwait(false);
                    thumbnails += imageCache.Remove(file.Id);
                    file.PurgedAtUtc = timeProvider.GetUtcNow();
                    purged++;
                    await auditor.RecordAsync(FileAccessActions.Purge, true, file.Id, file.Category, $"保留 {settings.RetentionDays} 天期滿", cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                               or Amazon.S3.AmazonS3Exception or Azure.RequestFailedException)
                {
                    failedIds.Add(file.Id);
                    logger.LogWarning(ex, "[FileManagement] 清除實體檔案失敗 {FileId}（{Target} {Container}/{Key}），下次排程重試。",
                        file.Id, file.StorageTarget, file.ContainerName, file.StoredKey);
                    await auditor.RecordAsync(FileAccessActions.Purge, false, file.Id, file.Category, ex.Message, cancellationToken).ConfigureAwait(false);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (candidates.Count < settings.BatchSize) break;
        }

        var completedAt = timeProvider.GetUtcNow();
        logger.LogInformation("[FileManagement] 已刪除檔案清除完成：清除 {Purged} 筆、失敗 {Failed} 筆，耗時 {Elapsed}。",
            purged, failedIds.Count, completedAt - startedAt);

        return new FilePurgeResult(purged, failedIds.Count, settings.RetentionDays, thumbnails, startedAt, completedAt);
    }

    // ===== 分類與輔助 =====

    /// <summary>依分類解析儲存目標、容器、白名單、大小上限與加密政策（業務分流由伺服器決定，用戶端無法指定）</summary>
    internal ResolvedCategory ResolveCategory(string? code)
    {
        var settings = Settings;
        code = string.IsNullOrWhiteSpace(code) ? settings.DefaultCategory : code.Trim();

        FileCategoryOptions? category;
        if (!settings.Categories.TryGetValue(code, out category))
        {
            if (settings.Categories.Count == 0 && code.Equals(FileCategoryOptions.GeneralCategory, StringComparison.OrdinalIgnoreCase))
            {
                category = new FileCategoryOptions();
                code = FileCategoryOptions.GeneralCategory;
            }
            else
            {
                throw new FileRejectedException($"未定義的檔案分類「{code}」。");
            }
        }
        else
        {
            // 以組態中的大小寫為準，避免同分類多種寫法
            code = settings.Categories.Keys.First(k => k.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        var extensions = category.AllowedExtensions is { Length: > 0 } configured
            ? configured.Select(e => e.Trim().ToLowerInvariant()).Select(e => e.StartsWith('.') ? e : "." + e).ToArray()
            : FileManagementOptions.DefaultAllowedExtensions;

        return new ResolvedCategory(
            code,
            category.Target,
            string.IsNullOrWhiteSpace(category.Container) ? storageFactory.GetDefaultContainer(category.Target) : category.Container,
            extensions,
            Math.Min(category.MaxFileSizeBytes ?? settings.MaxFileSizeBytes, settings.MaxFileSizeBytes),
            category.CacheMaxAgeSeconds,
            category.AllowGenericApi,
            category.Encrypt);
    }

    internal sealed record ResolvedCategory(string Code, StorageTarget Target, string Container, string[] AllowedExtensions,
        long MaxFileSizeBytes, int CacheMaxAgeSeconds, bool AllowGenericApi, bool Encrypt);

    /// <summary>通用端點不可見的分類（視同不存在，避免洩漏業務檔案存在性）</summary>
    private bool IsHidden(string category, FileAccessScope scope) =>
        scope == FileAccessScope.GenericApi &&
        Settings.Categories.TryGetValue(category, out var options) && !options.AllowGenericApi;

    private bool IsViewOnly(string category) =>
        Settings.Categories.TryGetValue(category, out var options) && options.ViewOnly;

    private int CacheMaxAgeOf(string category) =>
        Settings.Categories.TryGetValue(category, out var options) ? options.CacheMaxAgeSeconds : new FileCategoryOptions().CacheMaxAgeSeconds;

    private string[] AllowedSizes() =>
        Settings.Image.AllowedSizes is { Length: > 0 } sizes ? sizes : ImageProcessingOptions.DefaultAllowedSizes;

    private bool IsAllowedSize(int width, int height) =>
        (width > 0 || height > 0) &&
        AllowedSizes().Any(s => s.Trim().Equals($"{width}x{height}", StringComparison.OrdinalIgnoreCase));

    private Task<FileMetadata?> FindActiveAsync(Guid id, CancellationToken cancellationToken) =>
        Files.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

    private async Task DeleteStoredQuietlyAsync(IStorageProvider provider, FileMetadata entity)
    {
        try
        {
            await provider.DeleteAsync(entity.ContainerName, entity.StoredKey, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FileManagement] 無法移除實體檔案 {Container}/{Key}，需人工清理。", entity.ContainerName, entity.StoredKey);
        }
    }

    private string CurrentActor() =>
        currentUser.UserName ?? currentUser.UserId ?? "system";

    /// <summary>傳輸簽章：接受十六進位（64 字元）或 Base64 的 SHA-256，統一為小寫十六進位</summary>
    internal static string? NormalizeSha256(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        value = value.Trim();
        if (value.StartsWith("sha-256=", StringComparison.OrdinalIgnoreCase)) value = value[8..].Trim(':');

        if (value.Length == 64 && value.All(Uri.IsHexDigit)) return value.ToLowerInvariant();

        Span<byte> bytes = stackalloc byte[32];
        return Convert.TryFromBase64String(value, bytes, out var written) && written == 32
            ? Convert.ToHexStringLower(bytes)
            : throw new FileRejectedException("X-Content-SHA256 格式不正確（須為 64 字元十六進位或 Base64）。");
    }

    /// <summary>原始檔名僅保留檔名部分（去除路徑與控制字元），超長時保留副檔名截斷</summary>
    internal static string SanitizeFileName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/').Split('/').Last());
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (string.IsNullOrEmpty(name)) throw new FileRejectedException("檔名不可為空。");

        if (name.Length > MaxOriginalFileNameLength)
        {
            var extension = Path.GetExtension(name);
            name = name[..(MaxOriginalFileNameLength - extension.Length)] + extension;
        }

        return name;
    }

    private static string? NullIfEmpty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        value = value.Trim();
        return value.Length <= 100 ? value : throw new FileRejectedException("關聯類型 / 關聯識別碼不可超過 100 字元。");
    }
}
