using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class RcaRulesTests
{
    private static readonly DateTimeOffset Recovered = new(2026, 11, 14, 10, 0, 0, TimeSpan.FromHours(8));

    private static readonly RcaTimelineItem[] Timeline =
    [
        new(Recovered.AddHours(-4), "監控告警：結帳 API 錯誤率飆高"),
        new(Recovered, "完成回滾，服務恢復")
    ];

    [Fact]
    public void Due_Is48HoursAfterRecovery() =>
        RcaRules.Due(Recovered).Should().Be(Recovered.AddHours(48));

    [Theory]
    [InlineData(RcaStatus.Draft, 47, true, false)]   // 草稿未逾期
    [InlineData(RcaStatus.Draft, 49, true, true)]    // 草稿已逾期
    [InlineData(RcaStatus.Submitted, 100, false, false)] // 準時提交（提交 40h）後不再逾期
    [InlineData(RcaStatus.Sent, 100, false, false)]
    public void IsOverdue_DraftByNow_SubmittedBySubmitTime(RcaStatus status, int hoursAfterRecovery, bool draftOrNoSubmit, bool expected)
    {
        var due = RcaRules.Due(Recovered);
        var now = Recovered.AddHours(hoursAfterRecovery);
        DateTimeOffset? submittedAt = draftOrNoSubmit ? null : Recovered.AddHours(40);
        RcaRules.IsOverdue(status, due, submittedAt, now).Should().Be(expected);
    }

    [Fact]
    public void IsOverdue_SubmittedLate_StaysOverdue()
    {
        var due = RcaRules.Due(Recovered);
        RcaRules.IsOverdue(RcaStatus.Submitted, due, Recovered.AddHours(60), Recovered.AddHours(70)).Should().BeTrue();
    }

    [Fact]
    public void Eligible_OnlyTriagedIncidents()
    {
        RcaRules.EligibleTriggers(TicketType.Consultation, TicketStatus.InProgress, null, SlaStatus.InProgress).Should().BeEmpty();
        RcaRules.EligibleTriggers(TicketType.Incident, TicketStatus.Received, TicketPriority.P1, SlaStatus.NotApplicable).Should().BeEmpty();
        RcaRules.EligibleTriggers(TicketType.Incident, TicketStatus.Cancelled, TicketPriority.P1, SlaStatus.NotApplicable).Should().BeEmpty();
    }

    [Fact]
    public void Eligible_P1_And_SlaBreach_And_AlwaysDowngrade()
    {
        RcaRules.EligibleTriggers(TicketType.Incident, TicketStatus.InProgress, TicketPriority.P1, SlaStatus.InProgress)
            .Should().Equal(RcaTrigger.P1, RcaTrigger.Downgrade);
        RcaRules.EligibleTriggers(TicketType.Incident, TicketStatus.InProgress, TicketPriority.P2, SlaStatus.Overdue)
            .Should().Equal(RcaTrigger.SlaBreach, RcaTrigger.Downgrade);
        RcaRules.EligibleTriggers(TicketType.Incident, TicketStatus.Closed, TicketPriority.P1, SlaStatus.MetLate)
            .Should().Equal(RcaTrigger.P1, RcaTrigger.SlaBreach, RcaTrigger.Downgrade);
        RcaRules.EligibleTriggers(TicketType.Incident, TicketStatus.InProgress, TicketPriority.P3, SlaStatus.MetOnTime)
            .Should().Equal(RcaTrigger.Downgrade);
    }

    [Fact]
    public void ValidateDraft_AllowsPartialContent() =>
        RcaRules.ValidateDraft(RcaTrigger.P1, Recovered, null, null, null, null, null).Should().BeNull();

    [Fact]
    public void ValidateDraft_RejectsBadTriggerAndMissingRecovery()
    {
        RcaRules.ValidateDraft((RcaTrigger)99, Recovered, null, null, null, null, null).Should().NotBeNull();
        RcaRules.ValidateDraft(RcaTrigger.P1, default, null, null, null, null, null).Should().Contain("恢復時間");
    }

    [Fact]
    public void ValidateDraft_TimelineChecks()
    {
        RcaRules.ValidateDraft(RcaTrigger.P1, Recovered, null, null, null, null, [new RcaTimelineItem(default, "x")]).Should().NotBeNull();
        RcaRules.ValidateDraft(RcaTrigger.P1, Recovered, null, null, null, null, [new RcaTimelineItem(Recovered, " ")]).Should().NotBeNull();
        RcaRules.ValidateDraft(RcaTrigger.P1, Recovered, null, null, null, null,
            Enumerable.Range(0, RcaRules.MaxTimelineItems + 1).Select(i => new RcaTimelineItem(Recovered.AddMinutes(i), "e")).ToList()).Should().NotBeNull();
        RcaRules.ValidateDraft(RcaTrigger.P1, Recovered, null, null, null, null, Timeline).Should().BeNull();
    }

    [Fact]
    public void ValidateForSubmit_RequiresEverything()
    {
        RcaRules.ValidateForSubmit("摘要", "根因", "止血", "預防", Timeline).Should().BeNull();
        RcaRules.ValidateForSubmit(" ", "根因", "止血", "預防", Timeline).Should().Contain("摘要");
        RcaRules.ValidateForSubmit("摘要", "根因", "止血", "預防", [Timeline[0]]).Should().Contain("時間軸");
        RcaRules.ValidateForSubmit("摘要", "", "止血", "預防", Timeline).Should().Contain("根因");
        RcaRules.ValidateForSubmit("摘要", "根因", "", "預防", Timeline).Should().Contain("止血");
        RcaRules.ValidateForSubmit("摘要", "根因", "止血", "", Timeline).Should().Contain("預防");
    }

    [Theory]
    [InlineData("王小明工程師誤刪了設定檔", true)]
    [InlineData("由李大華同事負責回滾", true)]
    [InlineData("維運團隊於 10:00 完成回滾，開發團隊補上防護", false)]
    [InlineData("後端工程師與前端工程師共同確認", false)]
    [InlineData("資深工程師進行驗證", false)]
    [InlineData("客戶經理來電通報", false)]
    [InlineData("", false)]
    public void NameWarnings_FlagIndividualsOnly(string text, bool warns) =>
        (RcaRules.NameWarnings(text).Count > 0).Should().Be(warns);
}
