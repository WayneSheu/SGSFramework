using System.Runtime.Versioning;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDFtoImage;
using SGS.Modules.FMS.Infrastructure.Options;
using SGSFramework.Core.FileManagement;
using SkiaSharp;

namespace SGS.Modules.FMS.Application.Watermark;

/// <summary>
/// 動態浮水印檢視：浮水印直接繪入像素（點陣化融合），無法以 PDF 圖層、DOM 或網頁編輯方式分離移除。
/// </summary>
public interface IWatermarkRenderer
{
    bool Supports(string contentType);

    /// <summary>產生帶浮水印的內容（記憶體內處理，不落地）；格式不支援拋出 415、超過頁數上限拋出 413</summary>
    Task<(byte[] Content, string ContentType)> RenderAsync(byte[] content, string contentType, string watermarkText, CancellationToken cancellationToken = default);
}

/// <summary>
/// <list type="bullet">
/// <item>PDF：PDFtoImage（PDFium）逐頁渲染為點陣圖 → SkiaSharp 繪製斜向平鋪浮水印 → JPEG 壓縮 → SKDocument 重組為 PDF（頁面維持原尺寸）。</item>
/// <item>圖片：Magick.NET 解碼（第一影格、依 EXIF 轉正）→ SkiaSharp 繪製浮水印 → 原格式（JPEG）或 PNG。</item>
/// </list>
/// 逐頁渲染並即時釋放，記憶體峰值約為單頁點陣圖；頁數與 DPI 皆有上限防止 CPU / 記憶體耗盡（DoS）。
/// </summary>
public sealed class WatermarkRenderer(IOptionsMonitor<FileManagementOptions> options, ILogger<WatermarkRenderer> logger) : IWatermarkRenderer
{
    private const string Pdf = "application/pdf";

