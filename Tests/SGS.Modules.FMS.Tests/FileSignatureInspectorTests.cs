using System.IO.Compression;
using System.Text;
using FluentAssertions;
using SGS.Modules.FMS.Infrastructure.Options;
using SGS.Modules.FMS.Application.Validation;
using Xunit;

namespace SGS.Modules.FMS.Tests;

public class FileSignatureInspectorTests
{
    private static readonly string[] Allowed = FileManagementOptions.DefaultAllowedExtensions;
    private readonly FileSignatureInspector _inspector = new();

    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46];

    private Task<FileInspectionResult> InspectAsync(byte[] content, string fileName, string[]? allowed = null) =>
        _inspector.InspectAsync(new MemoryStream(content), fileName, allowed ?? Allowed);

    [Theory]
    [InlineData("photo.png")]
    [InlineData("PHOTO.PNG")]
    public async Task Png_with_png_extension_is_accepted(string fileName)
    {
        var result = await InspectAsync(PngHeader, fileName);

        result.IsValid.Should().BeTrue();
        result.Format!.ContentType.Should().Be("image/png");
        result.Format.IsImage.Should().BeTrue();
        result.Format.Extension.Should().Be(".png");
    }

    [Theory]
    [InlineData("a.jpg", ".jpg")]
    [InlineData("a.jpeg", ".jpeg")]
    public async Task Jpeg_accepts_both_extensions(string fileName, string expected)
    {
        var result = await InspectAsync(JpegHeader, fileName);

        result.IsValid.Should().BeTrue();
        result.Format!.Extension.Should().Be(expected);
    }

    [Fact]
    public async Task Png_content_renamed_to_pdf_is_rejected()
    {
        var result = await InspectAsync(PngHeader, "report.pdf");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("副檔名不符");
    }

    [Fact]
    public async Task Windows_executable_renamed_to_jpg_is_rejected()
    {
        byte[] pe = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

        var result = await InspectAsync(pe, "cat.jpg");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("可執行檔");
    }

    [Fact]
    public async Task Extension_outside_whitelist_is_rejected()
    {
        var result = await InspectAsync(Encoding.UTF8.GetBytes("<svg onload=alert(1)>"), "x.svg");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("不允許的檔案類型");
    }

    [Fact]
    public async Task Category_whitelist_is_enforced()
    {
        var result = await InspectAsync(Encoding.ASCII.GetBytes("%PDF-1.7\n"), "a.pdf", [".png"]);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Pdf_is_accepted()
    {
        var result = await InspectAsync(Encoding.ASCII.GetBytes("%PDF-1.7\n%....\n"), "doc.pdf");

        result.IsValid.Should().BeTrue();
        result.Format!.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Utf8_csv_is_accepted()
    {
        var result = await InspectAsync(Encoding.UTF8.GetBytes("名稱,數量\n樣品A,3\n"), "data.csv");

        result.IsValid.Should().BeTrue();
        result.Format!.ContentType.Should().StartWith("text/csv");
    }

    [Theory]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("  <?php echo 1; ?>")]
    public async Task Markup_disguised_as_text_is_rejected(string content)
    {
        var result = await InspectAsync(Encoding.UTF8.GetBytes(content), "notes.txt");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Binary_disguised_as_text_is_rejected()
    {
        var result = await InspectAsync([0x41, 0x00, 0x42], "notes.txt");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Docx_structure_is_accepted_and_renamed_xlsx_is_rejected()
    {
        var docx = BuildZip("[Content_Types].xml", "word/document.xml");

        (await InspectAsync(docx, "a.docx")).IsValid.Should().BeTrue();
        (await InspectAsync(docx, "a.xlsx")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Macro_enabled_office_document_is_rejected()
    {
        var docm = BuildZip("[Content_Types].xml", "word/document.xml", "word/vbaProject.bin");

        var result = await InspectAsync(docm, "a.docx");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("巨集");
    }

    [Fact]
    public async Task Plain_zip_is_rejected()
    {
        var zip = BuildZip("readme.txt");

        (await InspectAsync(zip, "a.docx")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Stream_position_is_reset_after_inspection()
    {
        var stream = new MemoryStream(PngHeader);

        await _inspector.InspectAsync(stream, "a.png", Allowed);

        stream.Position.Should().Be(0);
    }

    private static byte[] BuildZip(params string[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(entry).Open());
                writer.Write("<x/>");
            }
        }

        return buffer.ToArray();
    }
}
