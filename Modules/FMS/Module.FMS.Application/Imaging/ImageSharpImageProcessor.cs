using Microsoft.Extensions.Options;
using SGS.Modules.FMS.Infrastructure.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace SGS.Modules.FMS.Application.Imaging;

/// <summary>縮圖模式（白名單，對應 ImageSharp ResizeMode）</summary>
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
/// ImageSharp 實作。解碼前先以 Identify 讀取檔頭尺寸，超過 MaxDimension / MaxPixels 即拒絕，避免解壓縮炸彈耗盡記憶體。
/// </summary>
public sealed class ImageSharpImageProcessor(IOptionsMonitor<FileManagementOptions> options) : IImageProcessor
{
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
        var decoderOptions = new DecoderOptions { MaxFrames = (uint)settings.MaxFrames };

        try
        {
            if (input.CanSeek)
            {
                var info = await Image.IdentifyAsync(decoderOptions, input, cancellationToken).ConfigureAwait(false);
                EnsureWithinLimits(info.Width, info.Height, settings);
                input.Position = 0;
            }

            using var image = await Image.LoadAsync(decoderOptions, input, cancellationToken).ConfigureAwait(false);
            EnsureWithinLimits(image.Width, image.Height, settings);

            var format = image.Metadata.DecodedImageFormat
                         ?? throw new ImageValidationException("無法判定圖片格式。");

            image.Mutate(x =>
            {
                // 先依 EXIF 方向轉正，再移除中繼資料，避免圖片旋轉錯誤
                x.AutoOrient();
                if (resize is { } r && NeedsResize(image.Width, image.Height, r.Width, r.Height, r.Mode))
                {
                    x.Resize(new ResizeOptions
                    {
                        Size = new Size(r.Width, r.Height),
                        Mode = r.Mode switch
                        {
                            ImageResizeMode.Crop => ResizeMode.Crop,
                            ImageResizeMode.Pad => ResizeMode.Pad,
                            _ => ResizeMode.Max
                        },
                        Sampler = KnownResamplers.Bicubic
                    });
                }
            });

            StripMetadata(image);
            await image.SaveAsync(output, CreateEncoder(format, settings), cancellationToken).ConfigureAwait(false);

            return new ImageProcessResult(image.Width, image.Height, format.DefaultMimeType);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or SixLabors.ImageSharp.ImageProcessingException or NotSupportedException)
        {
            throw new ImageValidationException("圖片內容損毀或格式不受支援。", ex);
        }
    }

    /// <summary>
    /// Max 模式不放大；寬或高為 0 時依比例計算（ResizeOptions 以 0 表示自動）。Crop / Pad 須兩邊皆指定才有意義，否則同 Max。
    /// </summary>
    private static bool NeedsResize(int sourceWidth, int sourceHeight, int width, int height, ImageResizeMode mode)
    {
        if (mode != ImageResizeMode.Max && width > 0 && height > 0) return true;
        return (width > 0 && sourceWidth > width) || (height > 0 && sourceHeight > height);
    }

    private static void EnsureWithinLimits(int width, int height, ImageProcessingOptions settings)
    {
        if (width <= 0 || height <= 0)
            throw new ImageValidationException("圖片尺寸不合法。");

        if (width > settings.MaxDimension || height > settings.MaxDimension || (long)width * height > settings.MaxPixels)
            throw new ImageValidationException($"圖片尺寸 {width}×{height} 超過允許上限（單邊 {settings.MaxDimension} 像素、總計 {settings.MaxPixels:N0} 像素）。");
    }

    private static void StripMetadata(Image image)
    {
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        foreach (var frame in image.Frames)
        {
            frame.Metadata.ExifProfile = null;
            frame.Metadata.IptcProfile = null;
            frame.Metadata.XmpProfile = null;
        }
    }

    private static IImageEncoder CreateEncoder(IImageFormat format, ImageProcessingOptions settings) => format switch
    {
        JpegFormat => new JpegEncoder { Quality = settings.JpegQuality },
        PngFormat => new PngEncoder(),
        GifFormat => new GifEncoder(),
        WebpFormat => new WebpEncoder(),
        BmpFormat => new BmpEncoder(),
        TiffFormat => new TiffEncoder(),
        _ => throw new ImageValidationException($"不支援重新編碼的圖片格式：{format.Name}")
    };
}
