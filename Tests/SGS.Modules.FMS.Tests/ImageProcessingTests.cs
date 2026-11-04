using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SGS.Modules.FMS.Application.HostedServices;
using SGS.Modules.FMS.Application.Imaging;
using SGS.Modules.FMS.Infrastructure.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SGS.Modules.FMS.Tests;

public sealed class ImageProcessingTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FileManagementOptions _options = new() { Enabled = true };
    private readonly ImageSharpImageProcessor _processor;

    public ImageProcessingTests() => _processor = new ImageSharpImageProcessor(new TestOptionsMonitor<FileManagementOptions>(_options));

    public void Dispose() => _temp.Dispose();

    private static MemoryStream CreateJpegWithExif(int width, int height, byte[]? trailer = null)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(200, 30, 30));
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Software, "polyglot-test");
        var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        if (trailer is not null) stream.Write(trailer);
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
        output.Position = 0;
        using var decoded = await Image.LoadAsync(output);
        decoded.Metadata.ExifProfile.Should().BeNull();
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
