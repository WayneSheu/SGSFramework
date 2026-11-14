using System.Net;
using System.Text;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

/// <summary>
/// CR 評估逾期提醒規則（SOP 4.4：受理後 2 個工作天內完成影響評估），純函式。
/// 條件：仍在「受理待評估」且已逾期限；同一張 CR 每個台灣日（UTC+8）最多提醒一次，
/// 直到有人評估、取消或（暫緩後）重啟重新起算期限為止。
/// </summary>
public static class CrEvalWatch
{
    public static bool ShouldRemind(CrStatus status, DateTimeOffset evalDueAt, DateTimeOffset? lastReminderAt, DateTimeOffset now)
    {
        if (status != CrStatus.Received) return false;
        if (now <= evalDueAt) return false;
        if (lastReminderAt is null) return true;

        return LocalDate(lastReminderAt.Value) < LocalDate(now);
    }

    /// <summary>今天是否為工作日（週一至週五、扣除國定假日、含補班）。非工作日不發提醒，避免假日打擾。</summary>
    public static bool IsWorkdayToday(DateTimeOffset now, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return calendar.IsWorkday(LocalDate(now));
    }

    private static DateOnly LocalDate(DateTimeOffset value) => DateOnly.FromDateTime(value.ToOffset(SlaCalculator.Offset).DateTime);
}

/// <summary>逾期提醒信中的一筆 CR。</summary>
public sealed record CrEvalOverdueItem(string CrNo, string Title, CrKind Kind, DateTimeOffset EvalDueAt, DateTimeOffset ReceivedAt);

/// <summary>組出提醒信文字（主旨一行摘要＋純文字內容；內容經 HTML 編碼，因為郵件範本以 HTML 呈現）。</summary>
public static class CrEvalWatchMessage
{
    public static string Subject(string projectCode, int count) =>
        $"{projectCode} 有 {count} 張 CR 影響評估已逾期";

    public static string Body(string projectCode, string projectName, IReadOnlyList<CrEvalOverdueItem> items, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(items);

        var sb = new StringBuilder();
        sb.Append($"專案 {Encode(projectCode)} {Encode(projectName)} 有 {items.Count} 張 CR 的影響評估已超過受理後 2 個工作天的期限，請 Tech Lead 儘速評估，PM 請追蹤。<br/>");
        foreach (var item in items.OrderBy(i => i.EvalDueAt))
        {
            var overdue = now - item.EvalDueAt;
            var days = (int)Math.Floor(overdue.TotalDays);
            var late = days >= 1 ? $"已逾 {days} 天" : $"已逾 {Math.Max(1, (int)Math.Floor(overdue.TotalHours))} 小時";
            sb.Append($"<br/>・{Encode(item.CrNo)}（{(item.Kind == CrKind.Customer ? "客戶 CR" : "內部 CR")}）{Encode(item.Title)}；期限 {Format(item.EvalDueAt)}，{late}");
        }

        sb.Append("<br/><br/>評估完成、取消，或暫緩後重啟前，系統會在每個工作日再提醒一次。");
        return sb.ToString();
    }

    private static string Format(DateTimeOffset value) => value.ToOffset(SlaCalculator.Offset).ToString("yyyy-MM-dd HH:mm");

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
