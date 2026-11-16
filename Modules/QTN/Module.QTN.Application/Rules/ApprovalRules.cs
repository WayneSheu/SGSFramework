using SGS.Modules.QTN.Infrastructure.Entities;

namespace SGS.Modules.QTN.Application.Rules;

public enum RuleOutcome
{
    Ok,
    InvalidTransition,
    Locked,
    ValidationFailed,
    RoleNotAllowed,
    SeparationOfDuties,
    Blocked
}

public readonly record struct RuleResult(RuleOutcome Outcome, string? Detail = null)
{
    public bool IsOk => Outcome == RuleOutcome.Ok;
    public static RuleResult Ok() => new(RuleOutcome.Ok);
    public static RuleResult Fail(RuleOutcome outcome, string detail) => new(outcome, detail);
}

/// <param name="Route">必經審批路徑（依序）。</param>
/// <param name="BonusFactor">業務獎金提成係數：綠 1.0、黃 0.8、紅 0.5。</param>
/// <param name="Blocked">毛利率低於 20%：系統鎖定送出（戰略標竿客戶特批除外）。</param>
/// <param name="RequiresDiscountExplanation">黃燈以上須填寫《折扣說明書》。</param>
public sealed record MarginEvaluation(
    MarginLight Light, IReadOnlyList<ApprovalRole> Route, decimal BonusFactor, bool Blocked, string? BlockReason, bool RequiresDiscountExplanation);

/// <summary>
/// 毛利率紅綠燈與審批權責矩陣（SOP 5.2），滿足任一觸發條件即採最高層級：
/// 綠：金額 &lt; 100 萬且毛利 ≥ 40% → Tech Lead → 業務經理；
/// 黃：金額 100~500 萬或毛利 30%~39% → ＋PMO 總監；
/// 紅：金額 &gt; 500 萬或毛利 &lt; 30% → ＋財務長 → 總經理；毛利 &lt; 20% 鎖定送出（戰略客戶特批除外）。
/// 涉及 IP 買斷：強制法務長會簽。金額以成交總價（未稅）判斷。
/// </summary>
public static class MarginLightPolicy
{
    public const decimal GreenAmountCap = 1_000_000m;
    public const decimal YellowAmountCap = 5_000_000m;
    public const decimal GreenMarginFloor = 0.40m;
    public const decimal YellowMarginFloor = 0.30m;
    public const decimal LockMarginFloor = 0.20m;

    public static MarginLight Light(decimal finalPrice, decimal marginRate)
    {
        if (finalPrice > YellowAmountCap || marginRate < YellowMarginFloor) return MarginLight.Red;
        if (finalPrice >= GreenAmountCap || marginRate < GreenMarginFloor) return MarginLight.Yellow;
        return MarginLight.Green;
    }

    public static MarginEvaluation Evaluate(decimal finalPrice, decimal marginRate, bool ipBuyout, bool specialApproval)
    {
        var light = Light(finalPrice, marginRate);

        var route = new List<ApprovalRole> { ApprovalRole.TechLead, ApprovalRole.SalesManager };
        if (light >= MarginLight.Yellow) route.Add(ApprovalRole.PmoDirector);
        if (light == MarginLight.Red) route.AddRange([ApprovalRole.Cfo, ApprovalRole.Ceo]);
        if (ipBuyout) route.Add(ApprovalRole.Legal);

        var blocked = marginRate < LockMarginFloor && !specialApproval;
        return new MarginEvaluation(
            light, route, BonusFactor(light), blocked,
            blocked ? $"預估毛利率 {marginRate:P1} 低於 20%，系統鎖定送出；僅限戰略標竿客戶申請特批。" : null,
            light >= MarginLight.Yellow);
    }

    public static decimal BonusFactor(MarginLight light) => light switch
    {
        MarginLight.Green => 1.0m,
        MarginLight.Yellow => 0.8m,
        _ => 0.5m
    };

    public static string LightName(MarginLight light) => light switch
    {
        MarginLight.Green => "綠燈（健康）",
        MarginLight.Yellow => "黃燈（警戒）",
        _ => "紅燈（高危）"
    };

    public static string RoleName(ApprovalRole role) => role switch
    {
        ApprovalRole.TechLead => "Tech Lead",
        ApprovalRole.SalesManager => "業務經理",
        ApprovalRole.PmoDirector => "PMO 總監",
        ApprovalRole.Cfo => "財務長",
        ApprovalRole.Ceo => "總經理",
        ApprovalRole.Legal => "法務長",
        _ => role.ToString()
    };
}

