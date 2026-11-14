using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// UAT 驗收簽核單（FORM-UAT-01，SOP 第 5 章）：PM 建立 → 客戶驗收（通過／不通過）。驗收內容以 SHA-256 固化，簽核須對同一雜湊。
/// 客戶外部簽核帳號尚未開放，目前由專案 PM 代錄客戶結果並附原因與依據。專案資料列授權：非內部成員一律 404。
/// </summary>
public sealed class UatService(PMSDbContext db, TimeProvider clock)
{
    private const int MaxPageSize = 200;

    public async Task<PmsResult<UatDetailDto>> CreateAsync(Guid userId, string actor, CreateUatRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var version = DeliverableNaming.NormalizeVersion(request.Version);
        if (version is null) return PmsError.Validation("版本號格式須為 vX.Y 或 vX.Y.Z（例：v1.0）。");
        var invalid = UatRules.ValidateCreate(request.ScopeNote);
        if (invalid is not null) return PmsError.Validation(invalid);

        var project = await db.Projects.AsNoTracking().Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound("專案不存在。");

        var members = project.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        if (ProjectAccessPolicy.Evaluate(userId, members) != ProjectAccessLevel.Internal) return PmsError.NotFound("專案不存在。");
        if (!ProjectAccessPolicy.IsPm(userId, members)) return PmsError.Forbidden("僅專案 PM 可建立 UAT 驗收簽核單（SOP：PM 協調 UAT）。");
        if (project.Status != ProjectStatus.Active)
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "專案已結案或作廢，不可再建立驗收簽核單。");
        if (await db.UatSignoffs.AnyAsync(u => u.ProjectId == project.Id && u.Version == version && u.Status != UatStatus.Cancelled, ct).ConfigureAwait(false))
            return PmsError.Conflict("PMS.UAT_VERSION_EXISTS", $"此專案的版本 {version} 已有進行中或已完成的驗收簽核單（取消後才可重建）。");

