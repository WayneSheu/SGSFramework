using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PDFtoImage;
using SGS.Modules.FMS.Application.Auditing;
using SGS.Modules.FMS.Application.Watermark;
using SGS.Modules.FMS.Infrastructure.Entities;
using SGS.Modules.FMS.Infrastructure.Options;
using SGS.Modules.FMS.Infrastructure.Storage;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Core.FileManagement;
using SkiaSharp;
using Xunit;

namespace SGS.Modules.FMS.Tests;

public class WatermarkRendererTests
{
    private readonly FileManagementOptions _options = new() { Enabled = true };

    private WatermarkRenderer CreateRenderer() =>
        new(new TestOptionsMonitor<FileManagementOptions>(_options), NullLogger<WatermarkRenderer>.Instance);

    /// <summary>以 SkiaSharp 產生含文字的 A4 PDF（點：595 × 842）</summary>
    private static byte[] CreatePdf(int pages)
    {
        using var stream = new MemoryStream();
        using (var document = SKDocument.CreatePdf(stream))
        {
            using var paint = new SKPaint { Color = SKColors.Black, TextSize = 24, IsAntialias = true };
            for (var i = 0; i < pages; i++)
            {
                var canvas = document.BeginPage(595, 842);
                canvas.DrawText($"CONFIDENTIAL DOCUMENT PAGE {i + 1}", 60, 120, paint);
                document.EndPage();
            }

            document.Close();
        }

        return stream.ToArray();
    }

    [Fact]
    public async Task Pdf_watermark_keeps_pages_and_size_and_fuses_into_pixels()
    {
        if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux())) return;

        var original = CreatePdf(2);

        var (content, contentType) = await CreateRenderer().RenderAsync(original, "application/pdf", "alice | 2026-11-09 10:00:00 UTC | 10.0.0.1");

        contentType.Should().Be("application/pdf");
        Conversion.GetPageCount(content).Should().Be(2);
        var size = Conversion.GetPageSizes(content)[0];
        size.Width.Should().BeApproximately(595, 2);
        size.Height.Should().BeApproximately(842, 2);

        // 浮水印已融入像素：同頁渲染結果與原文件明顯不同
        using var before = Conversion.ToImage(original, page: (Index)0, options: new RenderOptions(Dpi: 72, BackgroundColor: SKColors.White));
        using var after = Conversion.ToImage(content, page: (Index)0, options: new RenderOptions(Dpi: 72, BackgroundColor: SKColors.White));
        CountDifferentPixels(before, after).Should().BeGreaterThan(before.Width * before.Height / 50);
    }

    [Fact]
    public async Task Pdf_over_page_limit_is_rejected()
    {
        if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux())) return;
        _options.Watermark.MaxPages = 1;

        var act = () => CreateRenderer().RenderAsync(CreatePdf(2), "application/pdf", "bob | now | ip");

        (await act.Should().ThrowAsync<FileRejectedException>()).Which.StatusCode.Should().Be(413);
    }

    [Fact]
    public async Task Image_watermark_keeps_dimensions_and_changes_pixels()
    {
        using var source = new SKBitmap(300, 200);
        source.Erase(SKColors.White);
        using var png = SKImage.FromBitmap(source).Encode(SKEncodedImageFormat.Png, 100);

        var (content, contentType) = await CreateRenderer().RenderAsync(png.ToArray(), "image/png", "王小明 | 2026-11-09 | 10.0.0.1");

        contentType.Should().Be("image/png");
        using var result = SKBitmap.Decode(content);
        (result.Width, result.Height).Should().Be((300, 200));
        CountDifferentPixels(source, result).Should().BeGreaterThan(300);
    }

    [Fact]
    public void Unsupported_formats_are_reported()
    {
        var renderer = CreateRenderer();

        renderer.Supports("application/pdf").Should().BeTrue();
        renderer.Supports("image/jpeg").Should().BeTrue();
        renderer.Supports("text/csv; charset=utf-8").Should().BeFalse();
        renderer.Supports("application/vnd.openxmlformats-officedocument.wordprocessingml.document").Should().BeFalse();
    }

    private static int CountDifferentPixels(SKBitmap a, SKBitmap b)
    {
        var count = 0;
        for (var y = 0; y < Math.Min(a.Height, b.Height); y++)
        for (var x = 0; x < Math.Min(a.Width, b.Width); x++)
        {
            if (a.GetPixel(x, y) != b.GetPixel(x, y)) count++;
        }

        return count;
    }
}

