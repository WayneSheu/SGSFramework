using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

public enum CrAction
{
    Evaluate,
    Decide,
    Sign,
    Cancel,
    Reopen
}

/// <summary>
/// CR 狀態機（SOP 第 4 章）：受理 → 影響評估（Tech Lead）→ 決策（PM）→ 簽核 → 核准；
/// 決策 C 為駁回或暫緩。簽核後內容鎖定，不可再評估或改決策。
/// </summary>
public static class CrWorkflow
{
    /// <param name="decision">決策動作時的方案；其他動作忽略。</param>
    /// <param name="defer">決策 C 時是否為「暫緩」（轉 Backlog）而非駁回。</param>
    public static RuleResult Next(CrStatus current, CrAction action, CrDecision? decision, bool defer, out CrStatus next)
    {
        next = current;

        switch (action)
        {
            // 評估可於受理後與待決策期間重做（決策前內容尚未鎖定）
            case CrAction.Evaluate when current is CrStatus.Received or CrStatus.PendingDecision:
                next = CrStatus.PendingDecision;
                return RuleResult.Ok();

            case CrAction.Decide when current == CrStatus.PendingDecision && decision is { } d:
                next = d switch
                {
                    CrDecision.C_Reject => defer ? CrStatus.Deferred : CrStatus.Rejected,
                    _ => CrStatus.PendingSign
                };
                return RuleResult.Ok();

            case CrAction.Sign when current == CrStatus.PendingSign:
                next = CrStatus.PendingSign;
                return RuleResult.Ok();

            case CrAction.Cancel when current is CrStatus.Received or CrStatus.PendingDecision:
                next = CrStatus.Cancelled;
                return RuleResult.Ok();

            // 暫緩（轉 Backlog）的 CR 於下期規劃時重啟：回到受理，重新評估與決策
            case CrAction.Reopen when current == CrStatus.Deferred:
                next = CrStatus.Received;
                return RuleResult.Ok();
        }

        return RuleResult.Fail(RuleOutcome.InvalidTransition, $"CR 目前狀態為 {current}，不可執行「{action}」。");
    }
}

/// <summary>
/// CR 簽核規則（SOP 4.2）：客戶 CR 須 PM＋Tech Lead＋客戶；內部 CR 須 PM＋Tech Lead。
/// 客戶簽核須在 PM、Tech Lead 都簽完之後；每個角色只能簽一次。
/// </summary>
public static class CrSignoffRules
{
    public static IReadOnlyList<CrSignRole> Required(CrKind kind) =>
        kind == CrKind.Customer
            ? [CrSignRole.Pm, CrSignRole.TechLead, CrSignRole.Customer]
            : [CrSignRole.Pm, CrSignRole.TechLead];

    public static IReadOnlyList<CrSignRole> Pending(CrKind kind, IEnumerable<CrSignRole> signed)
    {
        var done = signed.ToHashSet();
        return Required(kind).Where(r => !done.Contains(r)).ToList();
    }

    public static bool IsComplete(CrKind kind, IEnumerable<CrSignRole> signed) => Pending(kind, signed).Count == 0;

    /// <summary>
    /// 判斷簽核者可簽的角色：專案 PM 簽 PM、Tech Lead 簽 Tech Lead；其他人不可簽。
    /// （客戶簽核由 PM 以代錄路徑處理，見 <see cref="ValidateCustomerProxy"/>。）
    /// </summary>
    public static CrSignRole? RoleOf(Guid actor, IEnumerable<(Guid UserId, RaciRole Role)> members)
    {
        var list = members.ToList();
        if (ProjectAccessPolicy.IsPm(actor, list)) return CrSignRole.Pm;
        if (ProjectAccessPolicy.IsTechLead(actor, list)) return CrSignRole.TechLead;
        return null;
    }

