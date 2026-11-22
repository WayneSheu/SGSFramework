using FluentAssertions;
using SGS.Modules.MTG.Application.Rules;
using Xunit;

namespace SGS.Modules.MTG.Tests;

public class ScenarioDefaultsTests
{
    [Fact]
    public void 內建四種場景且代碼唯一()
    {
        ScenarioDefaults.All.Should().HaveCount(4);
        ScenarioDefaults.All.Select(s => s.Code).Should().OnlyHaveUniqueItems();
        ScenarioDefaults.All.Select(s => s.Code).Should().BeEquivalentTo(
            [ScenarioDefaults.General, ScenarioDefaults.ClientDiscovery, ScenarioDefaults.ArchitectureReview, ScenarioDefaults.WeeklySync]);
    }

    [Fact]
    public void 內建場景代碼都符合代碼規則且提示詞不為空()
    {
        foreach (var s in ScenarioDefaults.All)
        {
            ScenarioDefaults.IsValidCode(s.Code).Should().BeTrue(s.Code);
            s.DisplayName.Should().NotBeNullOrWhiteSpace();
            s.MapPrompt.Should().NotBeNullOrWhiteSpace();
            s.ReducePrompt.Should().Contain("## 1.", "Reduce 必須給出固定的 Markdown 骨架");
        }
    }

    [Theory]
    [InlineData("general", true)]
    [InlineData("client-discovery", true)]
    [InlineData("a", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("General", false)]
    [InlineData("-bad", false)]
    [InlineData("has space", false)]
    [InlineData("中文", false)]
    public void 場景代碼驗證(string? code, bool valid) => ScenarioDefaults.IsValidCode(code).Should().Be(valid);
}
