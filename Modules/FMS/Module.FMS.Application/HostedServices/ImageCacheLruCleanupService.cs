using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.FMS.Application.Imaging;
using SGS.Modules.FMS.Infrastructure.Options;

namespace SGS.Modules.FMS.Application.HostedServices;

/// <summary>
/// 縮圖快取 LRU 清理（常駐背景服務）：定期統計快取目錄總量，超過高水位時依「最後存取時間」由舊到新刪除至低水位以下。
/// 快取屬各節點本機資料，故以 BackgroundService 於每個節點執行（不走 Hangfire 單點排程）。
/// 同時清除逾 1 小時的殘留暫存檔（產生縮圖途中程序中止）。
/// </summary>
public sealed class ImageCacheLruCleanupService(
    IImageCacheService cache,
    IOptionsMonitor<FileManagementOptions> options,
    TimeProvider timeProvider,
    ILogger<ImageCacheLruCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan StaleTempAge = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (cache.RootPath is null)
        {
            logger.LogInformation("[ImageCache] 縮圖快取未啟用，略過 LRU 清理服務。");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (Directory.Exists(cache.RootPath))
                {
                    RunCleanup(cache.RootPath, options.CurrentValue.ImageCache, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "[ImageCache] 縮圖快取 LRU 清理失敗。");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(options.CurrentValue.ImageCache.CheckIntervalMinutes), timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <returns>刪除的檔案數</returns>
    public int RunCleanup(string rootPath, ImageCacheOptions settings, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        // 列舉當下快照大小與存取時間：FileInfo 刪除後再讀 Length 會拋出 FileNotFoundException
        var files = new List<(FileInfo File, long Length, DateTime LastAccessUtc)>();
        var deleted = 0;

        foreach (var file in new DirectoryInfo(rootPath).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            if (file.Name.EndsWith(ImageCacheService.TempSuffix, StringComparison.Ordinal))
            {
                if (now - file.LastWriteTimeUtc > StaleTempAge && TryDelete(file)) deleted++;
                continue;
            }

            files.Add((file, file.Length, file.LastAccessTimeUtc));
        }

        var currentSize = files.Sum(f => f.Length);
        if (currentSize <= settings.HighWatermarkBytes) return deleted;

        var startSize = currentSize;
        foreach (var (file, length, _) in files.OrderBy(f => f.LastAccessUtc))
        {
            if (cancellationToken.IsCancellationRequested || currentSize <= settings.LowWatermarkBytes) break;

            if (TryDelete(file))
            {
                currentSize -= length;
                deleted++;
            }
        }

        logger.LogInformation("[ImageCache] LRU 清理完成：刪除 {Count} 個檔案，快取總量 {Before:N0} → {After:N0} 位元組。",
            deleted, startSize, currentSize);
        return deleted;
    }

    private bool TryDelete(FileInfo file)
    {
        try
        {
            file.Delete();
            return true;
        }
        catch (IOException)
        {
            return false; // 正被讀取中，下次循環再處理
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "[ImageCache] 無權限刪除快取檔：{Path}", file.FullName);
            return false;
        }
    }
}