    public static RuleResult ValidateSign(CrKind kind, CrSignRole role, IEnumerable<CrSignRole> alreadySigned)
    {
        var signed = alreadySigned.ToList();

        if (!Required(kind).Contains(role))
            return RuleResult.Fail(RuleOutcome.RoleNotAllowed, $"此 CR 不需要 {role} 簽核。");
        if (signed.Contains(role))
            return RuleResult.Fail(RuleOutcome.InvalidTransition, $"{role} 已簽核，不可重複簽核。");

        // 客戶最後簽：須 PM 與 Tech Lead 先簽
        if (role == CrSignRole.Customer && (!signed.Contains(CrSignRole.Pm) || !signed.Contains(CrSignRole.TechLead)))
            return RuleResult.Fail(RuleOutcome.InvalidTransition, "客戶簽核須在 PM 與 Tech Lead 都簽核之後。");

        return RuleResult.Ok();
    }

    /// <summary>PM 代錄客戶簽核（例外路徑）：僅限客戶 CR、須 PM 本人、須填原因，且 PM 與 Tech Lead 已簽。</summary>
    public static RuleResult ValidateCustomerProxy(
        CrKind kind, Guid actor, IEnumerable<(Guid UserId, RaciRole Role)> members, IEnumerable<CrSignRole> alreadySigned, string? reason)
    {
        if (kind != CrKind.Customer)
            return RuleResult.Fail(RuleOutcome.InvalidTransition, "內部 CR 不需要客戶簽核。");
        if (!ProjectAccessPolicy.IsPm(actor, members))
            return RuleResult.Fail(RuleOutcome.RoleNotAllowed, "僅專案 PM 可代錄客戶簽核。");
        if (string.IsNullOrWhiteSpace(reason))
            return RuleResult.Fail(RuleOutcome.InvalidTransition, "代錄客戶簽核須填寫原因（並保留客戶簽核證明）。");

        return ValidateSign(kind, CrSignRole.Customer, alreadySigned);
    }
}

/// <summary>CR 影響評估與決策的輸入驗證（SOP 4.3、4.4）。</summary>
public static class CrInputRules
{
    public static readonly IReadOnlyList<string> AllLayers = ["Presentation", "Api", "Application", "Domain", "Infrastructure"];

    /// <summary>高風險層級：Domain 與 Infrastructure（CR 評估須特別標註）。</summary>
    public static bool IsHighRisk(IEnumerable<string> layers) =>
        layers.Any(l => l.Equals("Domain", StringComparison.OrdinalIgnoreCase) || l.Equals("Infrastructure", StringComparison.OrdinalIgnoreCase));

    /// <summary>驗證並正規化受影響層級；回傳去重、依標準順序排列的清單。</summary>
    public static (IReadOnlyList<string>? Layers, string? Error) NormalizeLayers(IEnumerable<string>? layers)
    {
        var requested = (layers ?? []).Select(l => l?.Trim() ?? string.Empty).Where(l => l.Length > 0).ToList();
        if (requested.Count == 0) return (null, "請至少選擇一個受影響的整潔架構層級。");

        var unknown = requested.FirstOrDefault(l => !AllLayers.Contains(l, StringComparer.OrdinalIgnoreCase));
        if (unknown is not null) return (null, $"未知的層級：{unknown}。");

        var set = requested.Select(l => AllLayers.First(a => a.Equals(l, StringComparison.OrdinalIgnoreCase))).ToHashSet();
        return (AllLayers.Where(set.Contains).ToList(), null);
    }

    public static string? ValidateHours(decimal be, decimal fe, decimal verify)
    {
        foreach (var (hours, label) in new[] { (be, "BE"), (fe, "FE"), (verify, "交叉驗證") })
        {
            if (hours < 0 || hours > 9999) return $"{label} 工時須介於 0 與 9999 小時。";
        }

        return null;
    }

