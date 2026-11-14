using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class MonthlyReportScheduleTests
{
    private static readonly TimeSpan Tw = TimeSpan.FromHours(8);

    // 2026-11：11/1 週日、11/30 週一；2026-10 最後一天 10/31 週六
    private static DateTimeOffset At(int year, int month, int day, int hour = 18) => new(year, month, day, hour, 0, 0, Tw);

    private static readonly WorkCalendar Plain = new();

    [Theory]
    [InlineData(2026, 11, 30, true)]   // 週一，月底最後工作日
    [InlineData(2026, 11, 27, false)]  // 週五，後面還有 11/30
    [InlineData(2026, 10, 30, true)]   // 週五；10/31 週六不是工作日
    [InlineData(2026, 10, 31, false)]  // 週六本身不是工作日
    [InlineData(2026, 11, 29, false)]  // 週日
    public void LastWorkday_Plain(int y, int m, int d, bool expected) =>
        MonthlyReportSchedule.IsLastWorkdayOfMonth(new DateOnly(y, m, d), Plain).Should().Be(expected);

    [Fact]
    public void LastWorkday_HolidayAtMonthEnd_ShiftsEarlier()
    {
        // 11/30（週一）放假 → 最後工作日變成 11/27（週五）
        var calendar = new WorkCalendar([new HolidayEntry(new DateOnly(2026, 11, 30), false)]);
        MonthlyReportSchedule.IsLastWorkdayOfMonth(new DateOnly(2026, 11, 27), calendar).Should().BeTrue();
        MonthlyReportSchedule.IsLastWorkdayOfMonth(new DateOnly(2026, 11, 30), calendar).Should().BeFalse();
    }

    [Fact]
    public void LastWorkday_MakeupSaturdayCounts()
    {
        // 10/31（週六）補班 → 它才是 10 月最後工作日
        var calendar = new WorkCalendar([new HolidayEntry(new DateOnly(2026, 10, 31), true)]);
        MonthlyReportSchedule.IsLastWorkdayOfMonth(new DateOnly(2026, 10, 31), calendar).Should().BeTrue();
        MonthlyReportSchedule.IsLastWorkdayOfMonth(new DateOnly(2026, 10, 30), calendar).Should().BeFalse();
    }

    [Fact]
    public void WorkdayIndex_CountsFromFirstOfMonth_SkippingWeekendsAndHolidays()
    {
        // 2026-11：11/2 週一=1、11/3=2、11/4=3；11/5 若放假則 11/6=4
        var calendar = new WorkCalendar([new HolidayEntry(new DateOnly(2026, 11, 5), false)]);
        MonthlyReportSchedule.WorkdayIndexInMonth(new DateOnly(2026, 11, 1), calendar).Should().Be(0);  // 週日
        MonthlyReportSchedule.WorkdayIndexInMonth(new DateOnly(2026, 11, 2), calendar).Should().Be(1);
        MonthlyReportSchedule.WorkdayIndexInMonth(new DateOnly(2026, 11, 4), calendar).Should().Be(3);
        MonthlyReportSchedule.WorkdayIndexInMonth(new DateOnly(2026, 11, 5), calendar).Should().Be(0);  // 放假
        MonthlyReportSchedule.WorkdayIndexInMonth(new DateOnly(2026, 11, 6), calendar).Should().Be(4);
    }

    [Fact]
    public void Target_LastWorkday_IsCurrentMonth() =>
        MonthlyReportSchedule.TargetPeriods(At(2026, 11, 30), Plain).Should().Equal("2026-11");

    [Fact]
    public void Target_FirstThreeWorkdays_CatchUpPreviousMonth()
    {
        // 12/1 週二=第 1、12/2=第 2、12/3=第 3 → 補產 11 月；12/4=第 4 不再補
        MonthlyReportSchedule.TargetPeriods(At(2026, 12, 1), Plain).Should().Equal("2026-11");
        MonthlyReportSchedule.TargetPeriods(At(2026, 12, 3), Plain).Should().Equal("2026-11");
        MonthlyReportSchedule.TargetPeriods(At(2026, 12, 4), Plain).Should().BeEmpty();
    }

    [Fact]
    public void Target_MidMonth_AndNonWorkday_AreEmpty()
    {
        MonthlyReportSchedule.TargetPeriods(At(2026, 11, 17), Plain).Should().BeEmpty();
        MonthlyReportSchedule.TargetPeriods(At(2026, 11, 28), Plain).Should().BeEmpty(); // 週六
    }

    [Fact]
    public void Target_YearBoundary_PreviousMonthIsDecember()
    {
        // 2027-01-01 週五是第 1 個工作日 → 補產 2026-12
        MonthlyReportSchedule.TargetPeriods(At(2027, 1, 1), Plain).Should().Equal("2026-12");
    }

    [Fact]
    public void Target_UsesTaiwanDate_NotUtc()
    {
        // UTC 11/30 17:00 = 台灣 12/1 01:00（週二第 1 個工作日）→ 補產 11 月，而不是把它當成 11/30 最後工作日
        var utc = new DateTimeOffset(2026, 11, 30, 17, 0, 0, TimeSpan.Zero);
        MonthlyReportSchedule.TargetPeriods(utc, Plain).Should().Equal("2026-11");
    }

    [Fact]
    public void MonthRange_CoversWholeMonth()
    {
        var (from, to) = MonthlyReportSchedule.MonthRange(new DateOnly(2026, 2, 14));
        from.Should().Be(new DateOnly(2026, 2, 1));
        to.Should().Be(new DateOnly(2026, 2, 28));
        MonthlyReportSchedule.MonthRange(new DateOnly(2028, 2, 1)).To.Should().Be(new DateOnly(2028, 2, 29));
    }
}
