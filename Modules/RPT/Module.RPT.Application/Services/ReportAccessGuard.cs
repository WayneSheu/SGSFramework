using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SGS.Modules.RPT.Infrastructure.Entities;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using SGSFramework.Core.Abstractions.ReportAccess;

namespace SGS.Modules.RPT.Application.Services;

/// <summary>
/// 報告資料層授權門面：由報告實體組成 <see cref="ReportAccessContext"/>，交由框架的
/// <see cref="IReportAccessAuthorizationService"/> 判定。模組內所有報告存取都必須經過此處（防 IDOR）。
/// </summary>
public interface IReportAccessGuard
{
    ClaimsPrincipal User { get; }

    Guid? CurrentUserId { get; }

    string CurrentUserName { get; }

    /// <summary>目前請求的區域實驗室（標頭優先，其次 Token 宣告）</summary>
    Guid? CurrentLabId { get; }

    Task<ReportAccessDecision> AuthorizeAsync(Report report, ReportActions action, CancellationToken cancellationToken = default);

    /// <summary>逐一評估多個動作（同一份快照），供前端顯示可用操作</summary>
    Task<IReadOnlyDictionary<ReportActions, ReportAccessDecision>> EvaluateAsync(Report report, IEnumerable<ReportActions> actions, CancellationToken cancellationToken = default);

    Task<bool> CanCreateAsync(Guid tenantLabId, string categoryCode, CancellationToken cancellationToken = default);

    Task<ReportAccessSubject?> GetSubjectAsync(Guid tenantLabId, CancellationToken cancellationToken = default);
}

public sealed class ReportAccessGuard(
    IReportAccessAuthorizationService authorizationService,
    IHttpContextAccessor httpContextAccessor) : IReportAccessGuard
{
    public ClaimsPrincipal User => httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());

    public Guid? CurrentUserId => AuthorizationClaimHelper.ResolveUserId(User);

    public string CurrentUserName => User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "未知使用者";

    public Guid? CurrentLabId => AuthorizationClaimHelper.ResolveTenantLabId(User, httpContextAccessor.HttpContext);

    public Task<ReportAccessDecision> AuthorizeAsync(Report report, ReportActions action, CancellationToken cancellationToken = default) =>
        authorizationService.AuthorizeAsync(User, ToContext(report), action, cancellationToken);

    public async Task<IReadOnlyDictionary<ReportActions, ReportAccessDecision>> EvaluateAsync(
        Report report, IEnumerable<ReportActions> actions, CancellationToken cancellationToken = default)
    {
        var subject = await GetSubjectAsync(report.TenantLabId, cancellationToken).ConfigureAwait(false);
        var context = ToContext(report);
        return actions.Distinct().ToDictionary(
            a => a,
            a => subject is null ? ReportAccessDecision.Deny("無法識別使用者身分。") : ReportAccessPolicy.Evaluate(subject, context, a));
    }

    public Task<bool> CanCreateAsync(Guid tenantLabId, string categoryCode, CancellationToken cancellationToken = default) =>
        authorizationService.HasCategoryActionAsync(User, tenantLabId, [categoryCode], ReportActions.Edit, cancellationToken);

    public Task<ReportAccessSubject?> GetSubjectAsync(Guid tenantLabId, CancellationToken cancellationToken = default) =>
        authorizationService.GetSubjectAsync(User, tenantLabId, cancellationToken);

    public static ReportAccessContext ToContext(Report report) => new()
    {
        CategoryCode = report.CategoryCode,
        TenantLabId = report.TenantLabId,
        Status = report.Status,
        Confidentiality = report.Confidentiality,
        IsBlind = report.IsBlind,
        AssignedUserIds = report.Assignments.Select(a => a.UserId).Append(report.AuthorId).Distinct().ToList(),
        AuthorId = report.AuthorId,
        ReviewerId = report.ReviewerId
    };
}