    /// <summary>
    /// 決策驗證：A 須新上線日；B 須列出移出的原定任務；C 須說明原因。
    /// 客戶 CR 的 A／B 須報價；內部 CR 不報價。
    /// </summary>
    public static string? ValidateDecision(
        CrKind kind, CrDecision decision, decimal? quote, DateOnly? newReleaseDate, string? swappedOut, string? rationale, DateOnly today)
    {
        if (quote is < 0) return "報價金額不可為負數。";

        switch (decision)
        {
            case CrDecision.A_Delay:
                if (newReleaseDate is not { } date) return "方案 A（時程順延）須指定新的上線日。";
                if (date < today) return "新的上線日不可早於今天。";
                break;
            case CrDecision.B_Swap:
                if (string.IsNullOrWhiteSpace(swappedOut)) return "方案 B（範圍交換）須列出移出本次迭代的原定任務。";
                break;
            case CrDecision.C_Reject:
                if (string.IsNullOrWhiteSpace(rationale)) return "方案 C（駁回或暫緩）須說明原因。";
                break;
        }

        if (decision != CrDecision.C_Reject)
        {
            if (kind == CrKind.Customer && quote is null) return "客戶 CR 須填寫報價金額（SOP 4.2）。";
            if (kind == CrKind.Internal && quote is not null) return "內部 CR 不報價，請清空報價金額。";
        }
        else if (quote is not null)
        {
            return "駁回或暫緩不需要報價金額。";
        }

        return null;
    }
}

/// <summary>CR 影響評估期限（受理後 2 個工作天，SOP 4.4）。</summary>
public static class CrSla
{
    public const int EvaluationWorkdays = 2;

    public static DateTimeOffset EvalDue(DateTimeOffset receivedAt, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var local = receivedAt.ToOffset(SlaCalculator.Offset);
        var dueDate = calendar.AddWorkdays(DateOnly.FromDateTime(local.DateTime), EvaluationWorkdays);
        return new DateTimeOffset(dueDate.ToDateTime(TimeOnly.FromTimeSpan(local.TimeOfDay)), SlaCalculator.Offset);
    }

    /// <summary>評估期限燈號；以「首次完成評估時間」視為已解決。</summary>
    public static SlaStatus EvalStatus(DateTimeOffset due, DateTimeOffset? evaluatedAt, DateTimeOffset now, CrStatus status) =>
        status == CrStatus.Cancelled ? SlaStatus.NotApplicable : SlaCalculator.Status(due, evaluatedAt, now);
}

/// <summary>簽核內容雜湊：簽核者看到的內容須與決策當下相符，簽核後內容鎖定（防竄改）。</summary>
public sealed record CrContent(
    string CrNo, CrKind Kind, string Title, string Description,
    string AffectedLayers, CrApiImpact ApiImpact, CrCouplingImpact CouplingImpact, bool SecurityImpact,
    decimal HoursBe, decimal HoursFe, decimal HoursVerify,
    CrDecision Decision, decimal? QuoteAmount, DateOnly? NewReleaseDate, string? SwappedOutItems, string? Rationale);

public static class CrContentHash
{
    public static string Compute(CrContent c)
    {
        ArgumentNullException.ThrowIfNull(c);

        var inv = CultureInfo.InvariantCulture;
        // 正規化：以固定分隔、不可混淆的欄位順序串接，數值以不分文化格式表示
        var canonical = string.Join('\u001F',
            c.CrNo, c.Kind, c.Title.Trim(), c.Description.Trim(),
            c.AffectedLayers, c.ApiImpact, c.CouplingImpact, c.SecurityImpact ? "1" : "0",
            c.HoursBe.ToString("0.##", inv), c.HoursFe.ToString("0.##", inv), c.HoursVerify.ToString("0.##", inv),
            c.Decision, c.QuoteAmount?.ToString("0.##", inv) ?? string.Empty,
            c.NewReleaseDate?.ToString("yyyy-MM-dd", inv) ?? string.Empty,
            (c.SwappedOutItems ?? string.Empty).Trim(), (c.Rationale ?? string.Empty).Trim());

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

/// <summary>CR 單號格式：<c>專案代號-CR-流水號</c>。</summary>
public static class CrNumbering
{
    public static string Format(string projectCode, int sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectCode);
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
        return $"{projectCode}-CR-{sequence:D3}";
    }
}
