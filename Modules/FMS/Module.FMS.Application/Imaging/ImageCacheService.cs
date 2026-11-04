using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.FMS.Infrastructure.Options;

namespace SGS.Modules.FMS.Application.Imaging;

/// <summary>縮圖快取鍵（檔案 Id + 尺寸 + 模式）</summary>
public readonly record struct ImageCacheKey(Guid FileId, int Width, int Height, ImageResizeMode Mode, string Extension)
{
    /// <summary>以檔案 Id 前兩碼分散子目錄，避免單一目錄檔案過多</summary>
    public string RelativePath =>
        Path.Combine(FileId.ToString("N")[..2], $"{FileId:N}_{Width}x{Height}_{Mode.ToString().ToLowerInvariant()}{Extension}");
}

public interface IImageCacheService
{
    /// <summary>快取根目錄（未啟用時為 null）</summary>
    string? RootPath { get; }

    /// <summary>
    /// Cache-Aside：命中時更新最後存取時間（修正 LRU 戳記）並開啟串流；未命中時呼叫 factory 產生縮圖寫入快取。
    /// 快取停用時產生至「關閉即刪除」的暫存檔。回傳串流由呼叫端 Dispose。
    /// </summary>
    Task<Stream> GetOrCreateAsync(ImageCacheKey key, Func<Stream, CancellationToken, Task> factory, CancellationToken cancellationToken = default);

    /// <summary>移除指定檔案的所有縮圖（檔案刪除 / 清除時呼叫）</summary>
    int Remove(Guid fileId);
}

/// <summary>
/// 縮圖快取（伺服器本機高速磁碟）。寫入先落地暫存檔再原子更名，並以分段鎖避免同一縮圖被同時重複產生（Cache Stampede）。
/// 總量由 <see cref="HostedServices.ImageCacheLruCleanupService"/> 依高 / 低水位以 LRU 清理。
/// </summary>
public sealed class ImageCacheService : IImageCacheService
{
    internal const string TempSuffix = ".tmp";
    private const int LockStripes = 64;

    private readonly SemaphoreSlim[] _locks = Enumerable.Range(0, LockStripes).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly IOptionsMonitor<FileManagementOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ImageCacheService> _logger;

    public ImageCacheService(IOptionsMonitor<FileManagementOptions> options, TimeProvider timeProvider, ILogger<ImageCacheService> logger)
    {
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;

        var cache = options.CurrentValue.ImageCache;
        RootPath = options.CurrentValue.Enabled && cache.Enabled && !string.IsNullOrWhiteSpace(cache.RootPath) ? Path.GetFullPath(cache.RootPath) : null;
    }

    public string? RootPath { get; }

    private int BufferSize => _options.CurrentValue.StreamBufferSize;

    public async Task<Stream> GetOrCreateAsync(ImageCacheKey key, Func<Stream, CancellationToken, Task> factory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (RootPath is null)
        {
            return await CreateUncachedAsync(factory, cancellationToken).ConfigureAwait(false);
        }

        var path = Path.Combine(RootPath, key.RelativePath);
        if (TryOpenHit(path) is { } hit) return hit;

        var gate = _locks[(key.GetHashCode() & int.MaxValue) % LockStripes];
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 等待期間可能已由其他請求產生
            if (TryOpenHit(path) is { } produced) return produced;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tempPath = $"{path}.{Guid.NewGuid():N}{TempSuffix}";
            try
            {
                await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
                {
                    await factory(output, cancellationToken).ConfigureAwait(false);
                }

                File.Move(tempPath, path, overwrite: true);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }
        finally
        {
            gate.Release();
        }

        return OpenRead(path);
    }

    public int Remove(Guid fileId)
    {
        if (RootPath is null) return 0;

        var directory = Path.Combine(RootPath, fileId.ToString("N")[..2]);
        if (!Directory.Exists(directory)) return 0;

        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(directory, $"{fileId:N}_*"))
        {
            if (TryDelete(file)) removed++;
        }

        return removed;
    }

    private Stream? TryOpenHit(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            // 多數作業系統預設不更新 LastAccessTime（NTFS DisableLastAccess / noatime），命中時主動更新以確保 LRU 準確
            File.SetLastAccessTimeUtc(path, _timeProvider.GetUtcNow().UtcDateTime);
            return OpenRead(path);
        }
        catch (FileNotFoundException)
        {
            return null; // 剛好被 LRU 清除
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "[ImageCache] 更新快取存取時間失敗：{Path}", path);
            return File.Exists(path) ? OpenRead(path) : null;
        }
    }

    private FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, BufferSize, useAsync: true);

    private async Task<Stream> CreateUncachedAsync(Func<Stream, CancellationToken, Task> factory, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"sgs-thumb-{Guid.NewGuid():N}{TempSuffix}");
        var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, BufferSize,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
            await factory(stream, cancellationToken).ConfigureAwait(false);
            stream.Position = 0;
            return stream;
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "[ImageCache] 刪除快取檔失敗（使用中，稍後由 LRU 清理）：{Path}", path);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "[ImageCache] 無權限刪除快取檔：{Path}", path);
            return false;
        }
    }
}