    private static readonly Dictionary<string, MagickFormat> ImageFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = MagickFormat.Jpeg,
        ["image/png"] = MagickFormat.Png,
        ["image/gif"] = MagickFormat.Gif,
        ["image/webp"] = MagickFormat.WebP,
        ["image/bmp"] = MagickFormat.Bmp,
        ["image/tiff"] = MagickFormat.Tiff
    };

    private static readonly string[] FontFamilies = ["Noto Sans TC", "Microsoft JhengHei", "PingFang TC", "Noto Sans CJK TC"];

    private readonly Lazy<SKTypeface> _typeface = new(() => LoadTypeface(options.CurrentValue.Watermark.FontPath, logger));

    private WatermarkOptions Settings => options.CurrentValue.Watermark;

    public bool Supports(string contentType) =>
        contentType.Equals(Pdf, StringComparison.OrdinalIgnoreCase) || ImageFormats.ContainsKey(BaseType(contentType));

    public Task<(byte[] Content, string ContentType)> RenderAsync(byte[] content, string contentType, string watermarkText, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(watermarkText);

        // PDF 渲染與點陣處理皆為 CPU 密集同步運算，移至執行緒集區避免阻塞請求執行緒
        return Task.Run(() => contentType.Equals(Pdf, StringComparison.OrdinalIgnoreCase)
            ? RenderPdf(content, watermarkText, cancellationToken)
            : RenderImage(content, BaseType(contentType), watermarkText), cancellationToken);
    }

    private (byte[], string) RenderPdf(byte[] pdf, string text, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            return RenderPdfCore(pdf, text, cancellationToken);

        throw new FileRejectedException("此平台不支援 PDF 浮水印渲染。", 415);
    }

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    private (byte[], string) RenderPdfCore(byte[] pdf, string text, CancellationToken cancellationToken)
    {
        var settings = Settings;
        int pageCount;
        try
        {
            pageCount = Conversion.GetPageCount(pdf);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new FileRejectedException("PDF 內容無法解析（可能已加密或損毀）。", 422);
        }

        if (pageCount > settings.MaxPages)
            throw new FileRejectedException($"PDF 共 {pageCount} 頁，超過線上檢視上限 {settings.MaxPages} 頁。", 413);

        using var output = new MemoryStream();
        using (var document = SKDocument.CreatePdf(output))
        {
            for (var page = 0; page < pageCount; page++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var bitmap = Conversion.ToImage(pdf, page: (Index)page, options: new RenderOptions(Dpi: settings.Dpi, WithAnnotations: true, WithFormFill: true, BackgroundColor: SKColors.White));
                DrawWatermark(bitmap, text, settings);

                // 頁面尺寸（點）= 像素 × 72 / DPI，維持原文件版面大小
                var widthPt = bitmap.Width * 72f / settings.Dpi;
                var heightPt = bitmap.Height * 72f / settings.Dpi;

                using var encoded = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Jpeg, settings.JpegQuality);
                using var image = SKImage.FromEncodedData(encoded);
                var canvas = document.BeginPage(widthPt, heightPt);
                canvas.DrawImage(image, SKRect.Create(widthPt, heightPt));
                document.EndPage();
            }

            document.Close();
        }

        return (output.ToArray(), Pdf);
    }

    private (byte[], string) RenderImage(byte[] content, string contentType, string text)
    {
        var settings = Settings;
        byte[] png;
        try
        {
            // 以內容類型指定解碼器（不使用 ImageMagick 自動偵測），只取第一影格並依 EXIF 轉正
            using var image = new MagickImage(content, new MagickReadSettings { Format = ImageFormats[contentType], FrameIndex = 0, FrameCount = 1 });
            image.AutoOrient();
            image.Strip();
            png = image.ToByteArray(MagickFormat.Png);
        }
        catch (MagickException ex)
        {
            throw new FileRejectedException($"圖片內容無法解析：{ex.Message}", 422);
        }

        using var bitmap = SKBitmap.Decode(png) ?? throw new FileRejectedException("圖片內容無法解析。", 422);
        DrawWatermark(bitmap, text, settings);

        var asJpeg = contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase);
        using var data = SKImage.FromBitmap(bitmap).Encode(asJpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, settings.JpegQuality);
        return (data.ToArray(), asJpeg ? "image/jpeg" : "image/png");
    }

    /// <summary>斜向（-35°）平鋪半透明浮水印，覆蓋整個畫面，無法以裁切單一區域移除</summary>
    private void DrawWatermark(SKBitmap bitmap, string text, WatermarkOptions settings)
    {
        using var canvas = new SKCanvas(bitmap);
        var fontSize = Math.Clamp(Math.Min(bitmap.Width, bitmap.Height) / 28f, 12f, 72f);

        // 雙色疊印（深色陰影 + 淺色本體）：在淺色、深色與中間色底圖上都清晰可辨，單一灰色在中間色底圖上幾乎看不出來
        using var shadow = new SKPaint
        {
            Typeface = _typeface.Value,
            TextSize = fontSize,
            IsAntialias = true,
            Color = new SKColor(0, 0, 0, (byte)settings.Opacity),
            TextAlign = SKTextAlign.Left
        };
        using var paint = new SKPaint
        {
            Typeface = _typeface.Value,
            TextSize = fontSize,
            IsAntialias = true,
            Color = new SKColor(255, 255, 255, (byte)settings.Opacity),
            TextAlign = SKTextAlign.Left
        };
        var outlineOffset = Math.Max(1f, fontSize / 18f);

        var textWidth = paint.MeasureText(text);
        var stepX = textWidth + fontSize * 4;
        var stepY = fontSize * 6;
        var diagonal = (float)Math.Sqrt((double)bitmap.Width * bitmap.Width + (double)bitmap.Height * bitmap.Height);

        canvas.Save();
        canvas.Translate(bitmap.Width / 2f, bitmap.Height / 2f);
        canvas.RotateDegrees(-35);
        var row = 0;
        for (var y = -diagonal; y < diagonal; y += stepY, row++)
        {
            // 每列錯位半格，避免浮水印成直線容易被遮蔽
            var offset = row % 2 == 0 ? 0 : stepX / 2;
            for (var x = -diagonal - offset; x < diagonal; x += stepX)
            {
                canvas.DrawText(text, x, y, shadow);
                canvas.DrawText(text, x - outlineOffset, y - outlineOffset, paint);
            }
        }

        canvas.Restore();
        canvas.Flush();
    }

    private static SKTypeface LoadTypeface(string? fontPath, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(fontPath))
        {
            var fromFile = SKTypeface.FromFile(fontPath);
            if (fromFile is not null) return fromFile;
            logger.LogWarning("[Watermark] 無法載入浮水印字型檔 {FontPath}，改用系統字型。", fontPath);
        }

        foreach (var family in FontFamilies)
        {
            var typeface = SKTypeface.FromFamilyName(family);
            if (typeface is not null && typeface.FamilyName.Equals(family, StringComparison.OrdinalIgnoreCase)) return typeface;
            typeface?.Dispose();
        }

        // 最後手段：依中文字元尋找可用字型
        var matched = SKFontManager.Default.MatchCharacter('檔');
        if (matched is not null) return matched;

        logger.LogWarning("[Watermark] 找不到支援中文的字型，浮水印中文字可能無法顯示；請設定 FileManagement:Watermark:FontPath。");
        return SKTypeface.Default;
    }

    private static string BaseType(string contentType) => contentType.Split(';')[0].Trim();
}
