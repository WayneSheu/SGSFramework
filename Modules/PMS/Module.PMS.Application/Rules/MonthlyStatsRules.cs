using System.Globalization;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

/// <summary>統計用的問題單事實（只含計算所需欄位）。</summary>
public sealed record TicketFact(TicketType Type, TicketStatus Status, DateTimeOffset ReceivedAt, DateTimeOffset? DueAt, DateTimeOffset? ResolvedAt);

public sealed record TypeCount(TicketType Type, int Count);

/// <summary>月度統計結果（SOP 7.2：總進線單數、SLA 準時達成率、MTTR、問題類型佔比，另附逾期與即將到期數）。</summary>
public sealed record MonthlyStats(
    string Period, DateTimeOffset AsOf, int TotalIncoming, int SlaApplicableCount, int SlaMetOnTimeCount,
    decimal? SlaOnTimeRate, decimal? MttrHours, IReadOnlyList<TypeCount> TypeRatio, int OverdueCount, int DueSoonCount);

/// <summary>
/// 月度統計規則（純函式）。月份邊界以台灣時間（UTC+8）判定。
/// <list type="bullet">
/// <item>總進線單數：當月受理（不含已取消）。</item>
/// <item>SLA 準時達成率：當月受理且有 SLA 期限的單，以「準時完成／（準時完成＋超時完成＋已超時未結）」計；進行中者不計入分母。</item>
/// <item>MTTR：當月修復完成（<c>ResolvedAt</c> 落在當月）的系統異常單，受理到修復的平均小時數。</item>
/// <item>類型佔比：當月受理單依類型計數（四種類型皆列出）。</item>
/// <item>逾期／即將到期：統計基準時點（當月以現在，過去月份以月底）仍未結案且 SLA 已逾期／2 小時內到期的單。</item>
/// </list>
/// </summary>
public static class MonthlyStatsCalculator
{
    public static bool TryParsePeriod(string? period, out DateTimeOffset start, out DateTimeOffset endExclusive)
    {
        start = default;
        endExclusive = default;

        if (string.IsNullOrWhiteSpace(period) || period.Length != 7 || period[4] != '-') return false;
        if (!int.TryParse(period.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(period.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
            year is < 2000 or > 2999 || month is < 1 or > 12)
            return false;

        start = new DateTimeOffset(year, month, 1, 0, 0, 0, SlaCalculator.Offset);
        endExclusive = start.AddMonths(1);
        return true;
    }

    public static string FormatPeriod(DateTimeOffset value)
    {
        var local = value.ToOffset(SlaCalculator.Offset);
        return $"{local.Year:0000}-{local.Month:00}";
    }

    public static MonthlyStats Compute(string period, IReadOnlyCollection<TicketFact> facts, DateTimeOffset start, DateTimeOffset endExclusive, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var asOf = now < endExclusive ? now : endExclusive;
        var live = facts.Where(f => f.Status != TicketStatus.Cancelled && f.ReceivedAt < endExclusive).ToList();

        var incoming = live.Where(f => f.ReceivedAt >= start).ToList();

        // SLA：解決時間只認統計基準時點之前者（過去月份的月底後才解決視為當時仍未結）
        var sla = incoming.Where(f => f.DueAt is not null)
            .Select(f => SlaCalculator.Status(f.DueAt, EffectiveResolved(f, asOf), asOf))
            .ToList();
        var onTime = sla.Count(s => s == SlaStatus.MetOnTime);
        var late = sla.Count(s => s == SlaStatus.MetLate);
        var overdueOpen = sla.Count(s => s == SlaStatus.Overdue);
        var denominator = onTime + late + overdueOpen;
        decimal? rate = denominator == 0 ? null : Math.Round((decimal)onTime / denominator, 4, MidpointRounding.AwayFromZero);

        var repaired = live.Where(f => f.Type == TicketType.Incident && f.ResolvedAt is { } r && r >= start && r < endExclusive).ToList();
        decimal? mttr = repaired.Count == 0
            ? null
            : Math.Round((decimal)repaired.Average(f => (f.ResolvedAt!.Value - f.ReceivedAt).TotalHours), 2, MidpointRounding.AwayFromZero);

        var typeRatio = Enum.GetValues<TicketType>().Select(t => new TypeCount(t, incoming.Count(f => f.Type == t))).ToList();

        var openWithDue = live.Where(f => f.DueAt is not null && EffectiveResolved(f, asOf) is null).ToList();
        var overdue = openWithDue.Count(f => f.DueAt!.Value < asOf);
        var dueSoon = openWithDue.Count(f => f.DueAt!.Value >= asOf && f.DueAt!.Value - asOf < SlaCalculator.DueSoonThreshold);

        return new MonthlyStats(period, asOf, incoming.Count, denominator, onTime, rate, mttr, typeRatio, overdue, dueSoon);
    }

    private static DateTimeOffset? EffectiveResolved(TicketFact f, DateTimeOffset asOf) =>
        f.ResolvedAt is { } r && r <= asOf ? r : null;
}
