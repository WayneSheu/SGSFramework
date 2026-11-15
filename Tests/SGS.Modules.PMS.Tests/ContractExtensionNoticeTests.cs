using FluentAssertions;
using SGS.Modules.PMS.Application.Services;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class ContractExtensionNoticeTests
{
    private static ExtensionNoticeData Data(bool official = true, string? message = null, string coop = "提供正式機環境與 API 介接測試帳號") => new(
        "CT-2026-001", "XX 公司 ERP 導入專案", "XX 股份有限公司", "王經理", coop, "含 VPN 帳號",
        new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 10), 6, 6, new DateOnly(2027, 1, 12), new DateOnly(2026, 11, 17),
        "陳PM", "乙方公司", message, official, DateTimeOffset.Parse("2026-11-10T02:00:00Z"));

    [Fact]
    public void Render_ProducesPdf_ForOfficialAndPreview()
    {
        foreach (var official in new[] { true, false })
        {
            var bytes = ContractExtensionNoticePdf.Render(Data(official, "請盡快協助"));

            bytes.Length.Should().BeGreaterThan(1000);
            System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        }
    }

    [Fact]
    public void Body_EncodesDynamicText()
    {
        var body = ContractExtensionNoticePdf.Body(Data(message: "<script>alert(1)</script>", coop: "<b>環境</b>"));

        body.Should().NotContain("<script>").And.NotContain("<b>環境</b>");
        body.Should().Contain("&lt;script&gt;").And.Contain("&lt;b&gt;環境&lt;/b&gt;");
    }

    [Fact]
    public void Body_MentionsDelayExtensionAndRequestedDate()
    {
        var body = ContractExtensionNoticePdf.Body(Data());

        body.Should().Contain("6 個工作日").And.Contain("2026 年 11 月 17 日");
    }

    [Fact]
    public void CustomSubjectAndBody_ReplaceDefaults()
    {
        var d = Data() with { Subject = "【自訂主旨】環境尚未提供", BodyText = "第一段自訂內容\n\n第二段自訂內容" };

        ContractExtensionNoticePdf.EffectiveSubject(d).Should().Be("【自訂主旨】環境尚未提供");
        ContractExtensionNoticePdf.EffectiveBody(d).Should().Be("第一段自訂內容\n\n第二段自訂內容");

        var body = ContractExtensionNoticePdf.Body(d);
        body.Should().Contain("第一段自訂內容").And.Contain("第二段自訂內容").And.NotContain("感謝貴司團隊");

        System.Text.Encoding.ASCII.GetString(ContractExtensionNoticePdf.Render(d), 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void BlankSubjectAndBody_FallBackToDefaults()
    {
        var d = Data() with { Subject = "  ", BodyText = "" };

        ContractExtensionNoticePdf.EffectiveSubject(d).Should().Be(ContractExtensionNoticePdf.DefaultSubject(d.ContractTitle));
        ContractExtensionNoticePdf.EffectiveBody(d).Should().Be(ContractExtensionNoticePdf.DefaultBody(d));
    }

    [Fact]
    public void DefaultBody_ContainsKeyFacts()
    {
        var body = ContractExtensionNoticePdf.DefaultBody(Data());

        body.Should().Contain("CT-2026-001").And.Contain("6 個工作日").And.Contain("2026 年 11 月 17 日");
        body.Split("\n\n").Should().HaveCount(3);
    }

    [Fact]
    public void EditedBody_IsHtmlEncodedInEmail()
    {
        var body = ContractExtensionNoticePdf.Body(Data() with { BodyText = "<img src=x onerror=alert(1)>\n\n第二段" });

        body.Should().NotContain("<img").And.Contain("&lt;img");
    }

    [Fact]
    public void DefaultClosingAndSignature_FollowTemplate()
    {
        var d = Data();

        ContractExtensionNoticePdf.EffectiveClosing(d).Should().Be("順頌商祺");
        ContractExtensionNoticePdf.EffectiveSignature(d).Should().Be("乙方公司　專案經理　陳PM　敬上");
        ContractExtensionNoticePdf.DefaultSignature(d).Should().Be("乙方公司　專案經理　陳PM　敬上");
    }

    [Fact]
    public void CustomClosingAndSignature_AppearInEmailAndAreEncoded()
    {
        var d = Data() with { Closing = "敬祝 平安順心", Signature = "XX 科技 <b>專案經理</b> 王小明 敬上" };

        ContractExtensionNoticePdf.EffectiveClosing(d).Should().Be("敬祝 平安順心");
        var body = ContractExtensionNoticePdf.Body(d);
        body.Should().Contain("敬祝 平安順心").And.NotContain("順頌商祺");
        body.Should().Contain("&lt;b&gt;專案經理&lt;/b&gt;").And.NotContain("<b>專案經理</b>");
        System.Text.Encoding.ASCII.GetString(ContractExtensionNoticePdf.Render(d), 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void BlankClosingAndSignature_FallBackToDefaults()
    {
        var d = Data() with { Closing = " ", Signature = "" };

        ContractExtensionNoticePdf.EffectiveClosing(d).Should().Be(ContractExtensionNoticePdf.DefaultClosing);
        ContractExtensionNoticePdf.EffectiveSignature(d).Should().Be(ContractExtensionNoticePdf.DefaultSignature(d));
    }

    [Fact]
    public void Subject_AndFileName_FollowConvention()
    {
        ContractExtensionNoticePdf.Subject("ERP 專案").Should().Be("【專案進度通知】關於「ERP 專案」因待貴司提供協力事項之工期順延說明");
        ContractExtensionNoticePdf.FileName(new DateOnly(2026, 11, 10), "CT-2026-001", 2).Should().Be("20261110_CT-2026-001_協力遲延與工期展延通知書_2.pdf");
    }
}
