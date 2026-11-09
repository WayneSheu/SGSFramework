using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.FMS.Application.Auditing;
using SGS.Modules.FMS.Application.Security;
using SGS.Modules.FMS.Infrastructure.Dbcontexts;
using SGS.Modules.FMS.Infrastructure.Entities;
using SGS.Modules.FMS.Infrastructure.Options;
using SGS.Modules.FMS.Infrastructure.Storage;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.FMS.Application.Services;

/// <param name="Encrypted">本次完成加密的檔案數</param>
/// <param name="Failed">失敗數（保留原明文，下次重試）</param>
/// <param name="Remaining">尚待加密的檔案數</param>
public sealed record EncryptLegacyResult(int Encrypted, int Failed, int Remaining, DateTimeOffset StartedAt, DateTimeOffset CompletedAt);

public interface IFileMaintenanceService
{
    /// <summary>
    /// 將舊有明文檔案（EncryptionVersion = null 且分類要求加密）逐檔轉為 SGSE 加密：
    /// 串流讀取 → 驗證原雜湊 → 加密寫入新鍵 → 更新中繼資料 → 刪除舊明文物件。每次最多處理 <paramref name="maxFiles"/> 筆，可重複執行。
    /// </summary>
    Task<EncryptLegacyResult> EncryptLegacyAsync(int maxFiles, CancellationToken cancellationToken = default);
}

public sealed class FileMaintenanceService(
    FMSDbContext dbContext,
    IStorageFactory storageFactory,
    IFileKeyProtector keyProtector,
    IFileAccessAuditor auditor,
    IOptionsMonitor<FileManagementOptions> options,
    TimeProvider timeProvider,
    ILogger<FileMaintenanceService> logger) : IFileMaintenanceService
{
    public async Task<EncryptLegacyResult> EncryptLegacyAsync(int maxFiles, CancellationToken cancellationToken = default)
    {
        var settings = options.CurrentValue;
        if (!settings.Enabled) throw new FileRejectedException("檔案管理功能未啟用（FileManagement:Enabled）。", 503);

        var startedAt = timeProvider.GetUtcNow();
        var plaintextCategories = settings.Categories.Where(c => !c.Value.Encrypt).Select(c => c.Key).ToList();
        var pending = dbContext.FileMetadatas.Where(x => !x.IsDeleted && x.EncryptionVersion == null && !plaintextCategories.Contains(x.Category));

        var candidates = await pending.OrderBy(x => x.CreatedAtUtc).Take(Math.Clamp(maxFiles, 1, 1000))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        int encrypted = 0, failed = 0;
        foreach (var file in candidates)
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (await EncryptOneAsync(file, settings, cancellationToken).ConfigureAwait(false)) encrypted++;
            else failed++;
        }

        var remaining = await pending.CountAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[FileManagement] 舊檔加密：完成 {Encrypted} 筆、失敗 {Failed} 筆、尚餘 {Remaining} 筆。", encrypted, failed, remaining);
        return new EncryptLegacyResult(encrypted, failed, remaining, startedAt, timeProvider.GetUtcNow());
    }

    private async Task<bool> EncryptOneAsync(FileMetadata file, FileManagementOptions settings, CancellationToken cancellationToken)
    {
        var provider = storageFactory.GetProvider(file.StorageTarget);
        var oldKey = file.StoredKey;
        var newKey = $"{file.CreatedAtUtc:yyyy}/{file.CreatedAtUtc:MM}/{file.Id:N}.sgse";
        var (dataKey, wrappedKey) = keyProtector.CreateKey();
        var uploaded = false;

        try
        {
            long plainLength, cipherLength;
            string sha256;
            await using (var source = await provider.OpenReadAsync(file.ContainerName, oldKey, cancellationToken).ConfigureAwait(false))
            await using (var metered = new MeteredReadStream(source, long.MaxValue))
            await using (var encrypting = new EncryptingReadStream(metered, dataKey, settings.Upload.EncryptionChunkSize, leaveOpen: true))
            {
                await provider.UploadAsync(file.ContainerName, newKey, encrypting, "application/octet-stream", cancellationToken).ConfigureAwait(false);
                uploaded = true;
                (plainLength, cipherLength, sha256) = (metered.BytesRead, encrypting.BytesWritten, metered.Sha256Hex);
            }

            // 原檔完整性：內容須與上傳時記錄的雜湊一致，否則保留原檔並告警（不以已遭竄改的內容產生密文）
            if (!string.Equals(sha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new CryptographicException($"原檔雜湊不符（記錄 {file.Sha256}，實際 {sha256}），疑遭竄改。");

            file.StoredKey = newKey;
            file.WrappedKey = wrappedKey;
            file.EncryptionVersion = FileCipherFormat.Version;
            file.StoredSize = cipherLength;
            file.FileSize = plainLength;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // 中繼資料已指向密文，最後才移除舊明文物件
            try
            {
                await provider.DeleteAsync(file.ContainerName, oldKey, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[FileManagement] 已加密但無法移除舊明文物件 {Container}/{Key}，需人工清理。", file.ContainerName, oldKey);
            }

            await auditor.RecordAsync(FileAccessActions.Encrypt, true, file.Id, file.Category, "舊明文檔案已轉為 SGSE 加密", cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[FileManagement] 舊檔加密失敗 {FileId}（{Container}/{Key}），保留原明文。", file.Id, file.ContainerName, oldKey);
            // 還原追蹤中的實體為資料庫現況，避免後續 SaveChanges 帶出半套變更
            await dbContext.Entry(file).ReloadAsync(CancellationToken.None).ConfigureAwait(false);
            if (uploaded && file.StoredKey != newKey)
            {
                try { await provider.DeleteAsync(file.ContainerName, newKey, CancellationToken.None).ConfigureAwait(false); }
                catch (Exception cleanup) { logger.LogWarning(cleanup, "[FileManagement] 無法移除未完成的密文物件 {Key}", newKey); }
            }

            await auditor.RecordAsync(ex is CryptographicException ? FileAccessActions.IntegrityFailure : FileAccessActions.Encrypt, false,
                file.Id, file.Category, $"舊檔加密失敗：{ex.Message}", cancellationToken).ConfigureAwait(false);
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }
}
