using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class UatNamingTests
{
    private static readonly DateOnly Date = new(2026, 11, 14);

    [Fact]
    public void Generate_FollowsPattern_AndKeepsExtension()
    {
        var name = DeliverableNaming.GenerateUat(Date, "rag", "1.0.0", "客戶 Email 回覆", ".PDF");
        name.Success.Should().BeTrue();
        name.FileName.Should().Be("20261114_RAG_UAT_v1.0.0_客戶-Email-回覆.pdf");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("R_AG")]
    public void Generate_RejectsBadProjectCode(string? code) =>
        DeliverableNaming.GenerateUat(Date, code, "v1.0", "說明", ".png").Success.Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("最終版")]
    [InlineData("1")]
    public void Generate_RejectsBadVersion(string? version) =>
        DeliverableNaming.GenerateUat(Date, "RAG", version, "說明", ".png").Success.Should().BeFalse();

    [Fact]
    public void Generate_RequiresDescriptionAndExtension()
    {
        DeliverableNaming.GenerateUat(Date, "RAG", "v1.0", "  ", ".png").Success.Should().BeFalse();
        DeliverableNaming.GenerateUat(Date, "RAG", "v1.0", "說明", "").Success.Should().BeFalse();
    }

    [Fact]
    public void Generate_SanitizesUnderscoresAndPathCharacters_SoSegmentsStayIntact()
    {
        var name = DeliverableNaming.GenerateUat(Date, "RAG", "v1.0", @"a_b\c:d", ".txt");
        name.Success.Should().BeTrue();
        // 說明中的底線不可破壞「底線分段」結構；固定 5 段（日期、專案、UAT、版本、說明）
        name.FileName!.Replace(".txt", string.Empty).Split('_').Should().HaveCount(5);
        name.FileName.Should().NotContainAny("\\", ":");
    }

    [Fact]
    public void Generate_RejectsOverlongNames() =>
        DeliverableNaming.GenerateUat(Date, "RAG", "v1.0", new string('字', 300), ".png").Success.Should().BeFalse();
}
