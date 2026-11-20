using FluentAssertions;
using SGS.Modules.MAN.Application.Contracts;
using SGS.Modules.MAN.Application.Rules;
using Xunit;

namespace SGS.Modules.MAN.Tests;

public sealed class ManualTopicRulesTests
{
    private static SaveManualTopicRequest Valid(string key = "export_finance_report", string tooltip = "匯出財務報表為 Excel") =>
        new(key, "匯出財務報表", "財務", tooltip, "1. 點擊**匯出**", "", 0);

    [Theory]
    [InlineData("export_finance_report", true)]
    [InlineData("a", true)]
    [InlineData("01-login.page", true)]
    [InlineData("", false)]
    [InlineData("Export", false)]
    [InlineData("_start", false)]
    [InlineData("has space", false)]
    [InlineData("中文", false)]
    public void FeatureKey_格式驗證(string key, bool expected) =>
        ManualTopicRules.IsValidFeatureKey(key).Should().Be(expected);

    [Fact]
    public void FeatureKey_超過長度應不通過() =>
        ManualTopicRules.IsValidFeatureKey(new string('a', 101)).Should().BeFalse();

    [Fact]
    public void 合法請求應通過() =>
        ManualTopicRules.Validate(Valid()).Should().BeNull();

    [Fact]
    public void Tooltip_超過30字應不通過() =>
        ManualTopicRules.Validate(Valid(tooltip: new string('字', 31))).Should().NotBeNull();

    [Fact]
    public void Tooltip_剛好30字應通過() =>
        ManualTopicRules.Validate(Valid(tooltip: new string('字', 30))).Should().BeNull();

    [Fact]
    public void 標題為空應不通過() =>
        ManualTopicRules.Validate(Valid() with { Title = "  " }).Should().NotBeNull();

    [Fact]
    public void 發布須有操作步驟()
    {
        ManualTopicRules.ValidateForPublish("").Should().NotBeNull();
        ManualTopicRules.ValidateForPublish(null).Should().NotBeNull();
        ManualTopicRules.ValidateForPublish("1. 步驟").Should().BeNull();
    }

    [Theory]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-3, 500, 1, 100)]
    [InlineData(2, 10, 2, 10)]
    public void 分頁參數應被正規化(int index, int size, int expectedIndex, int expectedSize) =>
        ManualTopicRules.NormalizePaging(index, size).Should().Be((expectedIndex, expectedSize));
}
