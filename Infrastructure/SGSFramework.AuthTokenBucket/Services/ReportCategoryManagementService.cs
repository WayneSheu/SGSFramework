using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using SGSFramework.Core.Abstractions.ReportAccess;
using ZiggyCreatures.Caching.Fusion;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>
/// 報告類別權限管理：類別主檔、承作實驗室、類別授權、簽署人資格，以及敏感授權的雙重審批（Maker-Checker）。
/// 授權與資格不刪除，以撤銷保留完整稽核軌跡；撤銷屬權限降低，立即生效。
/// </summary>
public interface IReportCategoryManagementService
{
    Task<IReadOnlyList<ReportCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message, ReportCategoryDto? Category)> SaveCategoryAsync(SaveReportCategoryRequest request, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> SetCategoryLabsAsync(string categoryCode, IReadOnlyCollection<Guid> tenantLabIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportLabOptionDto>> GetLabOptionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportPrincipalOptionDto>> SearchPrincipalsAsync(string? keyword, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportCategoryGrantDto>> ListGrantsAsync(string? categoryCode, Guid? principalId, bool includeInactive, CancellationToken cancellationToken = default);

    Task<ReportGrantSubmitResult> SubmitGrantAsync(CreateReportCategoryGrantRequest request, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> RevokeGrantAsync(Guid id, string? reason, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SignerQualificationDto>> ListQualificationsAsync(string? categoryCode, Guid? userId, bool includeInactive, CancellationToken cancellationToken = default);

    Task<ReportGrantSubmitResult> SubmitQualificationAsync(CreateSignerQualificationRequest request, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> RevokeQualificationAsync(Guid id, string? reason, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportGrantChangeRequestDto>> ListChangeRequestsAsync(RoleChangeRequestStatus? status, CancellationToken cancellationToken = default);

    Task<ReportGrantSubmitResult> ApproveAsync(Guid requestId, string? comment, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> RejectAsync(Guid requestId, string? comment, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> CancelAsync(Guid requestId, CancellationToken cancellationToken = default);
}

public sealed partial class ReportCategoryManagementService(
    DbContext dbContext,
    IOrganizationIntegrationService orgIntegrationService,
    IPermissionChangeNotifier permissionChangeNotifier,
    IFusionCache cache,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider,
    ILogger<ReportCategoryManagementService> logger) : IReportCategoryManagementService
{
    /// <summary>簽署人資格單次最長效期（認證資格通常 3 年一換）</summary>
    private static readonly TimeSpan MaxQualificationDuration = TimeSpan.FromDays(366 * 3);

    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    [GeneratedRegex("^[A-Z0-9_]{2,32}$")]
    private static partial Regex CategoryCodePattern();

    // ===== 類別主檔 =====

    public async Task<IReadOnlyList<ReportCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await dbContext.Set<ReportCategory>().AsNoTracking()
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Code)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var assignments = await dbContext.Set<ReportCategoryLabAssignment>().AsNoTracking()
            .Select(a => new { a.CategoryCode, a.TenantLabId })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var labsByCategory = assignments
            .GroupBy(a => a.CategoryCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(a => a.TenantLabId).ToList(), StringComparer.OrdinalIgnoreCase);

        return categories.Select(c => ToDto(c, labsByCategory.GetValueOrDefault(c.Code) ?? [])).ToList();
    }

    public async Task<(bool Succeeded, string Message, ReportCategoryDto? Category)> SaveCategoryAsync(
        SaveReportCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0) return (false, "請填寫類別名稱。", null);

        ReportCategory entity;
        if (request.Id is null)
        {
            var code = request.Code?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!CategoryCodePattern().IsMatch(code)) return (false, "類別代碼須為 2～32 碼大寫英數字或底線。", null);

            bool exists = await dbContext.Set<ReportCategory>().AnyAsync(c => c.Code == code, cancellationToken).ConfigureAwait(false);
            if (exists) return (false, $"類別代碼「{code}」已存在。", null);

            entity = new ReportCategory { Code = code, CreatedBy = CurrentActor().Name };
            dbContext.Set<ReportCategory>().Add(entity);
        }
        else
        {
            var found = await dbContext.Set<ReportCategory>().FirstOrDefaultAsync(c => c.Id == request.Id.Value, cancellationToken).ConfigureAwait(false);
            if (found is null) return (false, "找不到指定的報告類別。", null);

            if (!string.Equals(found.Code, request.Code?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return (false, "類別代碼建立後不可變更（授權與報告皆以代碼參照）。", null);
            }

            // 樂觀鎖：以前端讀取時的版本比對，避免覆蓋他人同時間的修改
            if (!string.IsNullOrWhiteSpace(request.RowVersion))
            {
                dbContext.Entry(found).Property(c => c.RowVersion).OriginalValue = Convert.FromBase64String(request.RowVersion);
            }

            entity = found;
        }

        entity.Name = name;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.DefaultConfidentiality = request.DefaultConfidentiality;
        entity.RegulationTags = string.IsNullOrWhiteSpace(request.RegulationTags) ? null : request.RegulationTags.Trim();
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "此類別已被其他人修改，請重新整理後再編輯。", null);
        }

        // 類別啟用狀態影響全體使用者的授權快照
        await cache.RemoveByTagAsync(ReportAccessCacheTags.All, token: cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[ReportCategory] {Actor} 儲存報告類別 {Code}（啟用：{IsActive}）", CurrentActor().Name, entity.Code, entity.IsActive);

        var labs = await dbContext.Set<ReportCategoryLabAssignment>().AsNoTracking()
            .Where(a => a.CategoryCode == entity.Code).Select(a => a.TenantLabId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return (true, $"已儲存報告類別「{entity.Name}」。", ToDto(entity, labs));
    }

    public async Task<(bool Succeeded, string Message)> SetCategoryLabsAsync(
        string categoryCode, IReadOnlyCollection<Guid> tenantLabIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantLabIds);

        var category = await FindCategoryAsync(categoryCode, cancellationToken).ConfigureAwait(false);
        if (category is null) return (false, "找不到指定的報告類別。");

        var targetIds = tenantLabIds.Where(id => id != Guid.Empty).Distinct().ToList();
        foreach (var labId in targetIds)
        {
            var lab = await orgIntegrationService.GetOrganizationByIdAsync(labId, cancellationToken).ConfigureAwait(false);
            if (lab is null) return (false, $"找不到區域實驗室（{labId}）。");
        }

        var existing = await dbContext.Set<ReportCategoryLabAssignment>()
            .Where(a => a.CategoryCode == category.Code)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var actorName = CurrentActor().Name;
        var now = timeProvider.GetUtcNow();
        dbContext.Set<ReportCategoryLabAssignment>().RemoveRange(existing.Where(a => !targetIds.Contains(a.TenantLabId)));
        foreach (var labId in targetIds.Where(id => existing.All(a => a.TenantLabId != id)))
        {
            dbContext.Set<ReportCategoryLabAssignment>().Add(new ReportCategoryLabAssignment
            {
                CategoryCode = category.Code,
                TenantLabId = labId,
                CreatedAt = now,
                CreatedByName = actorName
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await cache.RemoveByTagAsync(ReportAccessCacheTags.All, token: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[ReportCategory] {Actor} 設定類別 {Code} 承作實驗室：{Labs}", actorName, category.Code, string.Join(", ", targetIds));
        return (true, $"已更新「{category.Name}」的承作實驗室（共 {targetIds.Count} 個）。");
    }

    public async Task<IReadOnlyList<ReportLabOptionDto>> GetLabOptionsAsync(CancellationToken cancellationToken = default)
    {
        var orgs = await orgIntegrationService.GetAllActiveOrganizationsAsync(cancellationToken).ConfigureAwait(false);

        // 區域實驗室（分公司）以 TenantLabId 識別；同一 TenantLabId 之下層單位僅取最上層節點顯示
        return orgs
            .Where(o => o.TenantLabId != Guid.Empty)
            .GroupBy(o => o.TenantLabId)
            .Select(g => g.OrderBy(o => o.HierarchyLevel).First())
            .OrderBy(o => o.Code, StringComparer.OrdinalIgnoreCase)
            .Select(o => new ReportLabOptionDto(o.TenantLabId, o.Code, o.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<ReportPrincipalOptionDto>> SearchPrincipalsAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        var term = keyword?.Trim() ?? string.Empty;

        var usersQuery = dbContext.Set<ApplicationUser>().AsNoTracking().Where(u => !u.IsDeleted);
        var rolesQuery = dbContext.Set<ApplicationRole>().AsNoTracking().Where(r => !r.IsDeleted && r.Name != null);
        if (term.Length > 0)
        {
            usersQuery = usersQuery.Where(u => (u.UserName != null && u.UserName.Contains(term)) || u.FullName.Contains(term) ||
                                               (u.EmployeeNo != null && u.EmployeeNo.Contains(term)));
            rolesQuery = rolesQuery.Where(r => r.Name!.Contains(term));
        }

        var roles = await rolesQuery.OrderBy(r => r.Name).Take(30)
            .Select(r => new ReportPrincipalOptionDto(GrantPrincipalType.Role, r.Id, r.Name!, r.Description))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var users = await usersQuery.OrderBy(u => u.UserName).Take(50)
            .Select(u => new { u.Id, u.UserName, u.FullName, u.EmployeeNo })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return roles
            .Concat(users.Select(u => new ReportPrincipalOptionDto(
                GrantPrincipalType.User, u.Id,
                string.IsNullOrWhiteSpace(u.FullName) ? u.UserName ?? u.Id.ToString() : u.FullName,
                string.IsNullOrWhiteSpace(u.EmployeeNo) ? u.UserName : $"{u.UserName}（{u.EmployeeNo}）")))
            .ToList();
    }

    // ===== 類別授權 =====

    public async Task<IReadOnlyList<ReportCategoryGrantDto>> ListGrantsAsync(
        string? categoryCode, Guid? principalId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var query = dbContext.Set<ReportCategoryGrant>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(categoryCode)) query = query.Where(g => g.CategoryCode == categoryCode);
        if (principalId.HasValue) query = query.Where(g => g.PrincipalId == principalId.Value);
        if (!includeInactive) query = query.Where(g => g.RevokedAt == null && (g.EffectiveTo == null || g.EffectiveTo >= now));

        var items = await query.OrderBy(g => g.CategoryCode).ThenByDescending(g => g.CreatedAtUtc).Take(1000)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var principalNames = await ResolvePrincipalNamesAsync(items.Select(g => (g.PrincipalType, g.PrincipalId)), cancellationToken).ConfigureAwait(false);
        var labNames = await ResolveLabNamesAsync(items.Where(g => g.TenantLabId.HasValue).Select(g => g.TenantLabId!.Value), cancellationToken).ConfigureAwait(false);

        return items.Select(g => new ReportCategoryGrantDto(
            g.Id, g.PrincipalType, g.PrincipalId,
            principalNames.GetValueOrDefault((g.PrincipalType, g.PrincipalId)) ?? g.PrincipalId.ToString(),
            g.CategoryCode, g.TenantLabId, g.TenantLabId.HasValue ? labNames[g.TenantLabId.Value] : null,
            g.Actions, g.DataScope, g.MaxConfidentiality, g.EffectiveFrom, g.EffectiveTo, g.Reason,
            StatusOf(g.RevokedAt, g.EffectiveFrom, g.EffectiveTo, now),
            g.CreatedBy, g.CreatedAtUtc, g.RevokedAt, g.RevokedByName, g.RevokeReason)).ToList();
    }

    public async Task<ReportGrantSubmitResult> SubmitGrantAsync(CreateReportCategoryGrantRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (error, summary, subjectUserId) = await ValidateGrantAsync(request, cancellationToken).ConfigureAwait(false);
        if (error is not null) return ReportGrantSubmitResult.Fail(error);

        if (ReportGrantGovernanceRules.RequiresApproval(request.Actions, request.DataScope, request.MaxConfidentiality))
        {
            var changeRequest = await CreateChangeRequestAsync(ReportGrantChangeType.CreateGrant, request, summary, subjectUserId, request.Reason, cancellationToken).ConfigureAwait(false);
            return new ReportGrantSubmitResult(true,
                "此授權包含簽署、作廢、盲樣識別、全域範圍或特檢密級，已送出申請，待其他審核者核准後生效。",
                PendingApproval: true, EntityId: changeRequest.Id);
        }

        var grant = await ApplyGrantAsync(request, cancellationToken).ConfigureAwait(false);
        return new ReportGrantSubmitResult(true, $"已新增授權：{summary}", EntityId: grant.Id);
    }

    public async Task<(bool Succeeded, string Message)> RevokeGrantAsync(Guid id, string? reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "請填寫撤銷原因。");

        var entity = await dbContext.Set<ReportCategoryGrant>().FirstOrDefaultAsync(g => g.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null) return (false, "找不到指定的授權。");
        if (entity.RevokedAt is not null) return (false, "此授權已撤銷。");

        entity.RevokedAt = timeProvider.GetUtcNow();
        entity.RevokedByName = CurrentActor().Name;
        entity.RevokeReason = reason.Trim();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await NotifyPrincipalAsync(entity.PrincipalType, entity.PrincipalId, "report-grant-revoked", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[ReportCategory] {Actor} 撤銷報告類別授權 {Id}（{Reason}）", entity.RevokedByName, id, entity.RevokeReason);
        return (true, "已撤銷授權，立即生效。");
    }

    // ===== 簽署人資格 =====

    public async Task<IReadOnlyList<SignerQualificationDto>> ListQualificationsAsync(
        string? categoryCode, Guid? userId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var query = dbContext.Set<SignerQualification>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(categoryCode)) query = query.Where(q => q.CategoryCode == categoryCode);
        if (userId.HasValue) query = query.Where(q => q.UserId == userId.Value);
        if (!includeInactive) query = query.Where(q => q.RevokedAt == null && q.ValidTo >= now);

        var items = await query.OrderBy(q => q.CategoryCode).ThenBy(q => q.ValidTo).Take(1000)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var userIds = items.Select(q => q.UserId).Distinct().ToList();
        var users = await dbContext.Set<ApplicationUser>().AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.FullName })
            .ToDictionaryAsync(u => u.Id, cancellationToken).ConfigureAwait(false);
        var labNames = await ResolveLabNamesAsync(items.Select(q => q.TenantLabId), cancellationToken).ConfigureAwait(false);

        return items.Select(q =>
        {
            users.TryGetValue(q.UserId, out var u);
            return new SignerQualificationDto(
                q.Id, q.UserId, u?.UserName ?? string.Empty, u?.FullName ?? string.Empty,
                q.CategoryCode, q.TenantLabId, labNames[q.TenantLabId], q.CertificateNo, q.ValidFrom, q.ValidTo, q.Remark,
                StatusOf(q.RevokedAt, q.ValidFrom, q.ValidTo, now), q.CreatedBy, q.RevokedAt, q.RevokedByName, q.RevokeReason);
        }).ToList();
    }

    public async Task<ReportGrantSubmitResult> SubmitQualificationAsync(CreateSignerQualificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (error, summary) = await ValidateQualificationAsync(request, cancellationToken).ConfigureAwait(false);
        if (error is not null) return ReportGrantSubmitResult.Fail(error);

        // 簽署資格一律須經雙重審批
        var changeRequest = await CreateChangeRequestAsync(ReportGrantChangeType.CreateQualification, request, summary, request.UserId, request.Reason, cancellationToken).ConfigureAwait(false);
        return new ReportGrantSubmitResult(true, "已送出簽署人資格申請，待其他審核者核准後生效。", PendingApproval: true, EntityId: changeRequest.Id);
    }

    public async Task<(bool Succeeded, string Message)> RevokeQualificationAsync(Guid id, string? reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "請填寫撤銷原因。");

        var entity = await dbContext.Set<SignerQualification>().FirstOrDefaultAsync(q => q.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null) return (false, "找不到指定的簽署人資格。");
        if (entity.RevokedAt is not null) return (false, "此簽署人資格已撤銷。");

        entity.RevokedAt = timeProvider.GetUtcNow();
        entity.RevokedByName = CurrentActor().Name;
        entity.RevokeReason = reason.Trim();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await permissionChangeNotifier.NotifyUsersAsync([entity.UserId.ToString()], "signer-qualification-revoked", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[ReportCategory] {Actor} 撤銷簽署人資格 {Id}（{Reason}）", entity.RevokedByName, id, entity.RevokeReason);
        return (true, "已撤銷簽署人資格，立即生效。");
    }

    // ===== 雙重審批 =====

    public async Task<IReadOnlyList<ReportGrantChangeRequestDto>> ListChangeRequestsAsync(RoleChangeRequestStatus? status, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<ReportGrantChangeRequest>().AsNoTracking();
        if (status.HasValue) query = query.Where(r => r.Status == status.Value);

        var items = await query.OrderByDescending(r => r.RequestedAt).Take(500)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var (actorId, _) = CurrentActor();
        bool isSuperAdmin = IsCurrentSuperAdmin();
        return items.Select(r => new ReportGrantChangeRequestDto(
            r.Id, r.RequestType, r.Summary, r.SubjectUserId, r.RequestReason, r.Status,
            r.RequestedByName, r.RequestedAt, r.ReviewedByName, r.ReviewedAt, r.ReviewComment,
            CanApprove: r.Status == RoleChangeRequestStatus.Pending && ReviewBlockReason(r, actorId, isSuperAdmin) is null,
            IsMine: actorId.HasValue && actorId.Value == r.RequestedBy)).ToList();
    }

    public async Task<ReportGrantSubmitResult> ApproveAsync(Guid requestId, string? comment, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Set<ReportGrantChangeRequest>().FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken).ConfigureAwait(false);
        if (entity is null) return ReportGrantSubmitResult.Fail("找不到指定的申請。");
        if (entity.Status != RoleChangeRequestStatus.Pending) return ReportGrantSubmitResult.Fail("此申請已處理完畢。");

        var (actorId, actorName) = CurrentActor();
        var blockReason = ReviewBlockReason(entity, actorId, IsCurrentSuperAdmin());
        if (blockReason is not null) return ReportGrantSubmitResult.Fail(blockReason);

        // 核准時重新驗證：申請期間類別、實驗室或對象可能已異動
        Guid appliedId;
        switch (entity.RequestType)
        {
            case ReportGrantChangeType.CreateGrant:
                {
                    var request = JsonSerializer.Deserialize<CreateReportCategoryGrantRequest>(entity.PayloadJson, PayloadJsonOptions)
                        ?? throw new InvalidOperationException("授權申請內容無法解析。");
                    var (error, _, _) = await ValidateGrantAsync(request, cancellationToken).ConfigureAwait(false);
                    if (error is not null) return ReportGrantSubmitResult.Fail($"無法套用：{error}");
                    appliedId = (await ApplyGrantAsync(request, cancellationToken).ConfigureAwait(false)).Id;
                    break;
                }
            case ReportGrantChangeType.CreateQualification:
                {
                    var request = JsonSerializer.Deserialize<CreateSignerQualificationRequest>(entity.PayloadJson, PayloadJsonOptions)
                        ?? throw new InvalidOperationException("簽署人資格申請內容無法解析。");
                    var (error, _) = await ValidateQualificationAsync(request, cancellationToken).ConfigureAwait(false);
                    if (error is not null) return ReportGrantSubmitResult.Fail($"無法套用：{error}");
                    appliedId = (await ApplyQualificationAsync(request, cancellationToken).ConfigureAwait(false)).Id;
                    break;
                }
            default:
                return ReportGrantSubmitResult.Fail("不支援的申請類型。");
        }

        bool isSelfApproval = actorId.HasValue && actorId.Value == entity.RequestedBy;
        entity.Status = RoleChangeRequestStatus.Approved;
        entity.ReviewedBy = actorId;
        entity.ReviewedByName = actorName;
        entity.ReviewedAt = timeProvider.GetUtcNow();
        entity.ReviewComment = (isSelfApproval ? "【最高管理者自行核准】" : string.Empty) + comment?.Trim();
        entity.AppliedEntityId = appliedId;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[ReportCategory] {Actor} 核准 {Requester} 的授權申請 {Id}：{Summary}", actorName, entity.RequestedByName, entity.Id, entity.Summary);
        return new ReportGrantSubmitResult(true, $"已核准並生效：{entity.Summary}", EntityId: appliedId);
    }

    public async Task<(bool Succeeded, string Message)> RejectAsync(Guid requestId, string? comment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comment)) return (false, "請填寫駁回原因。");

        var entity = await dbContext.Set<ReportGrantChangeRequest>().FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken).ConfigureAwait(false);
        if (entity is null) return (false, "找不到指定的申請。");
        if (entity.Status != RoleChangeRequestStatus.Pending) return (false, "此申請已處理完畢。");

        var (actorId, actorName) = CurrentActor();
        entity.Status = RoleChangeRequestStatus.Rejected;
        entity.ReviewedBy = actorId;
        entity.ReviewedByName = actorName;
        entity.ReviewedAt = timeProvider.GetUtcNow();
        entity.ReviewComment = comment.Trim();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[ReportCategory] {Actor} 駁回 {Requester} 的授權申請 {Id}（{Comment}）", actorName, entity.RequestedByName, entity.Id, entity.ReviewComment);
        return (true, "已駁回申請。");
    }

    public async Task<(bool Succeeded, string Message)> CancelAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Set<ReportGrantChangeRequest>().FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken).ConfigureAwait(false);
        if (entity is null) return (false, "找不到指定的申請。");
        if (entity.Status != RoleChangeRequestStatus.Pending) return (false, "此申請已處理完畢。");

        var (actorId, actorName) = CurrentActor();
        if (actorId != entity.RequestedBy) return (false, "僅申請人可撤回申請。");

        entity.Status = RoleChangeRequestStatus.Cancelled;
        entity.ReviewedBy = actorId;
        entity.ReviewedByName = actorName;
        entity.ReviewedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (true, "已撤回申請。");
    }

    // ===== 內部：驗證與套用 =====

    private async Task<(string? Error, string Summary, Guid? SubjectUserId)> ValidateGrantAsync(
        CreateReportCategoryGrantRequest request, CancellationToken cancellationToken)
    {
        if (request.Actions == ReportActions.None) return ("請至少選擇一項授權動作。", string.Empty, null);
        if (request.PrincipalId == Guid.Empty) return ("請選擇授權對象。", string.Empty, null);

        var effectiveFrom = request.EffectiveFrom ?? timeProvider.GetUtcNow();
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value <= effectiveFrom) return ("迄日必須晚於起日。", string.Empty, null);
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < timeProvider.GetUtcNow()) return ("迄日已過，無法建立已失效的授權。", string.Empty, null);

        var category = await FindCategoryAsync(request.CategoryCode, cancellationToken).ConfigureAwait(false);
        if (category is null) return ("找不到指定的報告類別。", string.Empty, null);
        if (!category.IsActive) return ($"報告類別「{category.Name}」已停用。", string.Empty, null);

        string labText = "不限實驗室";
        if (request.TenantLabId.HasValue)
        {
            var lab = await orgIntegrationService.GetOrganizationByIdAsync(request.TenantLabId.Value, cancellationToken).ConfigureAwait(false);
            if (lab is null) return ("找不到指定的區域實驗室。", string.Empty, null);
            labText = lab.Name;
        }
        else if (request.DataScope != ReportDataScope.Global)
        {
            // 未限定實驗室的「所屬實驗室 / 僅指派」授權於使用者所屬的每個實驗室生效
            labText = "所屬各實驗室";
        }

        string principalText;
        Guid? subjectUserId = null;
        if (request.PrincipalType == GrantPrincipalType.User)
        {
            var user = await dbContext.Set<ApplicationUser>().AsNoTracking()
                .Where(u => u.Id == request.PrincipalId && !u.IsDeleted)
                .Select(u => new { u.UserName, u.FullName })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (user is null) return ("找不到指定的使用者。", string.Empty, null);
            principalText = $"使用者「{(string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName)}」";
            subjectUserId = request.PrincipalId;
        }
        else
        {
            var roleName = await dbContext.Set<ApplicationRole>().AsNoTracking()
                .Where(r => r.Id == request.PrincipalId && !r.IsDeleted)
                .Select(r => r.Name)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (roleName is null) return ("找不到指定的角色。", string.Empty, null);
            principalText = $"角色「{roleName}」";
        }

        var summary = $"{principalText} 於「{category.Name}」/{labText}：{request.Actions}（範圍 {request.DataScope}，最高密級 {request.MaxConfidentiality}）";
        return (null, summary.Length > 500 ? summary[..500] : summary, subjectUserId);
    }

    private async Task<ReportCategoryGrant> ApplyGrantAsync(CreateReportCategoryGrantRequest request, CancellationToken cancellationToken)
    {
        var category = await FindCategoryAsync(request.CategoryCode, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("報告類別不存在。");

        var entity = new ReportCategoryGrant
        {
            PrincipalType = request.PrincipalType,
            PrincipalId = request.PrincipalId,
            CategoryCode = category.Code,
            TenantLabId = request.TenantLabId,
            Actions = request.Actions,
            DataScope = request.DataScope,
            MaxConfidentiality = request.MaxConfidentiality,
            EffectiveFrom = request.EffectiveFrom ?? timeProvider.GetUtcNow(),
            EffectiveTo = request.EffectiveTo,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            CreatedBy = CurrentActor().Name
        };
        dbContext.Set<ReportCategoryGrant>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await NotifyPrincipalAsync(entity.PrincipalType, entity.PrincipalId, "report-grant-created", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[ReportCategory] 新增報告類別授權 {Id}：{Type} {Principal} / {Category} / {Actions}",
            entity.Id, entity.PrincipalType, entity.PrincipalId, entity.CategoryCode, entity.Actions);
        return entity;
    }

    private async Task<(string? Error, string Summary)> ValidateQualificationAsync(
        CreateSignerQualificationRequest request, CancellationToken cancellationToken)
    {
        var certificateNo = request.CertificateNo?.Trim() ?? string.Empty;
        if (certificateNo.Length == 0) return ("請填寫資格證號。", string.Empty);
        if (request.ValidTo <= request.ValidFrom) return ("迄日必須晚於起日。", string.Empty);
        if (request.ValidTo - request.ValidFrom > MaxQualificationDuration) return ("單次簽署資格效期不可超過三年。", string.Empty);
        if (request.ValidTo < timeProvider.GetUtcNow()) return ("迄日已過，無法建立已失效的資格。", string.Empty);

        var category = await FindCategoryAsync(request.CategoryCode, cancellationToken).ConfigureAwait(false);
        if (category is null) return ("找不到指定的報告類別。", string.Empty);
        if (!category.IsActive) return ($"報告類別「{category.Name}」已停用。", string.Empty);

        var lab = await orgIntegrationService.GetOrganizationByIdAsync(request.TenantLabId, cancellationToken).ConfigureAwait(false);
        if (lab is null) return ("找不到指定的區域實驗室。", string.Empty);

        bool labHandlesCategory = await dbContext.Set<ReportCategoryLabAssignment>().AsNoTracking()
            .AnyAsync(a => a.CategoryCode == category.Code && a.TenantLabId == request.TenantLabId, cancellationToken).ConfigureAwait(false);
        if (!labHandlesCategory) return ($"實驗室「{lab.Name}」未承作「{category.Name}」，請先設定承作實驗室。", string.Empty);

        var user = await dbContext.Set<ApplicationUser>().AsNoTracking()
            .Where(u => u.Id == request.UserId && !u.IsDeleted)
            .Select(u => new { u.UserName, u.FullName })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (user is null) return ("找不到指定的使用者。", string.Empty);

        bool overlaps = await dbContext.Set<SignerQualification>().AsNoTracking()
            .AnyAsync(q => q.UserId == request.UserId && q.CategoryCode == category.Code && q.TenantLabId == request.TenantLabId &&
                           q.RevokedAt == null && q.ValidFrom <= request.ValidTo && q.ValidTo >= request.ValidFrom, cancellationToken)
            .ConfigureAwait(false);
        if (overlaps) return ("此使用者於該類別與實驗室已有期間重疊的簽署資格，請調整期間或先撤銷既有資格。", string.Empty);

        var display = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;
        return (null, $"簽署人資格：「{display}」於「{category.Name}」/{lab.Name}（證號 {certificateNo}，{request.ValidFrom:yyyy-MM-dd} ～ {request.ValidTo:yyyy-MM-dd}）");
    }

    private async Task<SignerQualification> ApplyQualificationAsync(CreateSignerQualificationRequest request, CancellationToken cancellationToken)
    {
        var category = await FindCategoryAsync(request.CategoryCode, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("報告類別不存在。");

        var entity = new SignerQualification
        {
            UserId = request.UserId,
            CategoryCode = category.Code,
            TenantLabId = request.TenantLabId,
            CertificateNo = request.CertificateNo.Trim(),
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
            CreatedBy = CurrentActor().Name
        };
        dbContext.Set<SignerQualification>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await permissionChangeNotifier.NotifyUsersAsync([entity.UserId.ToString()], "signer-qualification-created", cancellationToken).ConfigureAwait(false);
        return entity;
    }

    private async Task<ReportGrantChangeRequest> CreateChangeRequestAsync<TPayload>(
        ReportGrantChangeType type, TPayload payload, string summary, Guid? subjectUserId, string? reason, CancellationToken cancellationToken)
    {
        var (actorId, actorName) = CurrentActor();
        var entity = new ReportGrantChangeRequest
        {
            RequestType = type,
            PayloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions),
            Summary = summary,
            SubjectUserId = subjectUserId,
            RequestReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            RequestedBy = actorId ?? Guid.Empty,
            RequestedByName = actorName,
            RequestedAt = timeProvider.GetUtcNow(),
            CreatedBy = actorName
        };
        dbContext.Set<ReportGrantChangeRequest>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[ReportCategory] {Actor} 送出授權申請 {Id}：{Summary}", actorName, entity.Id, summary);
        return entity;
    }

    /// <summary>
    /// 審核者限制：申請人不得核准自己的申請（最高管理者除外，並於審核意見標註）；受授權者不得核准給自己的授權（無例外）。
    /// </summary>
    private static string? ReviewBlockReason(ReportGrantChangeRequest request, Guid? actorId, bool isSuperAdmin)
    {
        if (!actorId.HasValue) return "無法識別審核者身分。";
        if (request.SubjectUserId == actorId.Value) return "職能分立：不得核准授予自己的權限或簽署資格。";
        if (request.RequestedBy == actorId.Value && !isSuperAdmin) return "雙重審批規定：申請人不得核准自己的申請，請由其他審核者處理。";
        return null;
    }

    private async Task NotifyPrincipalAsync(GrantPrincipalType type, Guid principalId, string reason, CancellationToken cancellationToken)
    {
        if (type == GrantPrincipalType.User)
        {
            await permissionChangeNotifier.NotifyUsersAsync([principalId.ToString()], reason, cancellationToken).ConfigureAwait(false);
            return;
        }

        var roleName = await dbContext.Set<ApplicationRole>().AsNoTracking()
            .Where(r => r.Id == principalId).Select(r => r.Name)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            await permissionChangeNotifier.NotifyRoleMembersAsync(roleName, reason, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<ReportCategory?> FindCategoryAsync(string? code, CancellationToken cancellationToken)
    {
        var normalized = code?.Trim().ToUpperInvariant() ?? string.Empty;
        return dbContext.Set<ReportCategory>().AsNoTracking().FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);
    }

    private async Task<Dictionary<(GrantPrincipalType, Guid), string>> ResolvePrincipalNamesAsync(
        IEnumerable<(GrantPrincipalType Type, Guid Id)> principals, CancellationToken cancellationToken)
    {
        var list = principals.Distinct().ToList();
        var userIds = list.Where(p => p.Type == GrantPrincipalType.User).Select(p => p.Id).ToList();
        var roleIds = list.Where(p => p.Type == GrantPrincipalType.Role).Select(p => p.Id).ToList();

        var result = new Dictionary<(GrantPrincipalType, Guid), string>();
        if (userIds.Count > 0)
        {
            var users = await dbContext.Set<ApplicationUser>().AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName, u.FullName })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var u in users)
            {
                result[(GrantPrincipalType.User, u.Id)] = string.IsNullOrWhiteSpace(u.FullName) ? u.UserName ?? u.Id.ToString() : u.FullName;
            }
        }

        if (roleIds.Count > 0)
        {
            var roles = await dbContext.Set<ApplicationRole>().AsNoTracking()
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Name })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var r in roles)
            {
                result[(GrantPrincipalType.Role, r.Id)] = r.Name ?? r.Id.ToString();
            }
        }

        return result;
    }

    private async Task<Dictionary<Guid, string>> ResolveLabNamesAsync(IEnumerable<Guid> labIds, CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var labId in labIds.Distinct())
        {
            var org = await orgIntegrationService.GetOrganizationByIdAsync(labId, cancellationToken).ConfigureAwait(false);
            names[labId] = org?.Name ?? labId.ToString()[..8];
        }
        return names;
    }

    private static string StatusOf(DateTimeOffset? revokedAt, DateTimeOffset from, DateTimeOffset? to, DateTimeOffset now) =>
        revokedAt is not null ? LabAuditStatuses.Revoked
        : to.HasValue && to.Value < now ? LabAuditStatuses.Expired
        : from > now ? LabAuditStatuses.Upcoming
        : LabAuditStatuses.Active;

    private static ReportCategoryDto ToDto(ReportCategory c, IReadOnlyList<Guid> labIds) => new(
        c.Id, c.Code, c.Name, c.Description, c.DefaultConfidentiality, c.RegulationTags, c.DisplayOrder, c.IsActive,
        labIds, Convert.ToBase64String(c.RowVersion ?? []));

    private bool IsCurrentSuperAdmin()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        return principal is not null && AuthorizationClaimHelper.IsSuperAdmin(principal);
    }

    private (Guid? Id, string Name) CurrentActor()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        return (principal is null ? null : AuthorizationClaimHelper.ResolveUserId(principal),
                principal?.Identity?.Name ?? principal?.FindFirst(ClaimTypes.Name)?.Value ?? "未知使用者");
    }
}
