using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class SlaCalculatorTests
{
    private static readonly TimeSpan Tw = TimeSpan.FromHours(8);
    private static readonly WorkCalendar NoHolidays = new();

    // 2026-11-13 是星期五；2026-11-16 是下週一
    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, Tw);

    [Fact]
    public void P1_IsReceivedPlus4Hours() =>
        SlaCalculator.Due(TicketType.Incident, TicketPriority.P1, At(13, 14, 30), NoHolidays).Should().Be(At(13, 18, 30));

    [Fact]
    public void P1_IgnoresWeekendsAndHolidays() =>
        // 星期五深夜受理，P1 仍是 +4 小時（跨日不受影響）
        SlaCalculator.Due(TicketType.Incident, TicketPriority.P1, At(13, 22), NoHolidays).Should().Be(At(14, 2));

    [Fact]
    public void P2_IsOneWorkday_SameTimeOfDay() =>
        SlaCalculator.Due(TicketType.Incident, TicketPriority.P2, At(11, 9, 15), NoHolidays).Should().Be(At(12, 9, 15));

    [Fact]
    public void P2_OnFriday_SkipsWeekend() =>
        SlaCalculator.Due(TicketType.Incident, TicketPriority.P2, At(13, 14, 30), NoHolidays).Should().Be(At(16, 14, 30));

    [Fact]
    public void P3_IsTwoWorkdays_SkippingWeekend() =>
        SlaCalculator.Due(TicketType.Incident, TicketPriority.P3, At(13, 10), NoHolidays).Should().Be(At(17, 10));

    [Fact]
    public void P2_ReceivedOnSaturday_CountsFromNextWorkday() =>
        // Excel WORKDAY(週六, 1) = 週一
        SlaCalculator.Due(TicketType.Incident, TicketPriority.P2, At(14, 11), NoHolidays).Should().Be(At(16, 11));

    [Fact]
    public void Holiday_IsSkipped()
    {
        // 11/16（週一）放假：週五受理的 P2 順延到週二
        var calendar = new WorkCalendar([new HolidayEntry(new DateOnly(2026, 11, 16), false)]);
        SlaCalculator.Due(TicketType.Incident, TicketPriority.P2, At(13, 14, 30), calendar).Should().Be(At(17, 14, 30));
    }

    [Fact]
    public void MakeUpWorkday_OnSaturday_Counts()
    {
        // 11/14（週六）補班：週五受理的 P2 於週六到期
        var calendar = new WorkCalendar([new HolidayEntry(new DateOnly(2026, 11, 14), true)]);
        SlaCalculator.Due(TicketType.Incident, TicketPriority.P2, At(13, 14, 30), calendar).Should().Be(At(14, 14, 30));
    }

    [Fact]
    public void ConsecutiveHolidays_AreAllSkipped()
    {
        var calendar = new WorkCalendar(
        [
            new HolidayEntry(new DateOnly(2026, 11, 16), false),
            new HolidayEntry(new DateOnly(2026, 11, 17), false)
        ]);
        SlaCalculator.Due(TicketType.Incident, TicketPriority.P3, At(13, 9), calendar).Should().Be(At(19, 9));
    }

    [Theory]
    [InlineData(TicketType.Consultation)]
    [InlineData(TicketType.DataRequest)]
    public void ConsultationAndDataRequest_AreOneWorkday(TicketType type) =>
        SlaCalculator.Due(type, null, At(13, 9), NoHolidays).Should().Be(At(16, 9));

    [Fact]
    public void ChangeRequest_HasNoDue() =>
        SlaCalculator.Due(TicketType.ChangeRequest, null, At(13, 9), NoHolidays).Should().BeNull();

    [Fact]
    public void Incident_WithoutPriority_HasNoDue() =>
        SlaCalculator.Due(TicketType.Incident, null, At(13, 9), NoHolidays).Should().BeNull();

    [Fact]
    public void DateIsJudgedInTaiwanTime_NotUtc()
    {
        // UTC 週五 17:00 = 台灣週六 01:00：以台灣週六計，P2 → 週一 01:00（台灣）
        var received = new DateTimeOffset(2026, 11, 13, 17, 0, 0, TimeSpan.Zero);
        SlaCalculator.Due(TicketType.Incident, TicketPriority.P2, received, NoHolidays).Should().Be(At(16, 1));
    }

    [Theory]
    [InlineData(TicketPriority.P1, 30)]
    [InlineData(TicketPriority.P2, 120)]
    [InlineData(TicketPriority.P3, 240)]
    public void FirstResponse_ByPriority(TicketPriority priority, int minutes) =>
        SlaCalculator.FirstResponseDue(TicketType.Incident, priority, At(11, 9), NoHolidays).Should().Be(At(11, 9).AddMinutes(minutes));

    [Fact]
    public void FirstResponse_ForConsultation_IsOneWorkday() =>
        SlaCalculator.FirstResponseDue(TicketType.Consultation, null, At(13, 9), NoHolidays).Should().Be(At(16, 9));

    // ===== 燈號 =====

    private static readonly DateTimeOffset Due = At(12, 12);

    [Fact]
    public void Status_NoDue_NotApplicable() =>
        SlaCalculator.Status(null, null, At(12, 10)).Should().Be(SlaStatus.NotApplicable);

    [Fact]
    public void Status_Plenty_OfTime_IsInProgress() =>
        SlaCalculator.Status(Due, null, At(12, 8)).Should().Be(SlaStatus.InProgress);

    [Fact]
    public void Status_Under2Hours_IsDueSoon() =>
        SlaCalculator.Status(Due, null, At(12, 10, 30)).Should().Be(SlaStatus.DueSoon);

    [Fact]
    public void Status_Exactly2Hours_IsStillInProgress() =>
        SlaCalculator.Status(Due, null, At(12, 10)).Should().Be(SlaStatus.InProgress);

    [Fact]
    public void Status_AtDeadline_IsDueSoon_NotOverdue() =>
        SlaCalculator.Status(Due, null, Due).Should().Be(SlaStatus.DueSoon);

    [Fact]
    public void Status_AfterDeadline_IsOverdue() =>
        SlaCalculator.Status(Due, null, Due.AddSeconds(1)).Should().Be(SlaStatus.Overdue);

    [Fact]
    public void Status_ResolvedBeforeDue_MetOnTime() =>
        SlaCalculator.Status(Due, At(12, 11), At(20, 0)).Should().Be(SlaStatus.MetOnTime);

    [Fact]
    public void Status_ResolvedExactlyAtDue_MetOnTime() =>
        SlaCalculator.Status(Due, Due, At(20, 0)).Should().Be(SlaStatus.MetOnTime);

    [Fact]
    public void Status_ResolvedAfterDue_MetLate() =>
        SlaCalculator.Status(Due, Due.AddMinutes(1), At(20, 0)).Should().Be(SlaStatus.MetLate);

    [Fact]
    public void WorkCalendar_AddWorkdays_Zero_ReturnsStart() =>
        NoHolidays.AddWorkdays(new DateOnly(2026, 11, 13), 0).Should().Be(new DateOnly(2026, 11, 13));

    [Fact]
    public void WorkCalendar_Negative_Throws()
    {
        var act = () => NoHolidays.AddWorkdays(new DateOnly(2026, 11, 13), -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
