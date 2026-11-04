using ImageMagick;
using Microsoft.Extensions.Options;
using SGS.Modules.FMS.Infrastructure.Options;

namespace SGS.Modules.FMS.Application.Imaging;

/// <summary>縮圖模式（白名單）</summary>
public enum ImageResizeMode
{
    /// <summary>等比縮放至框內（不放大）</summary>
    Max = 0,

    /// <summary>等比縮放後裁切填滿</summary>
    Crop = 1,

    /// <summary>等比縮放後補邊填滿</summary>
    Pad = 2
}

/// <param name="Width">輸出寬度</param>
/// <param name="Height">輸出高度</param>
/// <param name="ContentType">輸出格式 MIME（與來源相同）</param>
public sealed record ImageProcessResult(int Width, int Height, string ContentType);

/// <summary>圖片無法處理：格式損毀、尺寸超限（解壓縮炸彈）等，屬可預期的使用者輸入錯誤</summary>
public sealed class ImageValidationException(string message, Exception? innerException = null) : Exception(message, innerException);

public interface IImageProcessor
{
    /// <summary>
    /// 二次解碼與重新編碼（同格式），移除 EXIF / IPTC / XMP 中繼資料（含 GPS），洗除 Polyglot 夾帶內容。
    /// </summary>
    Task<ImageProcessResult> ReencodeAsync(Stream input, Stream output, CancellationToken cancellationToken = default);

    /// <summary>產生縮圖（同格式）；width / height 為 0 表示依比例自動</summary>
    Task<ImageProcessResult> ResizeAsync(Stream input, Stream output, int width, int height, ImageResizeMode mode, CancellationToken cancellationToken = default);
}

/// <summary>
/// Magick.NET（ImageMagick，Apache-2.0）實作：
/// <list type="bullet">
/// <item>格式一律由檔頭 Magic Number 判定並明確指定解碼器，只接受 JPEG / PNG / GIF / WebP / BMP / TIFF，
/// 不讓 ImageMagick 依內容自動挑選其他解碼器（例如 SVG / MVG / MSL 等可執行繪圖指令的格式）。</item>
/// <item>解碼前以 Ping（僅讀檔頭）取得尺寸，超過 MaxDimension / MaxPixels 即拒絕，避免解壓縮炸彈耗盡記憶體。</item>
/// <item>動畫 GIF / WebP 保留影格（最多 MaxFrames），先依 EXIF 方向轉正再移除所有中繼資料。</item>
/// </list>
/// </summary>
public sealed class MagickImageProcessor(IOptionsMonitor<FileManagementOptions> options) : IImageProcessor
{
    private sealed record SupportedFormat(MagickFormat Format, string ContentType, bool Animated, bool HasAlpha);

    private static readonly SupportedFormat Jpeg = new(MagickFormat.Jpeg, "image/jpeg", false, false);
    private static readonly SupportedFormat Png = new(MagickFormat.Png, "image/png", false, true);
    private static readonly SupportedFormat Gif = new(MagickFormat.Gif, "image/gif", true, true);
    private static readonly SupportedFormat Webp = new(MagickFormat.WebP, "image/webp", true, true);
    private static readonly SupportedFormat Bmp = new(MagickFormat.Bmp, "image/bmp", false, false);
    private static readonly SupportedFormat Tiff = new(MagickFormat.Tiff, "image/tiff", false, true);

    public Task<ImageProcessResult> ReencodeAsync(Stream input, Stream output, CancellationToken cancellationToken = default) =>
        ProcessAsync(input, output, null, cancellationToken);

    public Task<ImageProcessResult> ResizeAsync(Stream input, Stream output, int width, int height, ImageResizeMode mode, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        if (width == 0 && height == 0) throw new ArgumentException("縮圖寬高不可同時為 0。");

        return ProcessAsync(input, output, (width, height, mode), cancellationToken);
    }

