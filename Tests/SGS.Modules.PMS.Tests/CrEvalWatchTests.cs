using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class CrEvalWatchTests
{
    // 台灣時間 2026-11-17 09:00 = UTC 01:00
    private static readonly DateTimeOffset Due = new(2026, 11, 17, 9, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void NotOverdue_NoReminder() =>
        CrEvalWatch.ShouldRemind(CrStatus.Received, Due, null, Due.AddMinutes(-1)).Should().BeFalse();

    [Fact]
    public void Overdue_Received_FirstReminder() =>
        CrEvalWatch.ShouldRemind(CrStatus.Received, Due, null, Due.AddHours(1)).Should().BeTrue();

    [Theory]
    [InlineData(CrStatus.PendingDecision)]
    [InlineData(CrStatus.PendingSign)]
    [InlineData(CrStatus.Approved)]
    [InlineData(CrStatus.Rejected)]
    [InlineData(CrStatus.Deferred)]
    [InlineData(CrStatus.Cancelled)]
    public void OnlyReceivedIsWatched(CrStatus status) =>
        CrEvalWatch.ShouldRemind(status, Due, null, Due.AddDays(3)).Should().BeFalse();

    [Fact]
    public void SameTaiwanDay_NoSecondReminder()
    {
        var now = new DateTimeOffset(2026, 11, 18, 17, 0, 0, TimeSpan.FromHours(8));
        var last = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(8));
        CrEvalWatch.ShouldRemind(CrStatus.Received, Due, last, now).Should().BeFalse();
    }

    [Fact]
    public void NextTaiwanDay_RemindsAgain()
    {
        var now = new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.FromHours(8));
        var last = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(8));
        CrEvalWatch.ShouldRemind(CrStatus.Received, Due, last, now).Should().BeTrue();
    }

    [Fact]
    public void DayBoundary_UsesTaiwanTimeNotUtc()
    {
        // 台灣 11/18 23:30（UTC 15:30）與台灣 11/19 00:30（UTC 16:30，仍同為 UTC 11/18）是不同的台灣日
        var last = new DateTimeOffset(2026, 11, 18, 23, 30, 0, TimeSpan.FromHours(8));
        var now = new DateTimeOffset(2026, 11, 19, 0, 30, 0, TimeSpan.FromHours(8)).ToUniversalTime();
        CrEvalWatch.ShouldRemind(CrStatus.Received, Due, last, now).Should().BeTrue();
    }

    [Fact]
    public void Workday_WeekendAndHoliday_Skipped()
    {
        var calendar = new WorkCalendar([new HolidayEntry(new DateOnly(2026, 11, 18), false)]);
        // 2026-11-14 週六、2026-11-18 國定假日（測試資料）、2026-11-19 週四
        CrEvalWatch.IsWorkdayToday(new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.FromHours(8)), calendar).Should().BeFalse();
        CrEvalWatch.IsWorkdayToday(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(8)), calendar).Should().BeFalse();
        CrEvalWatch.IsWorkdayToday(new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.FromHours(8)), calendar).Should().BeTrue();
    }

    [Fact]
    public void Message_ListsCrsOldestFirst_AndEncodesHtml()
    {
        var now = Due.AddDays(2);
        var items = new[]
        {
            new CrEvalOverdueItem("RAG-CR-002", "新增<script>報表", CrKind.Internal, Due.AddHours(5), Due.AddDays(-2)),
            new CrEvalOverdueItem("RAG-CR-001", "匯出 Excel", CrKind.Customer, Due, Due.AddDays(-2))
        };

        var body = CrEvalWatchMessage.Body("RAG", "增生檢索", items, now);

        body.Should().NotContain("<script>");
        body.Should().Contain("&lt;script&gt;");
        body.IndexOf("RAG-CR-001", StringComparison.Ordinal).Should().BeLessThan(body.IndexOf("RAG-CR-002", StringComparison.Ordinal));
        body.Should().Contain("客戶 CR").And.Contain("內部 CR").And.Contain("已逾 2 天");
        CrEvalWatchMessage.Subject("RAG", 2).Should().Be("RAG 有 2 張 CR 影響評估已逾期");
    }

    [Fact]
    public void Message_LessThanOneDay_ShowsHours()
    {
        var items = new[] { new CrEvalOverdueItem("RAG-CR-001", "t", CrKind.Customer, Due, Due.AddDays(-2)) };
        CrEvalWatchMessage.Body("RAG", "n", items, Due.AddHours(3)).Should().Contain("已逾 3 小時");
    }
}
