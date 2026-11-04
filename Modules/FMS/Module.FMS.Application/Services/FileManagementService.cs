using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.FMS.Infrastructure.Dbcontexts;
using SGS.Modules.FMS.Infrastructure.Entities;
using SGSFramework.Core.FileManagement;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Core.Paginations;
using SGS.Modules.FMS.Infrastructure.Storage;
using SGS.Modules.FMS.Application.Imaging;
using SGS.Modules.FMS.Infrastructure.Options;
using SGS.Modules.FMS.Application.Validation;

namespace SGS.Modules.FMS.Application.Services;

/// <summary>
/// 檔案管理服務（IFileManagementService 實作，契約位於 SGSFramework.Core.FileManagement）。
/// 以本模組專屬的 FMSDbContext（schema fms）保存中繼資料，載入時以 AddScoped 覆蓋 Core 的 NullFileManagementService。
/// </summary>
public sealed class FileManagementService(
    FMSDbContext dbContext,
    IStorageFactory storageFactory,
    IFileSignatureInspector inspector,
    IImageProcessor imageProcessor,
    IImageCacheService imageCache,
    ICurrentUserService currentUser,
    IOptionsMonitor<FileManagementOptions> options,
    TimeProvider timeProvider,
    ILogger<FileManagementService> logger) : IFileManagementService
{
    private const int MaxOriginalFileNameLength = 255;

    private DbSet<FileMetadata> Files => dbContext.FileMetadatas;

    private FileManagementOptions Settings => options.CurrentValue;

    private void EnsureEnabled()
    {
        if (!Settings.Enabled) throw new FileRejectedException("檔案管理功能未啟用（FileManagement:Enabled）。", 503);
    }

    public async Task<FileMetadataDto> UploadAsync(FileUploadRequest request, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);

        var category = ResolveCategory(request.Category);
        if (scope == FileAccessScope.GenericApi && !category.AllowGenericApi)
            throw new FileRejectedException($"檔案分類「{category.Code}」僅限業務模組上傳。", 403);

        var originalFileName = SanitizeFileName(request.FileName);

        // 1. 確保可定位（檔案簽章檢查、圖片 Identify、雜湊需重讀）
        await using var buffered = request.Content.CanSeek ? null : await BufferToTempAsync(request.Content, category.MaxFileSizeBytes, cancellationToken).ConfigureAwait(false);
        var source = buffered ?? request.Content;
        source.Position = 0;

        if (source.Length == 0) throw new FileRejectedException("檔案內容為空。");
        if (source.Length > category.MaxFileSizeBytes)
            throw new FileRejectedException($"檔案大小超過上限 {category.MaxFileSizeBytes / 1024 / 1024:N0} MB。", 413);

        // 2. Magic Number 雙重驗證（真實格式 × 副檔名 × 白名單）
        var inspection = await inspector.InspectAsync(source, originalFileName, category.AllowedExtensions, cancellationToken).ConfigureAwait(false);
        if (!inspection.IsValid)
        {
            logger.LogWarning("[FileManagement] 拒絕上傳（分類 {Category}）：{Reason}", category.Code, inspection.Error);
            throw new FileRejectedException(inspection.Error!, 415);
        }

        var format = inspection.Format!;
        int? width = null, height = null;

        // 3. 圖片二次解碼與重新編碼，洗除 Polyglot 夾帶代碼與 EXIF / GPS
        await using var reencoded = format.IsImage && Settings.Image.ReencodeOnUpload ? CreateTempFile() : null;
        if (reencoded is not null)
        {
            try
            {
                var result = await imageProcessor.ReencodeAsync(source, reencoded, cancellationToken).ConfigureAwait(false);
                (width, height) = (result.Width, result.Height);
            }
            catch (ImageValidationException ex)
            {
                logger.LogWarning(ex, "[FileManagement] 圖片重新編碼失敗，拒絕上傳（分類 {Category}）。", category.Code);
                throw new FileRejectedException(ex.Message, 415);
            }

            reencoded.Position = 0;
        }

        var payload = reencoded ?? source;

        // 4. 實際儲存內容的雜湊（ETag / 完整性查驗）
        var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(payload, cancellationToken).ConfigureAwait(false));
        payload.Position = 0;

        // 5. 名稱混淆：Guid + 安全副檔名，依年月分目錄
        var now = timeProvider.GetUtcNow();
        var entity = new FileMetadata
        {
            OriginalFileName = originalFileName,
            Extension = format.Extension,
            ContentType = format.ContentType,
            FileSize = payload.Length,
            Sha256 = sha256,
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
        entity.StoredKey = $"{now:yyyy}/{now:MM}/{entity.Id:N}{format.Extension}";

        var provider = storageFactory.GetProvider(category.Target);
        await provider.UploadAsync(entity.ContainerName, entity.StoredKey, payload, entity.ContentType, cancellationToken).ConfigureAwait(false);

        try
        {
            Files.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // 補償：中繼資料未寫入則移除實體檔案，避免孤兒檔案
            try
            {
                await provider.DeleteAsync(entity.ContainerName, entity.StoredKey, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception cleanupEx)
            {
                logger.LogError(cleanupEx, "[FileManagement] 中繼資料寫入失敗且無法移除實體檔案 {Container}/{Key}，需人工清理。", entity.ContainerName, entity.StoredKey);
            }

            throw;
        }

        logger.LogInformation("[FileManagement] 檔案已上傳 {FileId}（分類 {Category}、目標 {Target}、{Size:N0} 位元組）。",
            entity.Id, entity.Category, entity.StorageTarget, entity.FileSize);
        return entity.ToDto();
    }

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

    public async Task<FileDownload?> OpenDownloadAsync(Guid id, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var entity = await FindActiveAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null || IsHidden(entity.Category, scope)) return null;

        var provider = storageFactory.GetProvider(entity.StorageTarget);
        var dto = entity.ToDto();
        var maxAge = CacheMaxAgeOf(entity.Category);

        // 公開資源且儲存體支援預簽名網址：302 導向直連儲存體 / CDN
        if (entity.StorageTarget == StorageTarget.CloudPublic && provider.SupportsPresignedUrl)
        {
            var url = await provider.GetPresignedUrlAsync(entity.ContainerName, entity.StoredKey, TimeSpan.FromMinutes(30), entity.OriginalFileName, cancellationToken).ConfigureAwait(false);
            return new FileDownload(dto, null, url, maxAge);
        }

        var stream = await OpenStorageAsync(provider, entity, cancellationToken).ConfigureAwait(false);
        return stream is null ? null : new FileDownload(dto, stream, null, maxAge);
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
        var key = new ImageCacheKey(entity.Id, w, h, resizeMode, entity.Extension);

        try
        {
            var stream = await imageCache.GetOrCreateAsync(key, async (output, ct) =>
            {
                await using var origin = await provider.OpenReadAsync(entity.ContainerName, entity.StoredKey, ct).ConfigureAwait(false);
                await imageProcessor.ResizeAsync(origin, output, w, h, resizeMode, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            return new FileImageContent(entity.ToDto(), stream, $"\"{entity.Sha256[..16]}-{w}x{h}-{resizeMode}\"", CacheMaxAgeOf(entity.Category));
        }
        catch (FileNotFoundException)
        {
            logger.LogError("[FileManagement] 中繼資料存在但實體檔案遺失：{FileId}（{Target} {Container}/{Key}）。", entity.Id, entity.StorageTarget, entity.ContainerName, entity.StoredKey);
            return null;
        }
        catch (ImageValidationException ex)
        {
            logger.LogWarning(ex, "[FileManagement] 產生縮圖失敗：{FileId}", entity.Id);
            throw new FileRejectedException(ex.Message, 422);
        }
    }

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
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    failedIds.Add(file.Id);
                    logger.LogWarning(ex, "[FileManagement] 清除實體檔案失敗 {FileId}（{Target} {Container}/{Key}），下次排程重試。",
                        file.Id, file.StorageTarget, file.ContainerName, file.StoredKey);
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

    /// <summary>依分類解析儲存目標、容器、白名單與大小上限（業務分流由伺服器決定，用戶端無法指定儲存目標）</summary>
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
            category.AllowGenericApi);
    }

    internal sealed record ResolvedCategory(string Code, StorageTarget Target, string Container, string[] AllowedExtensions, long MaxFileSizeBytes, int CacheMaxAgeSeconds, bool AllowGenericApi);

    /// <summary>通用端點不可見的分類（視同不存在，避免洩漏業務檔案存在性）</summary>
    private bool IsHidden(string category, FileAccessScope scope) =>
        scope == FileAccessScope.GenericApi &&
        Settings.Categories.TryGetValue(category, out var options) && !options.AllowGenericApi;

    private int CacheMaxAgeOf(string category) =>
        Settings.Categories.TryGetValue(category, out var options) ? options.CacheMaxAgeSeconds : new FileCategoryOptions().CacheMaxAgeSeconds;

    private string[] AllowedSizes() =>
        Settings.Image.AllowedSizes is { Length: > 0 } sizes ? sizes : ImageProcessingOptions.DefaultAllowedSizes;

    private bool IsAllowedSize(int width, int height) =>
        (width > 0 || height > 0) &&
        AllowedSizes().Any(s => s.Trim().Equals($"{width}x{height}", StringComparison.OrdinalIgnoreCase));

    private Task<FileMetadata?> FindActiveAsync(Guid id, CancellationToken cancellationToken) =>
        Files.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

    private async Task<Stream?> OpenStorageAsync(IStorageProvider provider, FileMetadata entity, CancellationToken cancellationToken)
    {
        try
        {
            return await provider.OpenReadAsync(entity.ContainerName, entity.StoredKey, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            logger.LogError("[FileManagement] 中繼資料存在但實體檔案遺失：{FileId}（{Target} {Container}/{Key}）。", entity.Id, entity.StorageTarget, entity.ContainerName, entity.StoredKey);
            return null;
        }
    }

    private string CurrentActor() =>
        currentUser.UserName ?? currentUser.UserId ?? "system";

    private FileStream CreateTempFile()
    {
        var directory = string.IsNullOrWhiteSpace(Settings.TempPath) ? Path.GetTempPath() : Settings.TempPath;
        Directory.CreateDirectory(directory);
        return new FileStream(Path.Combine(directory, $"sgs-upload-{Guid.NewGuid():N}.tmp"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            Settings.StreamBufferSize, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
    }

    /// <summary>不可定位的串流先緩衝至暫存檔（邊讀邊計量，超過上限立即中止，不整檔讀入記憶體）</summary>
    private async Task<FileStream> BufferToTempAsync(Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        var temp = CreateTempFile();
        try
        {
            var buffer = new byte[Settings.StreamBufferSize];
            long total = 0;
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > maxBytes) throw new FileRejectedException($"檔案大小超過上限 {maxBytes / 1024 / 1024:N0} MB。", 413);
                await temp.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            temp.Position = 0;
            return temp;
        }
        catch
        {
            await temp.DisposeAsync().ConfigureAwait(false);
            throw;
        }
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
