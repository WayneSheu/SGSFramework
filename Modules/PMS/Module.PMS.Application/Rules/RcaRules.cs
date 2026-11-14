using System.Text.RegularExpressions;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

/// <summary>RCA 時間軸的一筆事件。</summary>
public sealed record RcaTimelineItem(DateTimeOffset At, string Event);

/// <summary>
/// RCA 規則（SOP 7.3），純函式：提交時限＝系統恢復後 48 小時；觸發條件須與問題單相符；
/// 內文一律以團隊為主詞，疑似點名個人僅警示不阻擋。
/// </summary>
public static partial class RcaRules
{
    public const int DueHours = 48;
    public const int MaxTimelineItems = 50;

    public static DateTimeOffset Due(DateTimeOffset recoveredAt) => recoveredAt.AddHours(DueHours);

    public static bool IsOverdue(RcaStatus status, DateTimeOffset dueAt, DateTimeOffset? submittedAt, DateTimeOffset now) =>
        status == RcaStatus.Draft ? now > dueAt : submittedAt is { } at && at > dueAt;

    /// <summary>此問題單目前可選的觸發條件：僅「系統異常」且已分流；P1 → P1；SLA 已超時／超時完成 → SlaBreach；Downgrade 為人工判斷（插件降級／回滾）。</summary>
    public static IReadOnlyList<RcaTrigger> EligibleTriggers(TicketType type, TicketStatus status, TicketPriority? priority, SlaStatus sla)
    {
        if (type != TicketType.Incident || status is TicketStatus.Received or TicketStatus.Cancelled) return [];

        var list = new List<RcaTrigger>();
        if (priority == TicketPriority.P1) list.Add(RcaTrigger.P1);
        if (sla is SlaStatus.Overdue or SlaStatus.MetLate) list.Add(RcaTrigger.SlaBreach);
        list.Add(RcaTrigger.Downgrade);
        return list;
    }

    /// <summary>存草稿的最低要求：觸發條件有效、恢復時間、欄位長度、時間軸格式。其餘欄位提交時才要求完整。</summary>
    public static string? ValidateDraft(
        RcaTrigger trigger, DateTimeOffset recoveredAt, string? summary, string? rootCause, string? shortTerm, string? longTerm,
        IReadOnlyList<RcaTimelineItem>? timeline)
    {
        if (!Enum.IsDefined(trigger)) return "觸發條件不正確。";
        if (recoveredAt == default) return "系統恢復時間為必填（提交期限自此起算 48 小時）。";
        if (summary is { Length: > 2000 }) return "事件摘要不得超過 2000 字。";
        if (rootCause is { Length: > 4000 }) return "技術根因不得超過 4000 字。";
        if (shortTerm is { Length: > 2000 }) return "短期止血措施不得超過 2000 字。";
        if (longTerm is { Length: > 2000 }) return "長期預防措施不得超過 2000 字。";

        if (timeline is not null)
        {
            if (timeline.Count > MaxTimelineItems) return $"時間軸最多 {MaxTimelineItems} 筆。";
            foreach (var item in timeline)
            {
                if (item.At == default) return "時間軸每筆事件都需要時間。";
                if (string.IsNullOrWhiteSpace(item.Event)) return "時間軸每筆事件都需要說明。";
                if (item.Event.Length > 500) return "時間軸單筆事件不得超過 500 字。";
            }
        }

        return null;
    }

    /// <summary>提交的完整性要求（SOP 7.3 必要內容）：事件摘要、精確時間軸（至少 2 筆）、根本原因、短期止血與長期預防。</summary>
    public static string? ValidateForSubmit(string summary, string rootCause, string shortTerm, string longTerm, IReadOnlyList<RcaTimelineItem> timeline)
    {
        if (string.IsNullOrWhiteSpace(summary)) return "事件摘要為必填。";
        if (timeline.Count < 2) return "精確時間軸至少需要 2 筆（發生、恢復）。";
        if (string.IsNullOrWhiteSpace(rootCause)) return "技術根因為必填。";
        if (string.IsNullOrWhiteSpace(shortTerm)) return "短期止血措施為必填。";
        if (string.IsNullOrWhiteSpace(longTerm)) return "長期預防措施為必填。";
        return null;
    }

    /// <summary>檢查是否疑似點名個人（撰寫原則：一律以團隊為主詞）。僅警示，不阻擋。</summary>
    public static IReadOnlyList<string> NameWarnings(params string?[] texts)
    {
        var all = string.Join('\n', texts.Where(t => !string.IsNullOrWhiteSpace(t)));
        if (all.Length == 0) return [];

        foreach (Match m in PersonRegex().Matches(all))
        {
            var name = m.Groups["name"].Value;
            // 候選詞含「前端、後端、資深…」等職稱用語（如「與前端工程師」）視為泛稱，不是人名
            if (!GenericPrefixes.Any(p => name.Contains(p, StringComparison.Ordinal)))
                return ["內文疑似點名個人，請改以團隊為主詞（例：「維運團隊」「開發團隊」）。"];
        }

        return [];
    }

    private static readonly string[] GenericPrefixes =
    [
        "前端", "後端", "資深", "初階", "值班", "開發", "測試", "維運", "系統", "資料庫", "網管", "專案", "相關", "負責", "該", "本", "一位", "某位", "這位", "那位", "原", "其", "我方", "客戶"
    ];

    [GeneratedRegex(@"(?<name>[一-龥]{2,4})(?:工程師|同事|同仁|先生|小姐|經理|主管)")]
    private static partial Regex PersonRegex();
}
