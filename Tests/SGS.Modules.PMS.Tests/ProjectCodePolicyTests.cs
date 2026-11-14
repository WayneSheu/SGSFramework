using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class ProjectCodePolicyTests
{
    [Theory]
    [InlineData("PRJ")]
    [InlineData("LIMS2")]
    [InlineData("AB")]
    [InlineData("A123456789")]
    public void ValidFormat_Accepted(string code) => ProjectCodePolicy.IsValidFormat(code).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1ABC")]
    [InlineData("prj")]
    [InlineData("PR-J")]
    [InlineData("A1234567890")]
    [InlineData("專案")]
    public void InvalidFormat_Rejected(string? code) => ProjectCodePolicy.IsValidFormat(code).Should().BeFalse();

    [Fact]
    public void Normalize_TrimsAndUppercases() => ProjectCodePolicy.Normalize("  prj ").Should().Be("PRJ");

    [Theory]
    [InlineData("SYS")]
    [InlineData("TEST")]
    [InlineData("ADMIN")]
    public void ReservedWords_Detected(string code) => ProjectCodePolicy.IsReservedWord(code).Should().BeTrue();

    [Fact]
    public void Check_Available_WhenUnused() =>
        ProjectCodePolicy.Check("PRJ", null).Should().Be(ProjectCodeCheck.Available);

    [Fact]
    public void Check_InUse_IsAlreadyUsed() =>
        ProjectCodePolicy.Check("PRJ", ProjectCodeUsage.InUse).Should().Be(ProjectCodeCheck.AlreadyUsed);

    [Fact]
    public void Check_Retired_NeverReusable() =>
        ProjectCodePolicy.Check("PRJ", ProjectCodeUsage.Retired).Should().Be(ProjectCodeCheck.Retired);

    [Fact]
    public void Check_InvalidFormat_BeatsUsage() =>
        ProjectCodePolicy.Check("1bad", null).Should().Be(ProjectCodeCheck.Invalid);

    [Fact]
    public void Check_ReservedWord_NotAvailable() =>
        ProjectCodePolicy.Check("ADMIN", null).Should().Be(ProjectCodeCheck.Reserved);

    [Fact]
    public void Check_ExtraReservedWords_Honored() =>
        ProjectCodePolicy.Check("ACME", null, ["ACME"]).Should().Be(ProjectCodeCheck.Reserved);

    [Fact]
    public void Suggest_UsesInitialsOfWords()
    {
        var result = ProjectCodePolicy.Suggest("Smart Lab System", _ => false);
        result.Should().Contain("SLS");
        result.Should().OnlyContain(c => ProjectCodePolicy.IsValidFormat(c));
    }

    [Fact]
    public void Suggest_AvoidsTakenCodes_ByAddingSuffix()
    {
        var taken = new HashSet<string> { "SLS" };
        var result = ProjectCodePolicy.Suggest("Smart Lab System", taken.Contains);
        result.Should().NotContain("SLS");
        result.Should().Contain("SLS2");
    }

    [Fact]
    public void Suggest_ChineseName_FallsBackToPrjSequence()
    {
        var result = ProjectCodePolicy.Suggest("實驗室系統", c => c == "PRJ");
        result.Should().HaveCount(3);
        result.Should().NotContain("PRJ");
        result.Should().OnlyContain(c => ProjectCodePolicy.IsValidFormat(c));
    }

    [Fact]
    public void Suggest_NeverReturnsReservedWordsOrDuplicates()
    {
        var result = ProjectCodePolicy.Suggest("Test", _ => false, 5);
        result.Should().OnlyHaveUniqueItems();
        result.Should().NotContain(c => ProjectCodePolicy.IsReservedWord(c));
    }
}