    private async Task<ImageProcessResult> ProcessAsync(Stream input, Stream output, (int Width, int Height, ImageResizeMode Mode)? resize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        var settings = options.CurrentValue.Image;

        // 需可重複讀取（先 Ping 檔頭、再完整解碼）；不可 Seek 的來源先複製到記憶體
        await using var buffered = await EnsureSeekableAsync(input, cancellationToken).ConfigureAwait(false);
        var source = buffered ?? input;
        var start = source.Position;

        var format = await DetectFormatAsync(source, cancellationToken).ConfigureAwait(false)
                     ?? throw new ImageValidationException("圖片格式不受支援（僅接受 JPEG、PNG、GIF、WebP、BMP、TIFF）。");
        source.Position = start;

        try
        {
            var info = new MagickImageInfo(source, new MagickReadSettings { Format = format.Format });
            EnsureWithinLimits((long)info.Width, (long)info.Height, settings);
            source.Position = start;

            var readSettings = new MagickReadSettings
            {
                Format = format.Format,
                FrameIndex = 0,
                FrameCount = format.Animated ? (uint)settings.MaxFrames : 1
            };

            using var images = new MagickImageCollection();
            await images.ReadAsync(source, readSettings, cancellationToken).ConfigureAwait(false);
            if (images.Count == 0) throw new ImageValidationException("圖片內容為空。");

            foreach (var frame in images)
            {
                EnsureWithinLimits((long)frame.Width, (long)frame.Height, settings);
            }

            // 動畫：先展開為完整影格，才能逐格轉正 / 縮放
            if (images.Count > 1) images.Coalesce();

            foreach (var frame in images)
            {
                // 先依 EXIF 方向轉正，再移除中繼資料，避免圖片旋轉錯誤
                frame.AutoOrient();
                if (resize is { } r && NeedsResize((int)frame.Width, (int)frame.Height, r.Width, r.Height, r.Mode))
                {
                    Resize(frame, r.Width, r.Height, r.Mode, format);
                }

                // 移除 EXIF / IPTC / XMP / ICC 等所有 Profile 與註解
                frame.Strip();
                if (format.Format == MagickFormat.Jpeg) frame.Quality = (uint)settings.JpegQuality;
            }

            var first = images[0];
            if (images.Count > 1)
            {
                await images.WriteAsync(output, format.Format, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await first.WriteAsync(output, format.Format, cancellationToken).ConfigureAwait(false);
            }

            return new ImageProcessResult((int)first.Width, (int)first.Height, format.ContentType);
        }
        catch (MagickException ex)
        {
            throw new ImageValidationException("圖片內容損毀或格式不受支援。", ex);
        }
    }

    private static void Resize(IMagickImage<byte> image, int width, int height, ImageResizeMode mode, SupportedFormat format)
    {
        var w = (uint)width;
        var h = (uint)height;

        if (mode == ImageResizeMode.Max || width == 0 || height == 0)
        {
            // 寬或高為 0 時依比例計算；只縮小不放大
            image.Resize(new MagickGeometry(w, h) { Greater = true });
            return;
        }

        if (mode == ImageResizeMode.Crop)
        {
            // 等比縮放至完全覆蓋框，再置中裁切
            image.Resize(new MagickGeometry(w, h) { FillArea = true });
            image.Crop(new MagickGeometry(w, h), Gravity.Center);
            image.ResetPage();
            return;
        }

        // Pad：等比縮放至框內，再置中補邊（透明格式補透明，JPEG / BMP 補白）
        image.Resize(new MagickGeometry(w, h));
        image.BackgroundColor = format.HasAlpha ? MagickColors.Transparent : MagickColors.White;
        image.Extent(w, h, Gravity.Center);
    }

    /// <summary>
    /// Max 模式不放大；寬或高為 0 時依比例計算。Crop / Pad 須兩邊皆指定才有意義，否則同 Max。
    /// </summary>
    private static bool NeedsResize(int sourceWidth, int sourceHeight, int width, int height, ImageResizeMode mode)
    {
        if (mode != ImageResizeMode.Max && width > 0 && height > 0) return true;
        return (width > 0 && sourceWidth > width) || (height > 0 && sourceHeight > height);
    }

    private static void EnsureWithinLimits(long width, long height, ImageProcessingOptions settings)
    {
        if (width <= 0 || height <= 0)
            throw new ImageValidationException("圖片尺寸不合法。");

        if (width > settings.MaxDimension || height > settings.MaxDimension || width * height > settings.MaxPixels)
            throw new ImageValidationException($"圖片尺寸 {width}×{height} 超過允許上限（單邊 {settings.MaxDimension} 像素、總計 {settings.MaxPixels:N0} 像素）。");
    }

    /// <summary>依檔頭判定格式（與 FileSignatureInspector 一致的白名單），不採信副檔名與 ImageMagick 的自動偵測</summary>
    private static async Task<SupportedFormat?> DetectFormatAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        ReadOnlySpan<byte> h = header.AsSpan(0, read);

        if (h.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])) return Jpeg;
        if (h.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return Png;
        if (h.StartsWith("GIF87a"u8) || h.StartsWith("GIF89a"u8)) return Gif;
        if (h.Length >= 12 && h.StartsWith("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8)) return Webp;
        if (h.StartsWith("BM"u8)) return Bmp;
        if (h.StartsWith((ReadOnlySpan<byte>)[0x49, 0x49, 0x2A, 0x00]) || h.StartsWith((ReadOnlySpan<byte>)[0x4D, 0x4D, 0x00, 0x2A])) return Tiff;
        return null;
    }

    private static async Task<MemoryStream?> EnsureSeekableAsync(Stream input, CancellationToken cancellationToken)
    {
        if (input.CanSeek) return null;

        var copy = new MemoryStream();
        await input.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        copy.Position = 0;
        return copy;
    }
}
