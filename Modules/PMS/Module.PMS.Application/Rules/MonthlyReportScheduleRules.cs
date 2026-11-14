using SGS.Modules.PMS.Application.Services;

namespace SGS.Modules.PMS.Application.Rules;

/// <summary>
/// 月報自動產生的時點規則（SOP 7.2：每月最後一個工作天結算），純函式；日期以台灣時間（UTC+8）判定。
/// <list type="bullet">
/// <item>月底最後一個工作日 → 產生當月月報。</item>
/// <item>補產：每月前 <see cref="CatchUpWorkdays"/> 個工作日 → 檢查上月月報，缺的補產（排程當天遇停機或失敗時的保險）。</item>
/// </list>
/// 作業每天執行，由此規則決定當天是否為產生日，因此不受 cron 無法表達「最後工作日」的限制，也不必在國定假日調整 cron。
/// </summary>
public static class MonthlyReportSchedule
{
    public const int CatchUpWorkdays = 3;

    /// <summary>該月第一天到最後一天，供載入完整的工作日曆。</summary>
    public static (DateOnly From, DateOnly To) MonthRange(DateOnly date) =>
        (new DateOnly(date.Year, date.Month, 1), new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)));

    public static bool IsLastWorkdayOfMonth(DateOnly date, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        if (!calendar.IsWorkday(date)) return false;

        var last = DateTime.DaysInMonth(date.Year, date.Month);
        for (var day = date.Day + 1; day <= last; day++)
            if (calendar.IsWorkday(new DateOnly(date.Year, date.Month, day))) return false;

        return true;
    }

    /// <summary>當月第幾個工作日（從 1 起算）；當天不是工作日回傳 0。</summary>
    public static int WorkdayIndexInMonth(DateOnly date, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        if (!calendar.IsWorkday(date)) return 0;

        var index = 0;
        for (var day = 1; day <= date.Day; day++)
            if (calendar.IsWorkday(new DateOnly(date.Year, date.Month, day))) index++;

        return index;
    }

    /// <summary>今天應自動產生的月份清單（YYYY-MM）：月底最後工作日 → 當月；月初前 3 個工作日 → 上月（補產）。</summary>
    public static IReadOnlyList<string> TargetPeriods(DateTimeOffset now, WorkCalendar calendar)
    {
        var today = DateOnly.FromDateTime(now.ToOffset(SlaCalculator.Offset).DateTime);
        var periods = new List<string>();

        if (IsLastWorkdayOfMonth(today, calendar)) periods.Add(Format(today));

        var index = WorkdayIndexInMonth(today, calendar);
        if (index is >= 1 and <= CatchUpWorkdays) periods.Add(Format(today.AddMonths(-1)));

        return periods;
    }

    private static string Format(DateOnly date) => $"{date.Year:0000}-{date.Month:00}";
}
