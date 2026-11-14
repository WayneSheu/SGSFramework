using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Application.Services;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class MonthlyStatsTests
{
    private static readonly TimeSpan Tw = TimeSpan.FromHours(8);
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, Tw);
    private static readonly DateTimeOffset End = new(2026, 12, 1, 0, 0, 0, Tw);

    private static DateTimeOffset T(int day, int hour = 9) => new(2026, 11, day, hour, 0, 0, Tw);

    [Theory]
    [InlineData("2026-11", true)]
    [InlineData("2026-1", false)]
    [InlineData("2026/11", false)]
    [InlineData("2026-13", false)]
    [InlineData("2026-00", false)]
    [InlineData("1999-12", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ParsePeriod(string? period, bool ok) =>
        MonthlyStatsCalculator.TryParsePeriod(period, out _, out _).Should().Be(ok);

    [Fact]
    public void ParsePeriod_BoundariesAreTaiwanMonth()
    {
        MonthlyStatsCalculator.TryParsePeriod("2026-11", out var start, out var end).Should().BeTrue();
        start.Should().Be(Start);
        end.Should().Be(End);
        start.Offset.Should().Be(Tw);

        // 12 月接 1 月跨年
        MonthlyStatsCalculator.TryParsePeriod("2026-12", out _, out var yearEnd).Should().BeTrue();
        yearEnd.Should().Be(new DateTimeOffset(2027, 1, 1, 0, 0, 0, Tw));
    }

    [Fact]
    public void FormatPeriod_UsesTaiwanTime()
    {
        // UTC 11/30 17:00 = 台灣 12/1 01:00 → 屬 12 月
        MonthlyStatsCalculator.FormatPeriod(new DateTimeOffset(2026, 11, 30, 17, 0, 0, TimeSpan.Zero)).Should().Be("2026-12");
        MonthlyStatsCalculator.FormatPeriod(new DateTimeOffset(2026, 11, 30, 15, 59, 0, TimeSpan.Zero)).Should().Be("2026-11");
    }

    [Fact]
    public void Empty_AllZero_RatesNull_TypesAllListed()
    {
        var stats = MonthlyStatsCalculator.Compute("2026-11", [], Start, End, End.AddDays(1));
        stats.TotalIncoming.Should().Be(0);
        stats.SlaOnTimeRate.Should().BeNull();
        stats.MttrHours.Should().BeNull();
        stats.TypeRatio.Should().HaveCount(4).And.OnlyContain(t => t.Count == 0);
        stats.OverdueCount.Should().Be(0);
    }

    [Fact]
    public void TotalIncoming_OnlyThisMonth_ExcludesCancelledAndOtherMonths()
    {
        TicketFact[] facts =
        [
            new(TicketType.Incident, TicketStatus.Closed, T(3), null, T(4)),
            new(TicketType.Consultation, TicketStatus.Received, T(20), null, null),
            new(TicketType.Incident, TicketStatus.Cancelled, T(5), null, null),
            new(TicketType.Incident, TicketStatus.Closed, new DateTimeOffset(2026, 10, 31, 23, 59, 0, Tw), null, null),
            new(TicketType.Incident, TicketStatus.Received, new DateTimeOffset(2026, 12, 1, 0, 0, 0, Tw), null, null)
        ];

        var stats = MonthlyStatsCalculator.Compute("2026-11", facts, Start, End, End.AddDays(1));
        stats.TotalIncoming.Should().Be(2);
    }

    [Fact]
    public void SlaRate_OnTimeLateOverdue_InProgressExcluded()
    {
        var now = T(25);
        TicketFact[] facts =
        [
            new(TicketType.Incident, TicketStatus.Closed, T(2), T(4), T(3)),     // 準時
            new(TicketType.Incident, TicketStatus.Closed, T(2), T(4), T(6)),     // 超時完成
            new(TicketType.Incident, TicketStatus.InProgress, T(2), T(5), null), // 已超時未結
            new(TicketType.Incident, TicketStatus.InProgress, T(24), T(28), null), // 進行中（不計）
            new(TicketType.ChangeRequest, TicketStatus.InProgress, T(3), null, null) // 無期限（不計）
        ];

        var stats = MonthlyStatsCalculator.Compute("2026-11", facts, Start, End, now);
        stats.SlaApplicableCount.Should().Be(3);
        stats.SlaMetOnTimeCount.Should().Be(1);
        stats.SlaOnTimeRate.Should().Be(0.3333m);
    }

    [Fact]
    public void SlaRate_AllOnTime_IsOne()
    {
        TicketFact[] facts = [new(TicketType.Incident, TicketStatus.Closed, T(2), T(4), T(3))];
        MonthlyStatsCalculator.Compute("2026-11", facts, Start, End, End.AddDays(1)).SlaOnTimeRate.Should().Be(1m);
    }

    [Fact]
    public void Mttr_AveragesIncidentsResolvedThisMonth_ByResolveTime()
    {
        TicketFact[] facts =
        [
            new(TicketType.Incident, TicketStatus.Closed, T(2, 9), null, T(2, 15)),  // 6h
            new(TicketType.Incident, TicketStatus.Closed, T(3, 9), null, T(3, 13)),  // 4h
            // 10 月受理、11 月修復：計入 11 月 MTTR（以修復時間歸月）
            new(TicketType.Incident, TicketStatus.Closed, new DateTimeOffset(2026, 10, 31, 20, 0, 0, Tw), null, new DateTimeOffset(2026, 11, 1, 2, 0, 0, Tw)), // 6h
            new(TicketType.Consultation, TicketStatus.Closed, T(4), null, T(4, 20)),  // 非異常，不計
            new(TicketType.Incident, TicketStatus.InProgress, T(5), null, null)       // 未修復，不計
        ];

        MonthlyStatsCalculator.Compute("2026-11", facts, Start, End, End.AddDays(1)).MttrHours.Should().Be(5.33m);
    }

    [Fact]
    public void TypeRatio_CountsIncomingByType()
    {
        TicketFact[] facts =
        [
            new(TicketType.Incident, TicketStatus.Received, T(2), null, null),
            new(TicketType.Incident, TicketStatus.Received, T(3), null, null),
            new(TicketType.ChangeRequest, TicketStatus.Received, T(4), null, null),
            new(TicketType.DataRequest, TicketStatus.Received, T(5), null, null)
        ];

        var ratio = MonthlyStatsCalculator.Compute("2026-11", facts, Start, End, End.AddDays(1)).TypeRatio.ToDictionary(t => t.Type, t => t.Count);
        ratio[TicketType.Incident].Should().Be(2);
        ratio[TicketType.ChangeRequest].Should().Be(1);
        ratio[TicketType.DataRequest].Should().Be(1);
        ratio[TicketType.Consultation].Should().Be(0);
    }

    [Fact]
    public void OverdueAndDueSoon_AreAsOfNowForCurrentMonth()
    {
        var now = T(15, 10);
        TicketFact[] facts =
        [
            new(TicketType.Incident, TicketStatus.InProgress, T(10), T(14), null),            // 已逾期
            new(TicketType.Incident, TicketStatus.InProgress, T(14), T(15, 11), null),        // 1 小時後到期
            new(TicketType.Incident, TicketStatus.InProgress, T(14), T(15, 20), null),        // 10 小時後到期（不算即將）
            new(TicketType.Incident, TicketStatus.Closed, T(10), T(11), T(12))                // 已結案
        ];

        var stats = MonthlyStatsCalculator.Compute("2026-11", facts, Start, End, now);
        stats.AsOf.Should().Be(now);
        stats.OverdueCount.Should().Be(1);
        stats.DueSoonCount.Should().Be(1);
    }

    [Fact]
    public void PastMonth_UsesEndOfMonthAsBaseline_ResolvedAfterMonthEndStillCountsAsOpen()
    {
        // 11 月底仍未結（12/5 才解決）：以月底為基準，視為「已逾期未結」，且不計入 11 月 MTTR
        TicketFact[] facts =
        [
            new(TicketType.Incident, TicketStatus.Closed, T(20), T(22), new DateTimeOffset(2026, 12, 5, 9, 0, 0, Tw))
        ];

        var stats = MonthlyStatsCalculator.Compute("2026-11", facts, Start, End, new DateTimeOffset(2026, 12, 20, 0, 0, 0, Tw));
        stats.AsOf.Should().Be(End);
        stats.OverdueCount.Should().Be(1);
        stats.SlaOnTimeRate.Should().Be(0m);
        stats.MttrHours.Should().BeNull();
    }

    [Fact]
    public void Pdf_RendersNonEmptyPdf()
    {
        var stats = MonthlyStatsCalculator.Compute("2026-11",
            [new TicketFact(TicketType.Incident, TicketStatus.Closed, T(2), T(4), T(3))], Start, End, End.AddDays(1));

        var pdf = MonthlyReportPdf.Render("RAG", "增生檢索", stats, 1, End);
        pdf.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }
}
