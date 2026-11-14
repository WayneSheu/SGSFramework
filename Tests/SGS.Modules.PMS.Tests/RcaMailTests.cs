using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Application.Services;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class RcaMailTests
{
    [Fact]
    public void Parse_SplitsOnCommonSeparators_TrimsAndDeduplicates()
    {
        var (list, error) = RcaMail.ParseRecipients(["a@x.com; b@y.com，c@z.com\nA@X.COM", "  d@w.org  "], "收件人", required: true);
        error.Should().BeNull();
        list.Should().Equal("a@x.com", "b@y.com", "c@z.com", "d@w.org");
    }

    [Fact]
    public void Parse_RequiredVsOptional()
    {
        RcaMail.ParseRecipients(null, "收件人", required: true).Error.Should().Contain("收件人");
        RcaMail.ParseRecipients([" ", ""], "收件人", required: true).Error.Should().NotBeNull();
        RcaMail.ParseRecipients(null, "副本", required: false).Error.Should().BeNull();
        RcaMail.ParseRecipients([], "副本", required: false).Addresses.Should().BeEmpty();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("@x.com")]
    [InlineData("a@x.com.")]
    [InlineData("王經理 <a@x.com>")]
    [InlineData("a@x.com>")]
    public void Parse_RejectsInvalidAddresses(string address) =>
        RcaMail.ParseRecipients([address], "收件人", required: true).Error.Should().NotBeNull();

    [Fact]
    public void Parse_RejectsHeaderInjectionAttempts()
    {
        // 換行被視為分隔符，注入的第二段不是有效地址 → 拒絕
        RcaMail.ParseRecipients(["a@x.com\r\nBcc: evil@x.com"], "收件人", required: true).Error.Should().NotBeNull();
        RcaMail.IsValidAddress("a@x.com\r\nBcc: evil@x.com").Should().BeFalse();
    }

    [Fact]
    public void Parse_LimitsRecipientCount()
    {
        var many = Enumerable.Range(1, RcaMail.MaxRecipients + 1).Select(i => $"u{i}@x.com").ToList();
        RcaMail.ParseRecipients(many, "收件人", required: true).Error.Should().Contain($"{RcaMail.MaxRecipients}");
        RcaMail.ParseRecipients(many.Take(RcaMail.MaxRecipients), "收件人", required: true).Error.Should().BeNull();
    }

    [Fact]
    public void SubjectAndFileName_FollowConvention()
    {
        RcaMail.Subject("RAG", "RAG-012").Should().Be("[RAG] RAG-012 重大異常分析報告（RCA）");
        RcaMail.FileName(new DateOnly(2026, 11, 14), "RAG-012").Should().Be("20261114_RAG-012_RCA重大異常分析報告.pdf");
    }

    [Fact]
    public void Body_EncodesHtml_KeepsLineBreaks_AndSignsAsTeam()
    {
        var body = RcaMail.Body("增生<檢索>", "RAG-012", "請查收\n<script>alert(1)</script>");

        body.Should().NotContain("<script>");
        body.Should().Contain("&lt;script&gt;").And.Contain("請查收<br/>");
        body.Should().Contain("增生&lt;檢索&gt;").And.Contain("RAG-012");
        body.Should().Contain("專案團隊 敬上");
    }

    [Fact]
    public void Body_WithoutMessage_StillHasFixedText() =>
        RcaMail.Body("RAG", "RAG-012", null).Should().Contain("重大異常分析報告").And.NotContain("<p></p>");

    [Fact]
    public void FormatSentTo_IncludesCcOnlyWhenPresent()
    {
        RcaMail.FormatSentTo(["a@x.com"], []).Should().Be("收件：a@x.com");
        RcaMail.FormatSentTo(["a@x.com", "b@x.com"], ["c@x.com"]).Should().Be("收件：a@x.com、b@x.com；副本：c@x.com");
    }

    private static RcaPdfData Sample(bool official) => new(
        "RAG", "增生檢索", "RAG-012", "結帳 API 全面 500", RcaTrigger.P1,
        new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.FromHours(8)), new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.FromHours(8)),
        "維運團隊於 06:00 發現結帳 API 錯誤率上升，影響全部線上交易約 4 小時。",
        [new RcaTimelineItem(new DateTimeOffset(2026, 11, 14, 6, 0, 0, TimeSpan.FromHours(8)), "監控告警"),
         new RcaTimelineItem(new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.FromHours(8)), "完成回滾，服務恢復")],
        "新版本的資料庫連線池設定錯誤，導致連線耗盡。", "回滾至前一版本並重啟服務。", "上線檢核表新增連線池壓力測試項目，並加入告警門檻。",
        official ? "已寄送" : "已提交，待 PM 寄送", new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.FromHours(8)), official);

    [Fact]
    public void Pdf_RendersValidPdf_ForPreviewAndOfficial()
    {
        foreach (var official in new[] { false, true })
        {
            var pdf = RcaReportPdf.Render(Sample(official));
            pdf.Length.Should().BeGreaterThan(1000);
            System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
        }
    }

    [Fact]
    public void Pdf_HandlesEmptyOptionalSections_AndLongText()
    {
        var data = Sample(true) with { Summary = string.Empty, RootCause = new string('字', 4000), Timeline = [] };
        RcaReportPdf.Render(data).Length.Should().BeGreaterThan(1000);
    }
}
