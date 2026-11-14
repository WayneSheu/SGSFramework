using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

public enum RuleOutcome
{
    Ok,
    /// <summary>狀態轉移不允許（例如跳階、倒退）。</summary>
    InvalidTransition,
    /// <summary>前道 Gate 尚未通過。</summary>
    GatePrerequisiteNotMet,
    /// <summary>角色不符（例如放行者非 Tech Lead）。</summary>
    RoleNotAllowed,
    /// <summary>違反角色分離（修復人＝驗證人等）。</summary>
    SeparationOfDuties
}

public readonly record struct RuleResult(RuleOutcome Outcome, string? Detail = null)
{
    public bool IsOk => Outcome == RuleOutcome.Ok;
    public static RuleResult Ok() => new(RuleOutcome.Ok);
    public static RuleResult Fail(RuleOutcome outcome, string detail) => new(outcome, detail);
}

/// <summary>
/// SSDLC 卡控規則（SOP 3.3）：Design → Build → Verification → Release 逐道放行，前道未過不得放行後道，
/// 且進入下一生命週期階段須先通過對應 Gate。放行者須為專案 Tech Lead（A）。
/// </summary>
public static class GateRule
{
    /// <summary>進入各階段前須先通過的 Gate；未列出者無卡控。</summary>
    private static readonly IReadOnlyDictionary<ProjectPhase, GateKind> PhaseRequirement = new Dictionary<ProjectPhase, GateKind>
    {
        [ProjectPhase.Development] = GateKind.Design,
        [ProjectPhase.Testing] = GateKind.Build,
        [ProjectPhase.DeployClose] = GateKind.Verification
    };

    /// <summary>四道 Gate 依序列表。</summary>
    public static IReadOnlyList<GateKind> Order { get; } =
        [GateKind.Design, GateKind.Build, GateKind.Verification, GateKind.Release];

    /// <summary>
    /// 判斷能否放行（或駁回）指定 Gate：放行者須為 Tech Lead；放行時前道必須已通過；已放行者不可重複放行。
    /// </summary>
    public static RuleResult CanDecide(GateKind gate, GateStatus decision, IReadOnlyDictionary<GateKind, GateStatus> current, bool actorIsTechLead)
    {
        if (!actorIsTechLead)
            return RuleResult.Fail(RuleOutcome.RoleNotAllowed, "僅專案 Tech Lead 可放行或駁回卡控節點。");

        if (decision == GateStatus.Pending)
            return RuleResult.Fail(RuleOutcome.InvalidTransition, "決議須為 Approved 或 Rejected。");

        if (StatusOf(current, gate) == GateStatus.Approved)
            return RuleResult.Fail(RuleOutcome.InvalidTransition, $"{gate} Gate 已放行，不可重複決議。");

        if (decision == GateStatus.Approved)
        {
            var previous = Previous(gate);
            if (previous is { } prev && StatusOf(current, prev) != GateStatus.Approved)
                return RuleResult.Fail(RuleOutcome.GatePrerequisiteNotMet, $"{prev} Gate 尚未通過，無法放行 {gate} Gate。");
        }

        return RuleResult.Ok();
    }

    /// <summary>判斷能否推進至目標階段：僅能前進一階，且須先通過該階段的前置 Gate。</summary>
    public static RuleResult CanAdvance(ProjectPhase current, ProjectPhase target, IReadOnlyDictionary<GateKind, GateStatus> gates)
    {
        if ((int)target != (int)current + 1)
            return RuleResult.Fail(RuleOutcome.InvalidTransition, $"階段只能依序前進（目前 {current}，目標 {target}）。");

        if (PhaseRequirement.TryGetValue(target, out var required) && StatusOf(gates, required) != GateStatus.Approved)
            return RuleResult.Fail(RuleOutcome.GatePrerequisiteNotMet, $"{required} Gate 尚未通過，無法進入 {target}。");

        return RuleResult.Ok();
    }

    /// <summary>前一道 Gate；Design 為第一道故為 null。</summary>
    public static GateKind? Previous(GateKind gate)
    {
        var index = Order.ToList().IndexOf(gate);
        return index > 0 ? Order[index - 1] : null;
    }

    private static GateStatus StatusOf(IReadOnlyDictionary<GateKind, GateStatus> map, GateKind gate) =>
        map.TryGetValue(gate, out var status) ? status : GateStatus.Pending;
}
