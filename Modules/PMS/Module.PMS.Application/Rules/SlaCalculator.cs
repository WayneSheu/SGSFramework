using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

/// <summary>假日／補班日條目：<paramref name="IsWorkday"/> 為 false 表示放假，true 表示補班（週末上班）。</summary>
public readonly record struct HolidayEntry(DateOnly Date, bool IsWorkday);

/// <summary>
/// 工作日曆：週一至週五為工作日，國定假日放假、補班日上班。補 SOP 附錄 B-3 公式（WORKDAY）未涵蓋假日的缺口。
/// </summary>
public sealed class WorkCalendar
{
    private readonly Dictionary<DateOnly, bool> _overrides;

    public WorkCalendar(IEnumerable<HolidayEntry>? holidays = null)
    {
        _overrides = (holidays ?? []).GroupBy(h => h.Date).ToDictionary(g => g.Key, g => g.Last().IsWorkday);
    }

    public bool IsWorkday(DateOnly date) =>
        _overrides.TryGetValue(date, out var isWorkday)
            ? isWorkday
            : date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>
    /// 自 <paramref name="start"/> 起加上 <paramref name="workdays"/> 個工作天（等同 Excel WORKDAY(start, n)：
    /// 起算日本身不計，之後第 n 個工作日）。
    /// </summary>
    public DateOnly AddWorkdays(DateOnly start, int workdays)
    {
        if (workdays < 0) throw new ArgumentOutOfRangeException(nameof(workdays));

        var date = start;
        var counted = 0;
        while (counted < workdays)
        {
            date = date.AddDays(1);
            if (IsWorkday(date)) counted++;
        }

        return date;
    }
}

/// <summary>
/// SLA 計算（SOP 6.3、附錄 B-3），純函式。日期與工作日以台灣時間（UTC+8，無日光節約）判定，時分維持受理當下的時間。
/// <list type="bullet">
/// <item>P1：受理 + 4 小時；P2：1 個工作天；P3：2 個工作天</item>
/// <item>諮詢／資料撈取：1 個工作天回覆；CR：待報價確認（無期限）</item>
/// </list>
/// 「工作小時」的首次回應時限（P2 2h、P3 4h）以時鐘小時計，不扣除非上班時段。
/// </summary>
public static class SlaCalculator
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(8);

    /// <summary>「即將到期」門檻：距期限不足 2 小時。</summary>
    public static readonly TimeSpan DueSoonThreshold = TimeSpan.FromHours(2);

    /// <summary>應解決期限；CR 或尚未分級的異常回傳 null（SLA 不適用）。</summary>
    public static DateTimeOffset? Due(TicketType type, TicketPriority? priority, DateTimeOffset receivedAt, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return type switch
        {
            TicketType.ChangeRequest => null,
            TicketType.Incident => priority switch
            {
                TicketPriority.P1 => receivedAt.AddHours(4),
                TicketPriority.P2 => AddWorkdays(receivedAt, 1, calendar),
                TicketPriority.P3 => AddWorkdays(receivedAt, 2, calendar),
                _ => null
            },
            _ => AddWorkdays(receivedAt, 1, calendar)
        };
    }

    /// <summary>首次回應期限：P1 30 分、P2 2 小時、P3 4 小時；諮詢／資料撈取／CR 為 1 個工作天。</summary>
    public static DateTimeOffset FirstResponseDue(TicketType type, TicketPriority? priority, DateTimeOffset receivedAt, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        if (type == TicketType.Incident)
        {
            return priority switch
            {
                TicketPriority.P1 => receivedAt.AddMinutes(30),
                TicketPriority.P2 => receivedAt.AddHours(2),
                _ => receivedAt.AddHours(4)
            };
        }

        return AddWorkdays(receivedAt, 1, calendar);
    }

    /// <summary>
    /// SLA 燈號：無期限＝不適用；已解決＝準時／超時完成；未解決依現在時間判定已超時／即將到期（&lt; 2 小時）／進行中。
    /// </summary>
    public static SlaStatus Status(DateTimeOffset? due, DateTimeOffset? resolvedAt, DateTimeOffset now)
    {
        if (due is not { } deadline) return SlaStatus.NotApplicable;
        if (resolvedAt is { } resolved) return resolved <= deadline ? SlaStatus.MetOnTime : SlaStatus.MetLate;
        if (now > deadline) return SlaStatus.Overdue;
        return deadline - now < DueSoonThreshold ? SlaStatus.DueSoon : SlaStatus.InProgress;
    }

    private static DateTimeOffset AddWorkdays(DateTimeOffset receivedAt, int workdays, WorkCalendar calendar)
    {
        var local = receivedAt.ToOffset(Offset);
        var dueDate = calendar.AddWorkdays(DateOnly.FromDateTime(local.DateTime), workdays);
        return new DateTimeOffset(dueDate.ToDateTime(TimeOnly.FromTimeSpan(local.TimeOfDay)), Offset);
    }
}
