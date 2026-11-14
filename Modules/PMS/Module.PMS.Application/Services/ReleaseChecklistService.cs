using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 上線前資安與發布檢核表（FORM-SEC-01，SOP 附錄 A）：BE／FE 逐項檢核 → 交叉驗證（非主要作者）→ Tech Lead 放行
/// （阻斷項全數通過；警告項未完成須附例外備註；放行同時通過專案 Release Gate）→ PM 確認。放行後內容鎖定。
/// 專案資料列授權：非專案內部成員一律 404。
/// </summary>
public sealed class ReleaseChecklistService(PMSDbContext db, TimeProvider clock)
{
    private const int MaxPageSize = 200;

    // ---------- 建立 ----------

    public async Task<PmsResult<ReleaseDetailDto>> CreateAsync(Guid userId, string actor, CreateReleaseRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var version = DeliverableNaming.NormalizeVersion(request.Version);
        if (version is null) return PmsError.Validation("版本號格式須為 vX.Y 或 vX.Y.Z（例：v1.2.3）。");
        if (request.PlannedDate == default) return PmsError.Validation("預計上線日為必填。");

        var project = await db.Projects.AsNoTracking().Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound("專案不存在。");

        var members = project.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        if (ProjectAccessPolicy.Evaluate(userId, members) != ProjectAccessLevel.Internal) return PmsError.NotFound("專案不存在。");
        if (project.Status != ProjectStatus.Active)
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "專案已結案或作廢，不可再建立上線檢核表。");
        if (!members.Any(m => m.UserId == request.MainAuthorUserId && m.RaciRole is RaciRole.Be or RaciRole.Fe or RaciRole.TechLead))
            return PmsError.Validation("本次變更主要作者須為專案的 BE／FE／Tech Lead 成員。");
        if (await db.ReleaseChecklists.AnyAsync(r => r.ProjectId == project.Id && r.Version == version, ct).ConfigureAwait(false))
            return PmsError.Conflict("PMS.RELEASE_VERSION_EXISTS", $"此專案的版本 {version} 已建立過上線檢核表。");

