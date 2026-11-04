using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SGS.Modules.FMS.Application.HostedServices;
using SGS.Modules.FMS.Application.Imaging;
using SGS.Modules.FMS.Infrastructure.Options;
using ImageMagick;
using Xunit;

namespace SGS.Modules.FMS.Tests;

public sealed class ImageProcessingTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FileManagementOptions _options = new() { Enabled = true };
    private readonly MagickImageProcessor _processor;

    public ImageProcessingTests() => _processor = new MagickImageProcessor(new TestOptionsMonitor<FileManagementOptions>(_options));

    public void Dispose() => _temp.Dispose();

    private static MemoryStream CreateJpegWithExif(int width, int height, byte[]? trailer = null)
    {
        using var image = new MagickImage(new MagickColor(200, 30, 30), (uint)width, (uint)height);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Software, "polyglot-test");
        image.SetProfile(exif);
        var stream = new MemoryStream();
        image.Write(stream, MagickFormat.Jpeg);
        if (trailer is not null) stream.Write(trailer);
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateImage(MagickFormat format, int width, int height)
    {
        using var image = new MagickImage(new MagickColor(20, 120, 200), (uint)width, (uint)height);
        var stream = new MemoryStream();
        image.Write(stream, format);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task Reencode_strips_exif_and_appended_payload()
    {
        var payload = "<?php echo 'polyglot'; ?>"u8.ToArray();
        using var input = CreateJpegWithExif(40, 30, payload);
        using var output = new MemoryStream();

        var result = await _processor.ReencodeAsync(input, output);

        result.Should().Be(new ImageProcessResult(40, 30, "image/jpeg"));
        output.ToArray().AsSpan().IndexOf(payload).Should().Be(-1);
        output.ToArray().AsSpan().IndexOf("polyglot-test"u8).Should().Be(-1);
        output.Position = 0;
        using var decoded = new MagickImage(output);
        decoded.GetExifProfile().Should().BeNull();
    }

    [Theory]
    [InlineData(MagickFormat.Png, "image/png")]
    [InlineData(MagickFormat.Bmp, "image/bmp")]
    [InlineData(MagickFormat.Tiff, "image/tiff")]
    [InlineData(MagickFormat.WebP, "image/webp")]
    [InlineData(MagickFormat.Gif, "image/gif")]
    public async Task Reencode_keeps_same_format(MagickFormat format, string contentType)
    {
        using var input = CreateImage(format, 32, 16);
        using var output = new MemoryStream();

        var result = await _processor.ReencodeAsync(input, output);

        result.Should().Be(new ImageProcessResult(32, 16, contentType));
        output.Position = 0;
        new MagickImageInfo(output).Format.Should().Be(format);
    }

    [Fact]
    public async Task Animated_gif_keeps_frames_when_resized()
    {
        using var frames = new MagickImageCollection();
        foreach (var color in new[] { MagickColors.Red, MagickColors.Green, MagickColors.Blue })
        {
            var frame = new MagickImage(color, 80, 40) { AnimationDelay = 10 };
            frames.Add(frame);
        }
        using var input = new MemoryStream();
        frames.Write(input, MagickFormat.Gif);
        input.Position = 0;
        using var output = new MemoryStream();

        var result = await _processor.ResizeAsync(input, output, 40, 0, ImageResizeMode.Max);

        result.Should().Be(new ImageProcessResult(40, 20, "image/gif"));
        output.Position = 0;
        using var decoded = new MagickImageCollection(output);
        decoded.Should().HaveCount(3);
    }

    [Fact]
    public async Task Unsupported_format_is_rejected_without_invoking_imagemagick_detection()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"/>"u8.ToArray();

        var act = () => _processor.ReencodeAsync(new MemoryStream(svg), new MemoryStream());

        await act.Should().ThrowAsync<ImageValidationException>().WithMessage("*格式不受支援*");
    }

    [Fact]
    public async Task Oversized_image_is_rejected_before_full_decode()
    {
        _options.Image.MaxDimension = 100;
        using var input = CreateJpegWithExif(200, 50);

        var act = () => _processor.ReencodeAsync(input, new MemoryStream());

        await act.Should().ThrowAsync<ImageValidationException>().WithMessage("*超過允許上限*");
    }

    [Fact]
    public async Task Corrupted_image_is_rejected()
    {
        byte[] fake = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5];

        var act = () => _processor.ReencodeAsync(new MemoryStream(fake), new MemoryStream());

        await act.Should().ThrowAsync<ImageValidationException>();
    }

    [Theory]
    [InlineData(100, 0, ImageResizeMode.Max, 100, 50)]
    [InlineData(1000, 1000, ImageResizeMode.Max, 400, 200)] // Max 不放大
    [InlineData(100, 100, ImageResizeMode.Crop, 100, 100)]
    [InlineData(100, 100, ImageResizeMode.Pad, 100, 100)]
    public async Task Resize_respects_mode(int w, int h, ImageResizeMode mode, int expectedW, int expectedH)
    {
        using var input = CreateJpegWithExif(400, 200);
        using var output = new MemoryStream();

        var result = await _processor.ResizeAsync(input, output, w, h, mode);

        (result.Width, result.Height).Should().Be((expectedW, expectedH));
    }

    [Fact]
    public async Task Image_cache_creates_once_then_hits()
    {
        _options.ImageCache.RootPath = _temp.Path;
        var cache = new ImageCacheService(new TestOptionsMonitor<FileManagementOptions>(_options), TimeProvider.System, NullLogger<ImageCacheService>.Instance);
        var key = new ImageCacheKey(Guid.NewGuid(), 64, 64, ImageResizeMode.Max, ".png");
        var calls = 0;

        async Task Factory(Stream s, CancellationToken ct)
        {
            calls++;
            await s.WriteAsync(new byte[] { 1, 2, 3 }, ct);
        }

        await using (var first = await cache.GetOrCreateAsync(key, Factory)) { first.Length.Should().Be(3); }
        await using (var second = await cache.GetOrCreateAsync(key, Factory)) { second.Length.Should().Be(3); }

        calls.Should().Be(1);
        cache.Remove(key.FileId).Should().Be(1);
    }

    [Fact]
    public void Lru_cleanup_deletes_least_recently_accessed_until_low_watermark()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _options.ImageCache.RootPath = _temp.Path;
        var monitor = new TestOptionsMonitor<FileManagementOptions>(_options);
        var cache = new ImageCacheService(monitor, clock, NullLogger<ImageCacheService>.Instance);
        var service = new ImageCacheLruCleanupService(cache, monitor, clock, NullLogger<ImageCacheLruCleanupService>.Instance);

        var files = Enumerable.Range(0, 5).Select(i =>
        {
            var path = Path.Combine(_temp.Path, $"f{i}.png");
            File.WriteAllBytes(path, new byte[100]);
            File.SetLastAccessTimeUtc(path, DateTime.UtcNow.AddHours(-10 + i)); // f0 最舊
            return path;
        }).ToList();

        var settings = new ImageCacheOptions { HighWatermarkBytes = 400, LowWatermarkBytes = 250 };
        var deleted = service.RunCleanup(_temp.Path, settings, CancellationToken.None);

        deleted.Should().Be(3);
        files.Where(File.Exists).Should().Equal(files[3], files[4]);
    }
}
