using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class UatRulesTests
{
    [Fact]
    public void Hash_IsStable_AndNormalizesWhitespaceAndCase()
    {
        var a = UatRules.ComputeHash("rag", "v1.0.0", "  登入\r\n報表匯出  ");
        var b = UatRules.ComputeHash("RAG ", "v1.0.0", "登入\n報表匯出");
        a.Should().Be(b);
        a.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Hash_ChangesWithAnyInput()
    {
        var baseHash = UatRules.ComputeHash("RAG", "v1.0", "範圍");
        UatRules.ComputeHash("RAG", "v1.1", "範圍").Should().NotBe(baseHash);
        UatRules.ComputeHash("RAG", "v1.0", "範圍 2").Should().NotBe(baseHash);
        UatRules.ComputeHash("ABC", "v1.0", "範圍").Should().NotBe(baseHash);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("  ", false)]
    [InlineData("登入與報表", true)]
    public void ValidateCreate_RequiresScope(string? scope, bool ok) =>
        (UatRules.ValidateCreate(scope) is null).Should().Be(ok);

    [Fact]
    public void ValidateCreate_RejectsOverlongScope() =>
        UatRules.ValidateCreate(new string('字', 2001)).Should().NotBeNull();

    [Theory]
    [InlineData(UatStatus.Accepted)]
    [InlineData(UatStatus.Rejected)]
    [InlineData(UatStatus.Cancelled)]
    public void Record_OnlyWhenPendingSign(UatStatus current) =>
        UatRules.ValidateRecord(current, UatStatus.Accepted, "Email 確認", null).Outcome.Should().Be(RuleOutcome.InvalidTransition);

    [Fact]
    public void Record_AcceptedNeedsProxyReason()
    {
        UatRules.ValidateRecord(UatStatus.PendingSign, UatStatus.Accepted, null, null).IsOk.Should().BeFalse();
        UatRules.ValidateRecord(UatStatus.PendingSign, UatStatus.Accepted, "  ", null).IsOk.Should().BeFalse();
        UatRules.ValidateRecord(UatStatus.PendingSign, UatStatus.Accepted, "客戶 11/14 Email 回覆同意", null).IsOk.Should().BeTrue();
    }

    [Fact]
    public void Record_RejectedNeedsComment()
    {
        UatRules.ValidateRecord(UatStatus.PendingSign, UatStatus.Rejected, "Email", null).IsOk.Should().BeFalse();
        UatRules.ValidateRecord(UatStatus.PendingSign, UatStatus.Rejected, "Email", "報表金額與規格不符").IsOk.Should().BeTrue();
    }

    [Theory]
    [InlineData(UatStatus.PendingSign)]
    [InlineData(UatStatus.Cancelled)]
    public void Record_ResultMustBeAcceptedOrRejected(UatStatus result) =>
        UatRules.ValidateRecord(UatStatus.PendingSign, result, "Email", "x").IsOk.Should().BeFalse();

    [Fact]
    public void Cancel_OnlyPending_WithReason()
    {
        UatRules.ValidateCancel(UatStatus.PendingSign, "範圍調整").IsOk.Should().BeTrue();
        UatRules.ValidateCancel(UatStatus.PendingSign, " ").IsOk.Should().BeFalse();
        UatRules.ValidateCancel(UatStatus.Accepted, "x").IsOk.Should().BeFalse();
        UatRules.ValidateCancel(UatStatus.Rejected, "x").IsOk.Should().BeFalse();
    }
}
