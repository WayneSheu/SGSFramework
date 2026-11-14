using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

public enum TicketAction
{
    Triage,
    Resolve,
    VerifyPass,
    VerifyFail,
    Close,
    Cancel,
    Reopen
}

/// <summary>
/// 問題單狀態機（SOP 6）：受理 → 分流 → 處理中 → 待驗證 → 待複測 → 結案。
/// 諮詢／資料撈取／CR 無程式修復，可由 PM 於處理中直接結案；系統異常必須經修復與非作者驗證。
/// </summary>
public static class TicketWorkflow
{
    public static RuleResult Next(TicketStatus current, TicketAction action, TicketType type, out TicketStatus next)
    {
        next = current;
        var isIncident = type == TicketType.Incident;

        switch (action)
        {
            case TicketAction.Triage when current == TicketStatus.Received:
                next = TicketStatus.InProgress;
                return RuleResult.Ok();

            case TicketAction.Resolve when current == TicketStatus.InProgress && isIncident:
                next = TicketStatus.PendingVerify;
                return RuleResult.Ok();

            case TicketAction.VerifyPass when current == TicketStatus.PendingVerify:
                next = TicketStatus.PendingRetest;
                return RuleResult.Ok();

            case TicketAction.VerifyFail when current == TicketStatus.PendingVerify:
                next = TicketStatus.InProgress;
                return RuleResult.Ok();

            case TicketAction.Close when current == TicketStatus.PendingRetest:
                next = TicketStatus.Closed;
                return RuleResult.Ok();

            case TicketAction.Close when current == TicketStatus.InProgress && !isIncident:
                next = TicketStatus.Closed;
                return RuleResult.Ok();

            case TicketAction.Cancel when current is TicketStatus.Received or TicketStatus.InProgress:
                next = TicketStatus.Cancelled;
                return RuleResult.Ok();

            case TicketAction.Reopen when current == TicketStatus.PendingRetest:
                next = TicketStatus.InProgress;
                return RuleResult.Ok();
        }

        return RuleResult.Fail(RuleOutcome.InvalidTransition, $"問題單目前狀態為 {current}，不可執行「{action}」。");
    }
}

/// <summary>
/// 問題單角色分離（SOP 1.2、6.2）：修復人≠驗證人（非作者驗證），二者須為專案 BE／FE／Tech Lead 成員。
/// 純函式，伺服器端強制；資料層另有 CHECK 約束。
/// </summary>
public static class TicketSeparationOfDuties
{
    private static readonly RaciRole[] EligibleRoles = [RaciRole.Be, RaciRole.Fe, RaciRole.TechLead];

    public static RuleResult ValidateAssignment(Guid fixer, Guid verifier, IEnumerable<(Guid UserId, RaciRole Role)> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        if (fixer == Guid.Empty || verifier == Guid.Empty)
            return RuleResult.Fail(RuleOutcome.InvalidTransition, "須同時指定修復人與驗證人。");

        if (fixer == verifier)
            return RuleResult.Fail(RuleOutcome.SeparationOfDuties, "修復人與驗證人不得為同一人（須由非作者驗證）。");

        var list = members.ToList();
        foreach (var (id, label) in new[] { (fixer, "修復人"), (verifier, "驗證人") })
        {
            if (!list.Any(m => m.UserId == id && EligibleRoles.Contains(m.Role)))
                return RuleResult.Fail(RuleOutcome.RoleNotAllowed, $"{label}須為專案的 BE、FE 或 Tech Lead 成員。");
        }

        return RuleResult.Ok();
    }

    /// <summary>回報修復：僅被指派的修復人。</summary>
    public static RuleResult ValidateResolve(Guid actor, Guid? fixer) =>
        fixer is { } f && f == actor
            ? RuleResult.Ok()
            : RuleResult.Fail(RuleOutcome.RoleNotAllowed, "僅被指派的修復人可回報修復。");

    /// <summary>驗證：僅被指派的驗證人，且不得為修復人。</summary>
    public static RuleResult ValidateVerify(Guid actor, Guid? fixer, Guid? verifier)
    {
        if (fixer is { } f && f == actor)
            return RuleResult.Fail(RuleOutcome.SeparationOfDuties, "修復者不得驗證自己的修復（須由非作者驗證）。");

        return verifier is { } v && v == actor
            ? RuleResult.Ok()
            : RuleResult.Fail(RuleOutcome.RoleNotAllowed, "僅被指派的驗證人可執行驗證。");
    }
}

/// <summary>單號格式：<c>專案代號-流水號</c>（至少 3 位數，超過 999 自然延伸）。</summary>
public static class TicketNumbering
{
    public static string Format(string projectCode, int sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectCode);
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
        return $"{projectCode}-{sequence:D3}";
    }
}