/// <summary>審批關卡的職責分離檢核（純函式）。</summary>
public static class ApprovalSod
{
    /// <param name="userId">決定者。</param>
    /// <param name="submittedBy">送審者（不得核准自己送審的報價）。</param>
    /// <param name="decidedThisCycle">本輪已由此人決定過的關卡數（同一人同輪只能決定一關，避免一人走完多關）。</param>
    /// <param name="holdsRole">決定者是否擔任該關卡角色（Tech Lead 關卡＝商機 Tech Lead；其餘＝審批人指派）。</param>
    public static RuleResult CanDecide(Guid userId, Guid? submittedBy, int decidedThisCycle, bool holdsRole)
    {
        if (!holdsRole) return RuleResult.Fail(RuleOutcome.RoleNotAllowed, "您不是此關卡的審批人。");
        if (submittedBy == userId) return RuleResult.Fail(RuleOutcome.SeparationOfDuties, "送審者不得核准自己送審的報價（職責分離）。");
        if (decidedThisCycle > 0) return RuleResult.Fail(RuleOutcome.SeparationOfDuties, "同一人於同一輪審批只能決定一個關卡（職責分離）。");
        return RuleResult.Ok();
    }
}

/// <summary>
/// 報價版本狀態機（SOP 1.2 版本不可變原則）：僅 Draft 可編修；送審即鎖定，任何調整須衍生新版本並將來源標為 Superseded。
/// </summary>
public static class QuoteWorkflow
{
    public static bool IsEditable(QuoteStatus status, bool isLocked) => status == QuoteStatus.Draft && !isLocked;

    public static RuleResult CanEdit(QuoteStatus status, bool isLocked) =>
        IsEditable(status, isLocked) ? RuleResult.Ok() : RuleResult.Fail(RuleOutcome.Locked, "報價已進入審批或已核准，不可直接修改；請衍生新版本。");

    public static RuleResult CanSubmit(QuoteStatus status, bool techReviewed)
    {
        if (status != QuoteStatus.Draft) return RuleResult.Fail(RuleOutcome.InvalidTransition, "僅草稿可送審。");
        if (!techReviewed) return RuleResult.Fail(RuleOutcome.ValidationFailed, "尚未經 Tech Lead 完成工時評估與技術可行性複核，不得送審（技術評估前置原則）。");
        return RuleResult.Ok();
    }

    /// <summary>可衍生新版本的來源狀態（草稿直接修改；審批中須等結果；已成交走 CR）。</summary>
    public static bool CanRevise(QuoteStatus status) =>
        status is QuoteStatus.Rejected or QuoteStatus.Approved or QuoteStatus.Issued or QuoteStatus.Expired;

    /// <summary>衍生新版本時，來源版本是否轉為 Superseded（退件與逾期版本保留原狀態作為紀錄）。</summary>
    public static bool SupersedesSource(QuoteStatus status) => status is QuoteStatus.Approved or QuoteStatus.Issued;

    public static bool CanCreateChangeRequest(QuoteStatus status) => status == QuoteStatus.Won;

    public static RuleResult CanIssue(QuoteStatus status) =>
        status == QuoteStatus.Approved ? RuleResult.Ok() : RuleResult.Fail(RuleOutcome.InvalidTransition, "僅已核准的報價可對外發出。");

    public static RuleResult CanMarkWon(QuoteStatus status, DateOnly? validUntil, DateOnly today)
    {
        if (status != QuoteStatus.Issued) return RuleResult.Fail(RuleOutcome.InvalidTransition, "僅已發出的報價可登錄成交。");
        if (validUntil is { } v && today > v) return RuleResult.Fail(RuleOutcome.InvalidTransition, "報價已逾有效期，請重新評估並衍生新版本。");
        return RuleResult.Ok();
    }

    public static RuleResult CanMarkLost(QuoteStatus status) =>
        status is QuoteStatus.Issued or QuoteStatus.Approved or QuoteStatus.Expired
            ? RuleResult.Ok()
            : RuleResult.Fail(RuleOutcome.InvalidTransition, "此狀態不可登錄未得標。");

    public static bool IsExpired(QuoteStatus status, DateOnly? validUntil, DateOnly today) =>
        status == QuoteStatus.Issued && validUntil is { } v && today > v;

    /// <summary>報價有效期：自出具日起 <paramref name="validDays"/> 個日曆天。</summary>
    public static DateOnly ValidUntil(DateOnly issuedDate, int validDays) => issuedDate.AddDays(Math.Clamp(validDays, 1, 365));

    /// <summary>下一個版本號：次版本取商機所有版本最大值＋1；CR 版本加「-CR」。</summary>
    public static (int Major, int Minor, string Label) NextVersion(IEnumerable<(int Major, int Minor)> existing, bool isChangeRequest)
    {
        ArgumentNullException.ThrowIfNull(existing);
        var list = existing.ToList();
        var major = list.Count == 0 ? 1 : list.Max(v => v.Major);
        var minor = list.Count == 0 ? 0 : list.Where(v => v.Major == major).Max(v => v.Minor) + 1;
        return (major, minor, Label(major, minor, isChangeRequest));
    }

    public static string Label(int major, int minor, bool isChangeRequest) => $"v{major}.{minor}{(isChangeRequest ? "-CR" : string.Empty)}";
}
