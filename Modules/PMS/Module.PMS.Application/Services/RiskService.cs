using Microsoft.EntityFrameworkCore;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 專案風險管理表（SOP 3.1）：PM／Tech Lead 建立、編輯、作廢；風險負責人（BE／FE）更新自己負責風險的狀態與因應措施。
/// 僅專案內部成員可見，客戶 Stakeholder 與非成員一律 404／不列出。作廢為軟刪除，保留稽核。
/// </summary>
public sealed class RiskService(PMSDbContext db, TimeProvider clock)
{
    private const int MaxPageSize = 200;

    // ---------- 查詢 ----------

    public async Task<PagedResult<RiskDto>> SearchAsync(Guid userId, RiskSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var filter = request.Filter ?? new RiskSearchFilter();

        // 我所屬專案（內部成員）及我在各專案的角色，供逐筆計算可執行動作
        var memberships = await db.ProjectMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.RaciRole != RaciRole.Stakeholder)
            .Select(m => new { m.ProjectId, m.RaciRole }).ToListAsync(ct).ConfigureAwait(false);
        var projectIds = memberships.Select(m => m.ProjectId).Distinct().ToList();
        var leaderProjects = memberships.Where(m => m.RaciRole is RaciRole.Pm or RaciRole.TechLead).Select(m => m.ProjectId).ToHashSet();

        var query = db.RiskItems.AsNoTracking().Where(r => !r.IsVoided && projectIds.Contains(r.ProjectId));
        if (filter.ProjectId is { } projectId) query = query.Where(r => r.ProjectId == projectId);
        if (filter.Status is { Count: > 0 } statuses) query = query.Where(r => statuses.Contains(r.Status));
        if (filter.Level is { Count: > 0 } levels) query = query.Where(r => levels.Contains(r.Level));
        if (filter.OwnedByMe) query = query.Where(r => r.OwnerUserId == userId);
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(r => r.Title.Contains(term) || (r.Mitigation != null && r.Mitigation.Contains(term)) || r.Project!.Code.Contains(term));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var rows = await query
            .OrderBy(r => r.Status == RiskStatus.Closed ? 1 : 0)   // 未結案優先
            .ThenByDescending(r => r.Level)                          // 等級高者優先
            .ThenByDescending(r => r.CreatedAtUtc)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, r.ProjectId, ProjectCode = r.Project!.Code, ProjectName = r.Project!.Name, r.Title, r.Level, r.Status,
                r.Mitigation, r.OwnerUserId, r.ClosedAt, r.CreatedBy, r.CreatedAtUtc, r.UpdatedAtUtc, r.RowVersion
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var items = rows.Select(r =>
        {
            var leader = leaderProjects.Contains(r.ProjectId);
            var owner = r.OwnerUserId == userId;
            return new RiskDto(
                r.Id, r.ProjectId, r.ProjectCode, r.ProjectName, r.Title, r.Level, r.Status, r.Mitigation, r.OwnerUserId, r.ClosedAt,
                r.CreatedBy, r.CreatedAtUtc, r.UpdatedAtUtc,
                CanEdit: leader || owner, CanEditAll: leader, CanVoid: leader, Convert.ToBase64String(r.RowVersion));
        }).ToList();

