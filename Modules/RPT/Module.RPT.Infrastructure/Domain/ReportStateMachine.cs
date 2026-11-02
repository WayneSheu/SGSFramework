using SGSFramework.Core.Abstractions.Entities.ReportCategories;

namespace SGS.Modules.RPT.Infrastructure.Domain;

/// <summary>報告生命週期轉換</summary>
public enum ReportTransition
{
    /// <summary>分析員送審（Draft → InReview）</summary>
    Submit,

    /// <summary>審核核可（InReview → Reviewed）</summary>
    ApproveReview,

    /// <summary>審核退回（InReview → Draft）</summary>
    RejectReview,

    /// <summary>簽署發行（Reviewed → Published）</summary>
    Sign,

    /// <summary>簽署人退回（Reviewed → Draft）</summary>
    RejectSign,

    /// <summary>作廢（Published → Voided，經作廢申請核准）</summary>
    Void
}

/// <summary>
/// 報告狀態機（純靜態表）：定義每個轉換的起訖狀態、所需授權動作，以及是否必填原因。
/// 人員是否可執行由 IReportAccessAuthorizationService 依所需動作判定。
/// </summary>
public static class ReportStateMachine
{
    public sealed record TransitionRule(
        ReportLifecycleStatus From,
        ReportLifecycleStatus To,
        ReportActions RequiredAction,
        bool RequiresReason);

    private static readonly IReadOnlyDictionary<ReportTransition, TransitionRule> Rules = new Dictionary<ReportTransition, TransitionRule>
    {
        [ReportTransition.Submit] = new(ReportLifecycleStatus.Draft, ReportLifecycleStatus.InReview, ReportActions.Edit, false),
        [ReportTransition.ApproveReview] = new(ReportLifecycleStatus.InReview, ReportLifecycleStatus.Reviewed, ReportActions.Review, false),
        [ReportTransition.RejectReview] = new(ReportLifecycleStatus.InReview, ReportLifecycleStatus.Draft, ReportActions.Review, true),
        [ReportTransition.Sign] = new(ReportLifecycleStatus.Reviewed, ReportLifecycleStatus.Published, ReportActions.Sign, false),
        [ReportTransition.RejectSign] = new(ReportLifecycleStatus.Reviewed, ReportLifecycleStatus.Draft, ReportActions.Sign, true),
        [ReportTransition.Void] = new(ReportLifecycleStatus.Published, ReportLifecycleStatus.Voided, ReportActions.Void, true)
    };

    public static TransitionRule RuleFor(ReportTransition transition) => Rules[transition];

    /// <summary>檢核轉換是否適用於目前狀態；回傳錯誤訊息，可轉換時回傳 null</summary>
    public static string? Validate(ReportLifecycleStatus current, ReportTransition transition, string? reason)
    {
        var rule = Rules[transition];
        if (current != rule.From)
        {
            return $"報告目前狀態為「{StatusLabel(current)}」，無法執行「{TransitionLabel(transition)}」。";
        }

        if (rule.RequiresReason && string.IsNullOrWhiteSpace(reason))
        {
            return $"「{TransitionLabel(transition)}」須填寫原因。";
        }

        return null;
    }

    /// <summary>目前狀態可進行的轉換（供前端顯示可用操作）</summary>
    public static IEnumerable<ReportTransition> AvailableFrom(ReportLifecycleStatus current) =>
        Rules.Where(r => r.Value.From == current).Select(r => r.Key);

    public static string StatusLabel(ReportLifecycleStatus status) => status switch
    {
        ReportLifecycleStatus.Draft => "草稿",
        ReportLifecycleStatus.InReview => "審核中",
        ReportLifecycleStatus.Reviewed => "待簽署",
        ReportLifecycleStatus.Published => "已發行",
        ReportLifecycleStatus.Voided => "已作廢",
        _ => status.ToString()
    };

    public static string TransitionLabel(ReportTransition transition) => transition switch
    {
        ReportTransition.Submit => "送審",
        ReportTransition.ApproveReview => "審核核可",
        ReportTransition.RejectReview => "審核退回",
        ReportTransition.Sign => "簽署發行",
        ReportTransition.RejectSign => "簽署退回",
        ReportTransition.Void => "作廢",
        _ => transition.ToString()
    };
}
