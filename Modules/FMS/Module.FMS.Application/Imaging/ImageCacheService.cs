using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SGS.Modules.FMS.Infrastructure.Options;

namespace SGS.Modules.FMS.Application.Imaging;

/// <summary>縮圖快取鍵（檔案 Id + 尺寸 + 模式）</summary>
public readonly record struct ImageCacheKey(Guid FileId, int Width, int Height, ImageResizeMode Mode);

public interface IImageCacheService
{
    /// <summary>
    /// Cache-Aside：命中時直接回傳；未命中時呼叫 factory 於記憶體產生縮圖並放入快取（容量上限內）。
    /// 回傳的位元組陣列為唯讀共用內容，呼叫端不得修改。
    /// </summary>
    Task<byte[]> GetOrCreateAsync(ImageCacheKey key, Func<CancellationToken, Task<byte[]>> factory, CancellationToken cancellationToken = default);

    /// <summary>移除指定檔案的所有縮圖（檔案刪除 / 清除時呼叫），回傳移除筆數</summary>
    int Remove(Guid fileId);
}

/// <summary>
/// 縮圖記憶體快取（無狀態伺服器：不寫入本機磁碟或暫存目錄）。各節點獨立的 <see cref="MemoryCache"/>，
/// 以 SizeLimit 限制總容量並依存取時間滑動到期；分段鎖避免同一縮圖被同時重複產生（Cache Stampede）。
/// 跨節點與瀏覽器端的重複請求由 HTTP ETag / Cache-Control 吸收。
/// </summary>
public sealed class ImageCacheService : IImageCacheService, IDisposable
{
    private const int LockStripes = 64;

    private readonly SemaphoreSlim[] _locks = Enumerable.Range(0, LockStripes).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly IOptionsMonitor<FileManagementOptions> _options;
    private readonly MemoryCache _cache;

    public ImageCacheService(IOptionsMonitor<FileManagementOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = options.CurrentValue.ThumbnailCache.MaxMemoryBytes,
            CompactionPercentage = 0.2
        });
    }

    private ThumbnailCacheOptions Settings => _options.CurrentValue.ThumbnailCache;

    public async Task<byte[]> GetOrCreateAsync(ImageCacheKey key, Func<CancellationToken, Task<byte[]>> factory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (!Settings.Enabled) return await factory(cancellationToken).ConfigureAwait(false);
        if (_cache.TryGetValue(key, out byte[]? hit) && hit is not null) return hit;

        var gate = _locks[(key.GetHashCode() & int.MaxValue) % LockStripes];
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 等待期間可能已由其他請求產生
            if (_cache.TryGetValue(key, out hit) && hit is not null) return hit;

            var content = await factory(cancellationToken).ConfigureAwait(false);
            _cache.Set(key, content, new MemoryCacheEntryOptions
            {
                Size = content.Length,
                SlidingExpiration = TimeSpan.FromMinutes(Settings.SlidingExpirationMinutes)
            });
            return content;
        }
        finally
        {
            gate.Release();
        }
    }

    public int Remove(Guid fileId)
    {
        // 縮圖鍵僅可能來自尺寸白名單 × 模式（防 DoS 限制），逐一移除即可，不需額外索引
        var image = _options.CurrentValue.Image;
        var sizes = image.AllowedSizes is { Length: > 0 } configured ? configured : ImageProcessingOptions.DefaultAllowedSizes;
        var removed = 0;

        foreach (var size in sizes)
        {
            var parts = size.Trim().Split('x', 'X');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var w) || !int.TryParse(parts[1], out var h)) continue;

            foreach (var mode in Enum.GetValues<ImageResizeMode>())
            {
                var key = new ImageCacheKey(fileId, w, h, mode);
                if (!_cache.TryGetValue(key, out _)) continue;
                _cache.Remove(key);
                removed++;
            }
        }

        return removed;
    }

    public void Dispose()
    {
        _cache.Dispose();
        foreach (var gate in _locks) gate.Dispose();
    }
}