        var now = clock.GetUtcNow();
        var checklist = new ReleaseChecklist
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Version = version,
            PlannedDate = request.PlannedDate,
            MainAuthorUserId = request.MainAuthorUserId,
            Status = ReleaseChecklistStatus.Draft,
            CreatedByUserId = userId,
            CreatedBy = actor,
            CreatedAtUtc = now
        };
        foreach (var t in ReleaseChecklistTemplate.Items)
            checklist.Items.Add(new ReleaseChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist.Id, Code = t.Code, Severity = t.Severity, Result = ReleaseItemResult.Pending });
        db.ReleaseChecklists.Add(checklist);

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.RELEASE_VERSION_EXISTS", $"此專案的版本 {version} 已建立過上線檢核表。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, checklist.Id, ct).ConfigureAwait(false);
    }

    // ---------- 查詢 ----------

    public async Task<PagedResult<ReleaseListDto>> SearchAsync(Guid userId, ReleaseSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var filter = request.Filter ?? new ReleaseSearchFilter();

        var projectIds = await db.ProjectMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.RaciRole != RaciRole.Stakeholder)
            .Select(m => m.ProjectId).Distinct().ToListAsync(ct).ConfigureAwait(false);

        var query = db.ReleaseChecklists.AsNoTracking().Where(r => projectIds.Contains(r.ProjectId));
        if (filter.ProjectId is { } projectId) query = query.Where(r => r.ProjectId == projectId);
        if (filter.Status is { Count: > 0 } statuses) query = query.Where(r => statuses.Contains(r.Status));

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(r => r.Version.Contains(term) || r.Project!.Code.Contains(term) || r.Project!.Name.Contains(term));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var rows = await query.OrderByDescending(r => r.CreatedAtUtc)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, r.ProjectId, ProjectCode = r.Project!.Code, ProjectName = r.Project!.Name, r.Version, r.PlannedDate, r.Status, r.CreatedAtUtc,
                Items = r.Items.Select(i => new { i.Code, i.Severity, i.Result }).ToList(),
                Signed = r.Signoffs.Select(s => s.Role).ToList()
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var items = rows.Select(r =>
        {
            var open = ReleaseChecklistRules.OpenItems(r.Items.Select(i => (i.Code, i.Severity, i.Result)));
            return new ReleaseListDto(
                r.Id, r.ProjectId, r.ProjectCode, r.ProjectName, r.Version, r.PlannedDate, r.Status,
                r.Items.Count, r.Items.Count(i => i.Result == ReleaseItemResult.Pass), open.Blocking.Count, open.Warning.Count,
                r.Signed.OrderBy(s => s).ToList(), r.CreatedAtUtc);
        }).ToList();

        return new PagedResult<ReleaseListDto>(items, total, pageIndex, pageSize);
    }

    public async Task<PmsResult<ReleaseDetailDto>> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, id, track: false, ct).ConfigureAwait(false);
        return ctx.Error is { } error ? error : PmsResult<ReleaseDetailDto>.Success(ToDetail(ctx.Value!));
    }

    // ---------- 更新檢核項 ----------

    public async Task<PmsResult<ReleaseDetailDto>> UpdateItemAsync(
        Guid userId, string actor, Guid id, string code, UpdateReleaseItemRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        var item = c.Checklist.Items.FirstOrDefault(i => string.Equals(i.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (item is null) return PmsError.NotFound("無此檢核項。");

        var invalid = ReleaseChecklistRules.ValidateItemUpdate(c.Checklist.Status, item.Severity, request.Result, request.Note);
        if (invalid is not null)
        {
            return c.Checklist.Status != ReleaseChecklistStatus.Draft
                ? PmsError.Conflict("PMS.CONTENT_LOCKED", invalid)
                : PmsError.Validation(invalid);
        }

        var now = clock.GetUtcNow();
        item.Result = request.Result;
        item.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        item.CheckedBy = userId;
        item.CheckedByName = actor.Length > 256 ? actor[..256] : actor;
        item.CheckedAt = now;
        Touch(c.Checklist, actor, now);

        return await SaveAsync(userId, c.Checklist.Id, ct).ConfigureAwait(false);
    }

    // ---------- 簽核 ----------

    public async Task<PmsResult<ReleaseDetailDto>> SignAsync(Guid userId, string actor, Guid id, SignReleaseRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var checklist = c.Checklist;

        if (!Enum.IsDefined(request.Role)) return PmsError.Validation("簽核角色不正確。");
        if (request.ExceptionNote is { Length: > 1000 }) return PmsError.Validation("例外放行備註不得超過 1000 字。");

        var open = ReleaseChecklistRules.OpenItems(checklist.Items.Select(i => (i.Code, i.Severity, i.Result)));
        var signed = checklist.Signoffs.Select(s => s.Role).ToList();
        var verdict = ReleaseChecklistRules.ValidateSign(
            request.Role, userId, c.Members, checklist.MainAuthorUserId, checklist.Status, signed, open, request.ExceptionNote);
        if (!verdict.IsOk)
        {
            if (verdict.BlockingCodes is { Count: > 0 } blocking)
                return PmsError.Unprocessable("PMS.RELEASE_BLOCKING_ITEMS", $"尚有阻斷項未通過：{string.Join("、", blocking)}。");
            return ToError(verdict.Result);
        }

        var now = clock.GetUtcNow();
        var note = string.IsNullOrWhiteSpace(request.ExceptionNote) ? null : request.ExceptionNote.Trim();

        if (request.Role == ReleaseSignRole.TechLead)
        {
            // Tech Lead 放行同時通過專案 Release Gate（前道 Verification Gate 須已通過）
            var current = c.Project.Gates.ToDictionary(g => g.Gate, g => g.Status);
            var gateVerdict = GateRule.CanDecide(GateKind.Release, GateStatus.Approved, current, actorIsTechLead: true);
            if (!gateVerdict.IsOk) return ToError(gateVerdict);

            var gate = c.Project.Gates.FirstOrDefault(g => g.Gate == GateKind.Release);
            if (gate is null)
            {
                gate = new ProjectGate { Id = Guid.NewGuid(), ProjectId = c.Project.Id, Gate = GateKind.Release, CreatedBy = actor, CreatedAtUtc = now };
                db.ProjectGates.Add(gate);
            }

            gate.Status = GateStatus.Approved;
            gate.ApprovedBy = userId;
            gate.ApprovedAt = now;
            gate.EvidenceNote = $"上線檢核表 {checklist.Version}（Tech Lead 放行）";
            gate.UpdatedBy = actor;
            gate.UpdatedAtUtc = now;

            checklist.Status = ReleaseChecklistStatus.Released;
            checklist.ExceptionNote = note;
        }
        else if (request.Role == ReleaseSignRole.Pm)
        {
            checklist.Status = ReleaseChecklistStatus.Confirmed;
        }

        db.ReleaseSignoffs.Add(new ReleaseSignoff
        {
            Id = Guid.NewGuid(), ChecklistId = checklist.Id, Role = request.Role, SignerUserId = userId,
            SignerName = actor.Length > 256 ? actor[..256] : actor, SignedAt = now, ExceptionNote = request.Role == ReleaseSignRole.TechLead ? note : null
        });
        Touch(checklist, actor, now);

        return await SaveAsync(userId, checklist.Id, ct).ConfigureAwait(false);
    }

    // ---------- 內部輔助 ----------

    private sealed record Context(
        ReleaseChecklist Checklist, Project Project, IReadOnlyList<(Guid UserId, RaciRole Role)> Members, Guid UserId);

    private async Task<PmsResult<Context>> LoadAsync(Guid userId, Guid id, bool track, CancellationToken ct)
    {
        var query = track ? db.ReleaseChecklists.AsQueryable() : db.ReleaseChecklists.AsNoTracking();
        var checklist = await query.Include(r => r.Items).Include(r => r.Signoffs).FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
        if (checklist is null) return PmsError.NotFound();

        var projectQuery = track ? db.Projects.AsQueryable() : db.Projects.AsNoTracking();
        var project = await projectQuery.Include(p => p.Members).Include(p => p.Gates)
            .FirstOrDefaultAsync(p => p.Id == checklist.ProjectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound();

        var members = project.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        if (ProjectAccessPolicy.Evaluate(userId, members) != ProjectAccessLevel.Internal) return PmsError.NotFound();

        return PmsResult<Context>.Success(new Context(checklist, project, members, userId));
    }

    private async Task<PmsResult<Context>> LoadForActionAsync(Guid userId, Guid id, string? rowVersion, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        if (!TryParseRowVersion(rowVersion, out var bytes)) return PmsError.Validation("rowVersion 格式不正確。");
        db.Entry(ctx.Value!.Checklist).Property(x => x.RowVersion).OriginalValue = bytes;
        return ctx;
    }

    private async Task<PmsResult<ReleaseDetailDto>> SaveAsync(Guid userId, Guid id, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "檢核表已被他人更新，請重新載入後再試。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, id, ct).ConfigureAwait(false);
    }

    private static ReleaseDetailDto ToDetail(Context c)
    {
        var r = c.Checklist;
        var open = ReleaseChecklistRules.OpenItems(r.Items.Select(i => (i.Code, i.Severity, i.Result)));
        var signed = r.Signoffs.Select(s => s.Role).ToList();
        var releaseGate = c.Project.Gates.FirstOrDefault(g => g.Gate == GateKind.Release)?.Status ?? GateStatus.Pending;

        var items = ReleaseChecklistTemplate.Items.Select(t =>
        {
            var item = r.Items.FirstOrDefault(i => string.Equals(i.Code, t.Code, StringComparison.OrdinalIgnoreCase));
            return new ReleaseItemDto(
                t.Code, t.Section, t.Title, t.Standard, item?.Severity ?? t.Severity, item?.Result ?? ReleaseItemResult.Pending,
                item?.Note, item?.CheckedByName, item?.CheckedAt);
        }).ToList();

        var actions = new ReleaseActionsDto(
            CanEditItems: r.Status == ReleaseChecklistStatus.Draft,
            SignableRoles: ReleaseChecklistRules.SignableRoles(c.UserId, c.Members, r.MainAuthorUserId, r.Status, signed));

        return new ReleaseDetailDto(
            r.Id, r.ProjectId, c.Project.Code, c.Project.Name, r.Version, r.PlannedDate, r.MainAuthorUserId, r.Status, r.ExceptionNote, releaseGate,
            open.Blocking, open.Warning, items,
            r.Signoffs.OrderBy(s => s.SignedAt).Select(s => new ReleaseSignoffDto(s.Role, s.SignerName, s.SignedAt, s.ExceptionNote)).ToList(),
            actions, Convert.ToBase64String(r.RowVersion));
    }

    private static void Touch(ReleaseChecklist r, string actor, DateTimeOffset now)
    {
        r.UpdatedBy = actor;
        r.UpdatedAtUtc = now;
    }

    private static PmsError ToError(RuleResult result) => result.Outcome switch
    {
        RuleOutcome.SeparationOfDuties => PmsError.Conflict("PMS.SOD_VIOLATION", result.Detail ?? "違反角色分離。"),
        RuleOutcome.RoleNotAllowed => PmsError.Forbidden(result.Detail ?? "角色不符。"),
        RuleOutcome.GatePrerequisiteNotMet => PmsError.Unprocessable("PMS.GATE_PREREQUISITE_NOT_MET", result.Detail ?? "前道卡控節點尚未通過。"),
        _ => PmsError.Conflict("PMS.INVALID_TRANSITION", result.Detail ?? "狀態不允許該轉移。")
    };

    private static bool TryParseRowVersion(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { bytes = Convert.FromBase64String(value); return bytes.Length > 0; }
        catch (FormatException) { return false; }
    }
}
