using System.Text;
using FluentAssertions;
using SGS.Modules.RPT.Application.Pdf;
using SGS.Modules.RPT.Application.Services;
using Xunit;

namespace SGS.Modules.RPT.Tests;

public class ReportPdfAndMaskingTests
{
    private static ReportPdfData Data(bool preview) => new(
        "FOOD-202611-0001", 1, "重金屬檢驗報告", "食品檢測", "台中實驗室", "某某食品股份有限公司",
        "S-0001", "花生醬 500g",
        [new ReportPdfResultRow("鉛 (Pb)", "0.01", "mg/kg", "≤ 0.1", "ICP-MS"), new ReportPdfResultRow("鎘 (Cd)", "N.D.", "mg/kg", "≤ 0.05", "ICP-MS")],
        preview ? null : "王大明", preview ? null : "TAF-S-1234", preview ? null : DateTimeOffset.UtcNow,
        preview ? null : "0123456789ABCDEF0123456789ABCDEF", preview ? null : "https://lims.example/verify/0123456789ABCDEF0123456789ABCDEF",
        null, preview, preview ? "預覽 · tester" : null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Generates_valid_pdf(bool preview)
    {
        var bytes = ReportPdfGenerator.Generate(Data(preview));

        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        bytes.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void Blind_fields_are_masked_without_permission()
    {
        ReportFieldMasker.Mask("客戶甲", canSee: false).Should().Be(ReportFieldMasker.MaskedText);
        ReportFieldMasker.Mask("客戶甲", canSee: true).Should().Be("客戶甲");
        ReportFieldMasker.Mask(null, canSee: false).Should().BeNull();
    }
}