        return new PagedResult<RiskDto>(items, total, pageIndex, pageSize);
    }

    // ---------- 建立 ----------

    public async Task<PmsResult<RiskDto>> CreateAsync(Guid userId, string actor, CreateRiskRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var status = request.Status ?? RiskStatus.Open;
        var invalid = RiskRules.ValidateFields(request.Title, request.Level, status, request.Mitigation);
        if (invalid is not null) return PmsError.Validation(invalid);

        var project = await db.Projects.AsNoTracking().Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound("專案不存在。");

        var members = project.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        if (ProjectAccessPolicy.Evaluate(userId, members) != ProjectAccessLevel.Internal) return PmsError.NotFound("專案不存在。");
        if (!RiskRules.CanVoid(userId, members)) return PmsError.Forbidden("僅專案 PM 或 Tech Lead 可建立風險。");
        if (project.Status != ProjectStatus.Active) return PmsError.Conflict("PMS.INVALID_TRANSITION", "專案已結案或作廢，不可再新增風險。");
        if (!RiskRules.IsValidOwner(request.OwnerUserId, members)) return PmsError.Validation("風險負責人須為專案內部成員（PM、Tech Lead、BE、FE）。");

        var now = clock.GetUtcNow();
        var risk = new RiskItem
        {
            Id = Guid.NewGuid(), ProjectId = project.Id, Title = request.Title.Trim(), Level = request.Level, Status = status,
            Mitigation = string.IsNullOrWhiteSpace(request.Mitigation) ? null : request.Mitigation.Trim(),
            OwnerUserId = request.OwnerUserId, ClosedAt = status == RiskStatus.Closed ? now : null,
            CreatedBy = actor, CreatedAtUtc = now
        };
        db.RiskItems.Add(risk);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        db.ChangeTracker.Clear();
        return await GetAsync(userId, risk.Id, ct).ConfigureAwait(false);
    }

    // ---------- 更新 ----------

    public async Task<PmsResult<RiskDto>> UpdateAsync(Guid userId, string actor, Guid id, UpdateRiskRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        var scope = RiskRules.EditScope(userId, c.Risk.OwnerUserId, c.Members);
        if (scope == RiskEditScope.None) return PmsError.Forbidden("僅專案 PM、Tech Lead 或此風險的負責人可更新風險。");

        if (!TryParseRowVersion(request.RowVersion, out var bytes)) return PmsError.Validation("rowVersion 格式不正確。");
        db.Entry(c.Risk).Property(x => x.RowVersion).OriginalValue = bytes;

        // 負責人身分的更新只認狀態與因應措施，其餘欄位一律沿用原值（即使請求帶了不同值）
        var title = scope == RiskEditScope.Full ? request.Title : c.Risk.Title;
        var level = scope == RiskEditScope.Full ? request.Level : c.Risk.Level;
        var ownerId = scope == RiskEditScope.Full ? request.OwnerUserId : c.Risk.OwnerUserId;

        var invalid = RiskRules.ValidateFields(title, level, request.Status, request.Mitigation);
        if (invalid is not null) return PmsError.Validation(invalid);
        if (ownerId != c.Risk.OwnerUserId && !RiskRules.IsValidOwner(ownerId, c.Members))
            return PmsError.Validation("風險負責人須為專案內部成員（PM、Tech Lead、BE、FE）。");

        var now = clock.GetUtcNow();
        c.Risk.ClosedAt = RiskRules.NextClosedAt(c.Risk.Status, request.Status, c.Risk.ClosedAt, now);
        c.Risk.Title = title.Trim();
        c.Risk.Level = level;
        c.Risk.OwnerUserId = ownerId;
        c.Risk.Status = request.Status;
        c.Risk.Mitigation = string.IsNullOrWhiteSpace(request.Mitigation) ? null : request.Mitigation.Trim();
        c.Risk.UpdatedBy = actor;
        c.Risk.UpdatedAtUtc = now;

        return await SaveAsync(userId, id, ct).ConfigureAwait(false);
    }

    // ---------- 作廢 ----------

    /// <summary>作廢（軟刪除）：僅專案 PM／Tech Lead；保留稽核，不實體刪除。</summary>
    public async Task<PmsResult<bool>> VoidAsync(Guid userId, string actor, Guid id, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        if (!RiskRules.CanVoid(userId, c.Members)) return PmsError.Forbidden("僅專案 PM 或 Tech Lead 可作廢風險。");

        var now = clock.GetUtcNow();
        c.Risk.IsVoided = true;
        c.Risk.VoidedAt = now;
        c.Risk.VoidedBy = userId;
        c.Risk.UpdatedBy = actor;
        c.Risk.UpdatedAtUtc = now;

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "風險已被他人更新，請重新載入後再試。");
        }

        db.ChangeTracker.Clear();
        return PmsResult<bool>.Success(true);
    }

    // ---------- 內部輔助 ----------

    private sealed record Context(RiskItem Risk, Project Project, IReadOnlyList<(Guid UserId, RaciRole Role)> Members, Guid UserId);

    public async Task<PmsResult<RiskDto>> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, id, track: false, ct).ConfigureAwait(false);
        return ctx.Error is { } error ? error : PmsResult<RiskDto>.Success(ToDto(ctx.Value!));
    }

    private async Task<PmsResult<Context>> LoadAsync(Guid userId, Guid id, bool track, CancellationToken ct)
    {
        var query = track ? db.RiskItems.AsQueryable() : db.RiskItems.AsNoTracking();
        var risk = await query.FirstOrDefaultAsync(r => r.Id == id && !r.IsVoided, ct).ConfigureAwait(false);
        if (risk is null) return PmsError.NotFound("風險不存在。");

        var project = await db.Projects.AsNoTracking().Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == risk.ProjectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound("風險不存在。");

        var members = project.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        if (ProjectAccessPolicy.Evaluate(userId, members) != ProjectAccessLevel.Internal) return PmsError.NotFound("風險不存在。");

        return PmsResult<Context>.Success(new Context(risk, project, members, userId));
    }

    private async Task<PmsResult<RiskDto>> SaveAsync(Guid userId, Guid id, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "風險已被他人更新，請重新載入後再試。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, id, ct).ConfigureAwait(false);
    }

    private static RiskDto ToDto(Context c)
    {
        var r = c.Risk;
        var scope = RiskRules.EditScope(c.UserId, r.OwnerUserId, c.Members);
        return new RiskDto(
            r.Id, r.ProjectId, c.Project.Code, c.Project.Name, r.Title, r.Level, r.Status, r.Mitigation, r.OwnerUserId, r.ClosedAt,
            r.CreatedBy, r.CreatedAtUtc, r.UpdatedAtUtc,
            CanEdit: scope != RiskEditScope.None, CanEditAll: scope == RiskEditScope.Full, CanVoid: RiskRules.CanVoid(c.UserId, c.Members),
            Convert.ToBase64String(r.RowVersion));
    }

    private static bool TryParseRowVersion(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { bytes = Convert.FromBase64String(value); return bytes.Length > 0; }
        catch (FormatException) { return false; }
    }
}