        var now = clock.GetUtcNow();
        var uat = new UatSignoff
        {
            Id = Guid.NewGuid(), ProjectId = project.Id, Version = version, ScopeNote = request.ScopeNote.Trim(),
            Status = UatStatus.PendingSign, ContentHash = UatRules.ComputeHash(project.Code, version, request.ScopeNote),
            CreatedByUserId = userId, CreatedBy = actor, CreatedAtUtc = now
        };
        db.UatSignoffs.Add(uat);

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.UAT_VERSION_EXISTS", $"此專案的版本 {version} 已有進行中或已完成的驗收簽核單。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, uat.Id, ct).ConfigureAwait(false);
    }

    public async Task<PagedResult<UatListDto>> SearchAsync(Guid userId, UatSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var filter = request.Filter ?? new UatSearchFilter();

        var projectIds = await db.ProjectMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.RaciRole != RaciRole.Stakeholder)
            .Select(m => m.ProjectId).Distinct().ToListAsync(ct).ConfigureAwait(false);

        var query = db.UatSignoffs.AsNoTracking().Where(u => projectIds.Contains(u.ProjectId));
        if (filter.ProjectId is { } projectId) query = query.Where(u => u.ProjectId == projectId);
        if (filter.Status is { Count: > 0 } statuses) query = query.Where(u => statuses.Contains(u.Status));
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(u => u.Version.Contains(term) || u.Project!.Code.Contains(term) || u.Project!.Name.Contains(term));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query.OrderByDescending(u => u.CreatedAtUtc)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(u => new UatListDto(u.Id, u.ProjectId, u.Project!.Code, u.Project!.Name, u.Version, u.Status, u.CreatedAtUtc, u.ResultAt, u.ResultIsProxy))
            .ToListAsync(ct).ConfigureAwait(false);

        return new PagedResult<UatListDto>(items, total, pageIndex, pageSize);
    }

    public async Task<PmsResult<UatDetailDto>> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, id, track: false, ct).ConfigureAwait(false);
        return ctx.Error is { } error ? error : PmsResult<UatDetailDto>.Success(ToDetail(ctx.Value!));
    }

    public async Task<PmsResult<UatDetailDto>> RecordResultAsync(Guid userId, string actor, Guid id, RecordUatResultRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        if (!ProjectAccessPolicy.IsPm(userId, c.Members)) return PmsError.Forbidden("僅專案 PM 可代錄客戶驗收結果。");

        var verdict = UatRules.ValidateRecord(c.Uat.Status, request.Result, request.ProxyReason, request.Comment);
        if (!verdict.IsOk) return verdict.Detail is not null && c.Uat.Status != UatStatus.PendingSign
            ? PmsError.Conflict("PMS.INVALID_TRANSITION", verdict.Detail)
            : PmsError.Validation(verdict.Detail ?? "驗收結果不正確。");

        if (!string.Equals(c.Uat.ContentHash, request.ContentHash?.Trim(), StringComparison.OrdinalIgnoreCase))
            return PmsError.Conflict("PMS.CONTENT_LOCKED", "簽核內容與目前版本不符，請重新載入後再簽核。");

        var now = clock.GetUtcNow();
        c.Uat.Status = request.Result;
        c.Uat.ResultAt = now;
        c.Uat.ResultByUserId = userId;
        c.Uat.ResultIsProxy = true;
        c.Uat.ProxyReason = request.ProxyReason!.Trim();
        c.Uat.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        Touch(c.Uat, actor, now);
        return await SaveAsync(userId, c.Uat.Id, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<UatDetailDto>> CancelAsync(Guid userId, string actor, Guid id, CancelUatRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        if (!ProjectAccessPolicy.IsPm(userId, c.Members)) return PmsError.Forbidden("僅專案 PM 可取消驗收簽核單。");

        var verdict = UatRules.ValidateCancel(c.Uat.Status, request.Reason);
        if (!verdict.IsOk) return c.Uat.Status != UatStatus.PendingSign
            ? PmsError.Conflict("PMS.INVALID_TRANSITION", verdict.Detail!)
            : PmsError.Validation(verdict.Detail!);

        var now = clock.GetUtcNow();
        c.Uat.Status = UatStatus.Cancelled;
        c.Uat.Comment = request.Reason.Trim();
        Touch(c.Uat, actor, now);
        return await SaveAsync(userId, c.Uat.Id, ct).ConfigureAwait(false);
    }

    // ---------- 內部輔助 ----------

    private sealed record Context(UatSignoff Uat, Project Project, IReadOnlyList<(Guid UserId, RaciRole Role)> Members, Guid UserId);

    private async Task<PmsResult<Context>> LoadAsync(Guid userId, Guid id, bool track, CancellationToken ct)
    {
        var query = track ? db.UatSignoffs.AsQueryable() : db.UatSignoffs.AsNoTracking();
        var uat = await query.Include(u => u.Attachments.Where(a => !a.IsDeleted)).FirstOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false);
        if (uat is null) return PmsError.NotFound();

        var project = await db.Projects.AsNoTracking().Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == uat.ProjectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound();

        var members = project.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        if (ProjectAccessPolicy.Evaluate(userId, members) != ProjectAccessLevel.Internal) return PmsError.NotFound();

        return PmsResult<Context>.Success(new Context(uat, project, members, userId));
    }

    private async Task<PmsResult<Context>> LoadForActionAsync(Guid userId, Guid id, string? rowVersion, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        if (!TryParseRowVersion(rowVersion, out var bytes)) return PmsError.Validation("rowVersion 格式不正確。");
        db.Entry(ctx.Value!.Uat).Property(x => x.RowVersion).OriginalValue = bytes;
        return ctx;
    }

    private async Task<PmsResult<UatDetailDto>> SaveAsync(Guid userId, Guid id, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "驗收簽核單已被他人更新，請重新載入後再試。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, id, ct).ConfigureAwait(false);
    }

    private static UatDetailDto ToDetail(Context c)
    {
        var u = c.Uat;
        var isPm = ProjectAccessPolicy.IsPm(c.UserId, c.Members);
        var pending = u.Status == UatStatus.PendingSign;
        return new UatDetailDto(
            u.Id, u.ProjectId, c.Project.Code, c.Project.Name, u.Version, u.ScopeNote, u.Status, u.ContentHash, u.CreatedByUserId, u.CreatedAtUtc,
            u.ResultAt, u.ResultByUserId, u.ResultIsProxy, u.ProxyReason, u.Comment,
            u.Attachments.OrderBy(a => a.CreatedAtUtc).Select(UatAttachmentService.ToDto).ToList(),
            new UatActionsDto(
                CanRecordResult: isPm && pending, CanCancel: isPm && pending,
                CanUpload: CanManageAttachments(c) && u.Status != UatStatus.Cancelled,
                CanDeleteAttachment: CanManageAttachments(c) && pending),
            Convert.ToBase64String(u.RowVersion));
    }

    /// <summary>附件由專案 PM／Tech Lead 管理（上傳、確認遮罩、刪除）；其餘內部成員僅能檢視與下載。</summary>
    internal static bool CanManageAttachments(Guid userId, IReadOnlyList<(Guid UserId, RaciRole Role)> members) =>
        ProjectAccessPolicy.IsPm(userId, members) || ProjectAccessPolicy.IsTechLead(userId, members);

    private static bool CanManageAttachments(Context c) => CanManageAttachments(c.UserId, c.Members);

    private static void Touch(UatSignoff u, string actor, DateTimeOffset now)
    {
        u.UpdatedBy = actor;
        u.UpdatedAtUtc = now;
    }

    private static bool TryParseRowVersion(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { bytes = Convert.FromBase64String(value); return bytes.Length > 0; }
        catch (FormatException) { return false; }
    }
}