public class FileAccessAuditorTests
{
    private static (FileAccessAuditor Auditor, FileAccessLogQueue Queue) Create(HttpContext? context)
    {
        var queue = new FileAccessLogQueue();
        var accessor = new HttpContextAccessor { HttpContext = context };
        return (new FileAccessAuditor(queue, accessor, TimeProvider.System, NullLogger<FileAccessAuditor>.Instance), queue);
    }

    private static DefaultHttpContext Context(params Claim[] claims)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) };
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.1.2.3");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.9";
        context.Request.Headers["X-Correlation-ID"] = "trace-001";
        return context;
    }

    [Fact]
    public async Task System_account_is_distinguished_by_client_id()
    {
        var (auditor, queue) = Create(Context(
            new Claim(ClaimTypes.NameIdentifier, "sys_scheduler"),
            new Claim(ClaimTypes.Name, "sys_scheduler"),
            new Claim(SystemAccountClaims.AccountType, SystemAccountClaims.SystemAccountType),
            new Claim(SystemAccountClaims.ClientId, "physlims-scheduler")));

        await auditor.RecordAsync(FileAccessActions.Purge, true, Guid.NewGuid(), "General", "保留期滿");

        queue.Reader.TryRead(out var entry).Should().BeTrue();
        entry!.IsSystemAccount.Should().BeTrue();
        entry.ClientId.Should().Be("physlims-scheduler");
        entry.UserId.Should().Be("sys_scheduler");
        entry.RemoteIp.Should().Be("10.1.2.3");
        entry.ForwardedFor.Should().Be("203.0.113.9");
        entry.TraceId.Should().Be("trace-001");
    }

    [Fact]
    public async Task Human_user_is_recorded_with_name()
    {
        var (auditor, queue) = Create(Context(new Claim(ClaimTypes.NameIdentifier, "u-01"), new Claim(ClaimTypes.Name, "alice")));

        await auditor.RecordAsync(FileAccessActions.Download, true, Guid.NewGuid(), "General");

        queue.Reader.TryRead(out var entry).Should().BeTrue();
        entry!.IsSystemAccount.Should().BeFalse();
        entry.ClientId.Should().BeNull();
        entry.UserName.Should().Be("alice");
    }

    [Fact]
    public async Task Background_work_without_http_context_is_recorded_as_system()
    {
        var (auditor, queue) = Create(null);

        await auditor.RecordAsync(FileAccessActions.Encrypt, false, detail: new string('x', 900));

        queue.Reader.TryRead(out var entry).Should().BeTrue();
        entry!.UserName.Should().Be("System");
        entry.Detail!.Length.Should().Be(500);
    }
}

public class StorageInfrastructureTests
{
    [Fact]
    public async Task Range_stream_reads_sequentially_with_single_request_and_reopens_on_seek()
    {
        var data = Enumerable.Range(0, 10_000).Select(i => (byte)i).ToArray();
        var opens = new List<long>();
        await using var stream = new RangeReadStream(data.Length, (from, _) =>
        {
            opens.Add(from);
            return Task.FromResult<Stream>(new MemoryStream(data, (int)from, data.Length - (int)from));
        });

        using var all = new MemoryStream();
        await stream.CopyToAsync(all, 1000);
        all.ToArray().Should().Equal(data);
        opens.Should().Equal(0L);

        stream.Seek(5000, SeekOrigin.Begin);
        var buffer = new byte[10];
        await stream.ReadExactlyAsync(buffer);
        buffer.Should().Equal(data.AsSpan(5000, 10).ToArray());
        opens.Should().Equal(0L, 5000L);
    }

    [Fact]
    public void Factory_does_not_build_drivers_until_used()
    {
        // 未啟用（組態留白）時，注入 IFileManagementService 的服務（例如 RPT）仍須可建構
        var options = Options.Create(new FileManagementOptions
        {
            Enabled = false,
            Targets = { [StorageTarget.LocalPrivate] = new StorageTargetOptions { RootPath = "" } }
        });

        using var factory = new StorageFactory(options, NullLoggerFactory.Instance);

        var act = () => factory.GetProvider(StorageTarget.LocalPrivate);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validator_requires_driver_specific_settings()
    {
        var env = new FakeHostEnvironment();
        var options = new FileManagementOptions
        {
            Enabled = true,
            Targets =
            {
                [StorageTarget.LocalPrivate] = new StorageTargetOptions { Provider = StorageProviderKind.S3 },
                [StorageTarget.CloudPublic] = new StorageTargetOptions { Provider = StorageProviderKind.AzureBlob }
            }
        };

        var result = new FileManagementOptionsValidator(env).Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("S3");
    }

    private sealed class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
