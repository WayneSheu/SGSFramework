using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>稽核指派狀態</summary>
public static class LabAuditStatuses
{
    public const string Upcoming = "upcoming";
    public const string Active = "active";
    public const string Expired = "expired";
    public const string Revoked = "revoked";
}

public sealed record LabAuditAssignmentDto(
    Guid Id,
    string UserId,
    string UserName,
    string FullName,
    Guid TenantLabId,
    string LabName,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveTo,
    string Reason,
    string Status,
    string? AssignedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt,
    string? RevokedByName,
    string? RevokeReason,
    DateTimeOffset? UpdatedAt = null,
    string? UpdatedByName = null,
    string? UpdateNote = null);

/// <summary>可被指派稽核的使用者（持有啟用中的稽核角色）</summary>
public sealed record AuditorCandidateDto(string UserId, string UserName, string FullName, string? Email, IReadOnlyList<string> AuditRoles);

public sealed record CreateLabAuditRequest(Guid UserId, Guid TenantLabId, DateTimeOffset EffectiveFrom, DateTimeOffset EffectiveTo, string Reason);

/// <summary>批次新增稽核指派：每筆明細各有稽核員、實驗室、起訖期間與原因</summary>
public sealed record BatchCreateLabAuditRequest(IReadOnlyList<CreateLabAuditRequest> Items);

/// <summary>批次明細驗證結果（Index 為 0 起算的序號）；Error 非 null 表示此筆不合格，Warnings 為不阻擋的提醒</summary>
public sealed record BatchLabAuditItemResult(int Index, bool Succeeded, string? Error, IReadOnlyList<string> Warnings);

/// <summary>批次新增結果；全有全無：任一筆不合格則整批不寫入（Created = 0）</summary>
public sealed record BatchCreateLabAuditResult(bool Succeeded, string Message, int Created, IReadOnlyList<BatchLabAuditItemResult> Items);

/// <summary>稽核指派異動歷程事件（Action：created / updated / revoked）</summary>
public sealed record LabAuditHistoryDto(Guid Id, string Action, string? ActorName, DateTimeOffset OccurredAt, string Summary);

/// <summary>修改稽核指派：可調整稽核員、實驗室、期間與原因（已撤銷或已到期者不可修改）</summary>
public sealed record UpdateLabAuditRequest(Guid UserId, Guid TenantLabId, DateTimeOffset EffectiveFrom, DateTimeOffset EffectiveTo, string Reason);

/// <summary>
/// 稽核權限 = 稽核角色（全域權限，<see cref="ApplicationRole.RequiresAuditAssignment"/>）＋ 稽核指派（實驗室 × 起訖期間）。
/// 稽核角色的權限僅於有效指派的實驗室生效；指派期間內該實驗室會加入使用者可切換的實驗室清單。
/// </summary>
public interface ILabAuditService
{
    Task<IReadOnlyList<LabAuditAssignmentDto>> ListAsync(Guid? tenantLabId, Guid? userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditorCandidateDto>> GetAuditorCandidatesAsync(CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message, LabAuditAssignmentDto? Assignment)> CreateAsync(CreateLabAuditRequest request, CancellationToken cancellationToken = default);

    /// <summary>批次新增（全有全無）；dryRun 只驗證並回傳逐筆錯誤與警告，不寫入</summary>
    Task<BatchCreateLabAuditResult> BatchCreateAsync(BatchCreateLabAuditRequest request, bool dryRun, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message, LabAuditAssignmentDto? Assignment)> UpdateAsync(Guid id, UpdateLabAuditRequest request, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> RevokeAsync(Guid id, string? reason, CancellationToken cancellationToken = default);

    /// <summary>完整異動歷程（由舊到新）；找不到指派時回傳 null</summary>
    Task<IReadOnlyList<LabAuditHistoryDto>?> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>使用者目前有效的稽核指派（實驗室 → 迄日）</summary>
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetActiveLabsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class LabAuditService(
    DbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IOrganizationIntegrationService orgIntegrationService,
    IPermissionChangeNotifier permissionChangeNotifier,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider,
    ILogger<LabAuditService> logger) : ILabAuditService
{
    /// <summary>單次指派最長期間（避免以稽核指派取代人事歸屬）</summary>
    private static readonly TimeSpan MaxDuration = TimeSpan.FromDays(366);

    /// <summary>單次批次最多明細筆數</summary>
    private const int MaxBatchSize = 100;

    public async Task<IReadOnlyList<LabAuditAssignmentDto>> ListAsync(Guid? tenantLabId, Guid? userId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<LabAuditAssignment>().AsNoTracking();
        if (tenantLabId.HasValue) query = query.Where(a => a.TenantLabId == tenantLabId.Value);
        if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);

        var items = await query.OrderByDescending(a => a.EffectiveFrom).Take(500)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return await ToDtosAsync(items, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AuditorCandidateDto>> GetAuditorCandidatesAsync(CancellationToken cancellationToken = default)
    {
        var auditRoles = await dbContext.Set<ApplicationRole>().AsNoTracking()
            .Where(r => r.RequiresAuditAssignment && !r.IsDeleted && r.Status != RoleStatus.Disabled && r.Name != null)
            .Select(r => r.Name!)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var candidates = new Dictionary<Guid, (ApplicationUser User, List<string> Roles)>();
        foreach (var roleName in auditRoles)
        {
            foreach (var user in await userManager.GetUsersInRoleAsync(roleName).ConfigureAwait(false))
            {
                if (user.IsDeleted) continue;
                if (!candidates.TryGetValue(user.Id, out var entry)) candidates[user.Id] = entry = (user, []);
                entry.Roles.Add(roleName);
            }
        }

        return candidates.Values
            .OrderBy(c => c.User.FullName, StringComparer.CurrentCulture)
            .Select(c => new AuditorCandidateDto(c.User.Id.ToString(), c.User.UserName ?? string.Empty, c.User.FullName, c.User.Email, c.Roles))
            .ToList();
    }

    public async Task<(bool Succeeded, string Message, LabAuditAssignmentDto? Assignment)> CreateAsync(
        CreateLabAuditRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow();
        if (ValidateCreateFields(request, now) is { } fieldError) return (false, fieldError, null);

        var user = await userManager.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false);
        if (user is null || user.IsDeleted) return (false, "找不到指定的使用者。", null);

        // 須持有稽核角色：指派本身不授予權限，權限來自稽核角色
        var candidates = await GetAuditorCandidatesAsync(cancellationToken).ConfigureAwait(false);
        if (candidates.All(c => c.UserId != user.Id.ToString()))
        {
            return (false, $"使用者「{Display(user)}」未持有稽核角色（例：各區實驗室稽核員），請先於角色管理指派。", null);
        }

        var lab = await orgIntegrationService.GetOrganizationByIdAsync(request.TenantLabId, cancellationToken).ConfigureAwait(false);
        if (lab is null) return (false, "找不到指定的區域實驗室。", null);
        if (!lab.IsActive) return (false, $"實驗室「{lab.Name}」已停用，無法指派稽核。", null);

        bool overlaps = await dbContext.Set<LabAuditAssignment>().AsNoTracking()
            .AnyAsync(a => a.UserId == user.Id && a.TenantLabId == request.TenantLabId && a.RevokedAt == null &&
                           a.EffectiveFrom <= request.EffectiveTo && a.EffectiveTo >= request.EffectiveFrom, cancellationToken)
            .ConfigureAwait(false);
        if (overlaps) return (false, "此使用者於該實驗室已有期間重疊的稽核指派，請調整期間或先撤銷既有指派。", null);

        var (actorId, actorName) = CurrentActor();
        var entity = new LabAuditAssignment
        {
            UserId = user.Id,
            TenantLabId = request.TenantLabId,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Reason = request.Reason.Trim(),
            AssignedBy = actorId,
            AssignedByName = actorName,
            CreatedAt = now
        };
        dbContext.Set<LabAuditAssignment>().Add(entity);
        AddHistory(entity.Id, "created", actorId, actorName, now,
            $"指派「{Display(user)}」稽核「{lab.Name}」：{entity.EffectiveFrom.ToLocalTime():yyyy-MM-dd}～{entity.EffectiveTo.ToLocalTime():yyyy-MM-dd}；原因：{entity.Reason}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await permissionChangeNotifier.NotifyUsersAsync([user.Id.ToString()], "lab-audit-assigned", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[LabAudit] {Actor} 指派 {User} 稽核實驗室 {Lab}：{From:yyyy-MM-dd} ～ {To:yyyy-MM-dd}（{Reason}）",
            actorName, user.UserName, lab.Name, entity.EffectiveFrom, entity.EffectiveTo, entity.Reason);

        var dto = (await ToDtosAsync([entity], cancellationToken).ConfigureAwait(false))[0];
        return (true, $"已指派「{Display(user)}」稽核「{lab.Name}」。", dto);
    }

    /// <summary>新增 / 批次新增共用的欄位驗證（不含資料庫查詢）；通過回傳 null</summary>
    private static string? ValidateCreateFields(CreateLabAuditRequest request, DateTimeOffset now)
    {
        if (request.TenantLabId == Guid.Empty) return "實驗室識別碼無效。";
        if (string.IsNullOrWhiteSpace(request.Reason)) return "請填寫稽核原因或稽核案號。";
        if (request.EffectiveTo <= request.EffectiveFrom) return "迄日必須晚於起日。";
        if (request.EffectiveTo - request.EffectiveFrom > MaxDuration) return "單次稽核指派期間不可超過一年。";
        if (request.EffectiveTo < now) return "迄日已過，無法建立已失效的指派。";
        return null;
    }

    private static bool PeriodsOverlap(DateTimeOffset aFrom, DateTimeOffset aTo, DateTimeOffset bFrom, DateTimeOffset bTo) =>
        aFrom <= bTo && aTo >= bFrom;

    private static string PeriodText(DateTimeOffset from, DateTimeOffset to) =>
        $"{from.ToLocalTime():yyyy-MM-dd}～{to.ToLocalTime():yyyy-MM-dd}";

    public async Task<BatchCreateLabAuditResult> BatchCreateAsync(BatchCreateLabAuditRequest request, bool dryRun, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var items = request.Items ?? [];
        if (items.Count == 0) return new BatchCreateLabAuditResult(false, "請至少提供一筆指派明細。", 0, []);
        if (items.Count > MaxBatchSize) return new BatchCreateLabAuditResult(false, $"單次批次最多 {MaxBatchSize} 筆，請分批建立。", 0, []);

        var now = timeProvider.GetUtcNow();
        var errors = new string?[items.Count];
        var warnings = Enumerable.Range(0, items.Count).Select(_ => new List<string>()).ToArray();

        // 快取：批次內重複出現的稽核員 / 實驗室只查詢一次
        var auditorIds = (await GetAuditorCandidatesAsync(cancellationToken).ConfigureAwait(false)).Select(c => c.UserId).ToHashSet();
        var users = new Dictionary<Guid, ApplicationUser?>();
        var labs = new Dictionary<Guid, (string Name, bool IsActive)?>();

        async Task<ApplicationUser?> GetUserAsync(Guid id)
        {
            if (users.TryGetValue(id, out var cached)) return cached;
            var found = await userManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);
            return users[id] = found is { IsDeleted: false } ? found : null;
        }

        async Task<(string Name, bool IsActive)?> GetLabAsync(Guid id)
        {
            if (labs.TryGetValue(id, out var cached)) return cached;
            var org = await orgIntegrationService.GetOrganizationByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return labs[id] = org is null ? null : (org.Name, org.IsActive);
        }

        async Task<string> LabNameAsync(Guid id) => (await GetLabAsync(id))?.Name ?? id.ToString()[..8];

        // 1) 逐筆驗證（欄位、稽核員、稽核角色、實驗室）
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (ValidateCreateFields(item, now) is { } fieldError)
            {
                errors[i] = fieldError;
                continue;
            }

            var user = await GetUserAsync(item.UserId).ConfigureAwait(false);
            if (user is null)
            {
                errors[i] = "找不到指定的使用者。";
                continue;
            }

            if (!auditorIds.Contains(user.Id.ToString()))
            {
                errors[i] = $"使用者「{Display(user)}」未持有稽核角色（例：各區實驗室稽核員），請先於角色管理指派。";
                continue;
            }

            var lab = await GetLabAsync(item.TenantLabId).ConfigureAwait(false);
            if (lab is null) errors[i] = "找不到指定的區域實驗室。";
            else if (!lab.Value.IsActive) errors[i] = $"實驗室「{lab.Value.Name}」已停用，無法指派稽核。";
        }

        // 2) 與資料庫既有（未撤銷）指派比對：單次查詢涵蓋批次內所有稽核員
        var validIdx = Enumerable.Range(0, items.Count).Where(i => errors[i] is null).ToList();
        if (validIdx.Count > 0)
        {
            var userIds = validIdx.Select(i => items[i].UserId).Distinct().ToList();
            var minFrom = validIdx.Min(i => items[i].EffectiveFrom);
            var maxTo = validIdx.Max(i => items[i].EffectiveTo);
            var existing = await dbContext.Set<LabAuditAssignment>().AsNoTracking()
                .Where(a => userIds.Contains(a.UserId) && a.RevokedAt == null && a.EffectiveFrom <= maxTo && a.EffectiveTo >= minFrom)
                .Select(a => new { a.UserId, a.TenantLabId, a.EffectiveFrom, a.EffectiveTo })
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var i in validIdx)
            {
                var item = items[i];
                foreach (var e in existing.Where(e => e.UserId == item.UserId && PeriodsOverlap(e.EffectiveFrom, e.EffectiveTo, item.EffectiveFrom, item.EffectiveTo)))
                {
                    if (e.TenantLabId == item.TenantLabId)
                    {
                        errors[i] ??= "此使用者於該實驗室已有期間重疊的稽核指派，請調整期間或先撤銷既有指派。";
                    }
                    else
                    {
                        // 不同實驗室同期：權限以實驗室為單位生效，技術上不衝突，僅提醒排程
                        warnings[i].Add($"此稽核員同期另在「{await LabNameAsync(e.TenantLabId).ConfigureAwait(false)}」已有稽核指派（{PeriodText(e.EffectiveFrom, e.EffectiveTo)}）。");
                    }
                }
            }

            // 3) 批次內互相比對（資料庫檢查看不到尚未寫入的明細）
            for (int a = 0; a < validIdx.Count; a++)
            {
                for (int b = a + 1; b < validIdx.Count; b++)
                {
                    int i = validIdx[a], j = validIdx[b];
                    var x = items[i];
                    var y = items[j];
                    if (x.UserId != y.UserId || !PeriodsOverlap(x.EffectiveFrom, x.EffectiveTo, y.EffectiveFrom, y.EffectiveTo)) continue;

                    if (x.TenantLabId == y.TenantLabId)
                    {
                        errors[j] ??= $"與本批次第 {i + 1} 筆（同稽核員、同實驗室）期間重疊。";
                    }
                    else
                    {
                        warnings[j].Add($"與本批次第 {i + 1} 筆同期（稽核員同時稽核「{await LabNameAsync(x.TenantLabId).ConfigureAwait(false)}」）。");
                    }
                }
            }
        }

        var results = Enumerable.Range(0, items.Count)
            .Select(i => new BatchLabAuditItemResult(i, errors[i] is null, errors[i], warnings[i]))
            .ToList();

        int errorCount = errors.Count(e => e is not null);
        if (errorCount > 0)
        {
            return new BatchCreateLabAuditResult(false, $"有 {errorCount} 筆明細不合格，整批未建立；請修正後重試。", 0, results);
        }

        if (dryRun) return new BatchCreateLabAuditResult(true, $"驗證通過，共 {items.Count} 筆可建立。", 0, results);

        // 4) 全有全無：所有指派與歷程事件於同一次儲存寫入
        var (actorId, actorName) = CurrentActor();
        foreach (var item in items)
        {
            var user = users[item.UserId]!;
            var labName = (await GetLabAsync(item.TenantLabId).ConfigureAwait(false))!.Value.Name;
            var entity = new LabAuditAssignment
            {
                UserId = user.Id,
                TenantLabId = item.TenantLabId,
                EffectiveFrom = item.EffectiveFrom,
                EffectiveTo = item.EffectiveTo,
                Reason = item.Reason.Trim(),
                AssignedBy = actorId,
                AssignedByName = actorName,
                CreatedAt = now
            };
            dbContext.Set<LabAuditAssignment>().Add(entity);
            AddHistory(entity.Id, "created", actorId, actorName, now,
                $"（批次）指派「{Display(user)}」稽核「{labName}」：{PeriodText(entity.EffectiveFrom, entity.EffectiveTo)}；原因：{entity.Reason}");
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var affected = items.Select(i => i.UserId.ToString()).Distinct().ToList();
        await permissionChangeNotifier.NotifyUsersAsync(affected, "lab-audit-assigned", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[LabAudit] {Actor} 批次指派稽核 {Count} 筆（稽核員 {Users} 位）", actorName, items.Count, affected.Count);

        return new BatchCreateLabAuditResult(true, $"已建立 {items.Count} 筆稽核指派。", items.Count, results);
    }

    public async Task<(bool Succeeded, string Message, LabAuditAssignmentDto? Assignment)> UpdateAsync(
        Guid id, UpdateLabAuditRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await dbContext.Set<LabAuditAssignment>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null) return (false, "找不到指定的稽核指派。", null);
        if (entity.RevokedAt is not null) return (false, "此稽核指派已撤銷，不可修改。", null);

        var now = timeProvider.GetUtcNow();
        if (entity.EffectiveTo < now) return (false, "此稽核指派已到期，不可修改（請另行新增）。", null);

        if (request.TenantLabId == Guid.Empty) return (false, "實驗室識別碼無效。", null);
        if (string.IsNullOrWhiteSpace(request.Reason)) return (false, "請填寫稽核原因或稽核案號。", null);
        if (request.EffectiveTo <= request.EffectiveFrom) return (false, "迄日必須晚於起日。", null);
        if (request.EffectiveTo - request.EffectiveFrom > MaxDuration) return (false, "單次稽核指派期間不可超過一年。", null);
        if (request.EffectiveTo < now) return (false, "迄日已過，無法改成已失效的期間；如需終止請使用撤銷。", null);

        var user = await userManager.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false);
        if (user is null || user.IsDeleted) return (false, "找不到指定的使用者。", null);

        var candidates = await GetAuditorCandidatesAsync(cancellationToken).ConfigureAwait(false);
        if (candidates.All(c => c.UserId != user.Id.ToString()))
        {
            return (false, $"使用者「{Display(user)}」未持有稽核角色（例：各區實驗室稽核員），請先於角色管理指派。", null);
        }

        var lab = await orgIntegrationService.GetOrganizationByIdAsync(request.TenantLabId, cancellationToken).ConfigureAwait(false);
        if (lab is null) return (false, "找不到指定的區域實驗室。", null);
        if (!lab.IsActive) return (false, $"實驗室「{lab.Name}」已停用，無法指派稽核。", null);

        bool overlaps = await dbContext.Set<LabAuditAssignment>().AsNoTracking()
            .AnyAsync(a => a.Id != id && a.UserId == user.Id && a.TenantLabId == request.TenantLabId && a.RevokedAt == null &&
                           a.EffectiveFrom <= request.EffectiveTo && a.EffectiveTo >= request.EffectiveFrom, cancellationToken)
            .ConfigureAwait(false);
        if (overlaps) return (false, "此使用者於該實驗室已有期間重疊的稽核指派，請調整期間或先撤銷既有指派。", null);

        // 異動摘要：只記錄實際有變更的欄位，保留修改前後的值以供追溯（紀錄本身不刪除）
        var oldUserId = entity.UserId;
        var oldLabId = entity.TenantLabId;
        var changes = new List<string>();
        if (oldUserId != user.Id)
        {
            var oldUser = await userManager.FindByIdAsync(oldUserId.ToString()).ConfigureAwait(false);
            changes.Add($"稽核員：{(oldUser is null ? oldUserId.ToString()[..8] : Display(oldUser))} → {Display(user)}");
        }

        if (oldLabId != request.TenantLabId)
        {
            var oldLab = await orgIntegrationService.GetOrganizationByIdAsync(oldLabId, cancellationToken).ConfigureAwait(false);
            changes.Add($"實驗室：{oldLab?.Name ?? oldLabId.ToString()[..8]} → {lab.Name}");
        }

        if (entity.EffectiveFrom != request.EffectiveFrom || entity.EffectiveTo != request.EffectiveTo)
        {
            changes.Add($"期間：{entity.EffectiveFrom.ToLocalTime():yyyy-MM-dd}～{entity.EffectiveTo.ToLocalTime():yyyy-MM-dd} → " +
                        $"{request.EffectiveFrom.ToLocalTime():yyyy-MM-dd}～{request.EffectiveTo.ToLocalTime():yyyy-MM-dd}");
        }

        var newReason = request.Reason.Trim();
        if (!string.Equals(entity.Reason, newReason, StringComparison.Ordinal))
        {
            changes.Add($"原因：{entity.Reason} → {newReason}");
        }

        if (changes.Count == 0) return (false, "內容沒有任何變更。", null);

        var (updateActorId, actorName) = CurrentActor();
        entity.UserId = user.Id;
        entity.TenantLabId = request.TenantLabId;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.Reason = newReason;
        entity.UpdatedAt = now;
        entity.UpdatedByName = actorName;
        var note = string.Join("；", changes);
        entity.UpdateNote = note.Length > 1000 ? note[..1000] : note;
        AddHistory(entity.Id, "updated", updateActorId, actorName, now, note);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 稽核員變更時，原稽核員的權限須立即失效，新稽核員立即生效
        var affectedUsers = new HashSet<string> { user.Id.ToString(), oldUserId.ToString() };
        await permissionChangeNotifier.NotifyUsersAsync(affectedUsers.ToList(), "lab-audit-updated", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[LabAudit] {Actor} 修改稽核指派 {Id}：{Note}", actorName, id, entity.UpdateNote);

        var dto = (await ToDtosAsync([entity], cancellationToken).ConfigureAwait(false))[0];
        return (true, "已更新稽核指派。", dto);
    }

    public async Task<(bool Succeeded, string Message)> RevokeAsync(Guid id, string? reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "請填寫撤銷原因。");

        var entity = await dbContext.Set<LabAuditAssignment>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null) return (false, "找不到指定的稽核指派。");
        if (entity.RevokedAt is not null) return (false, "此稽核指派已撤銷。");

        var now = timeProvider.GetUtcNow();
        if (entity.EffectiveTo < now) return (false, "此稽核指派已到期，無須撤銷。");

        var (revokeActorId, actorName) = CurrentActor();
        entity.RevokedAt = now;
        entity.RevokedByName = actorName;
        entity.RevokeReason = reason.Trim();
        AddHistory(entity.Id, "revoked", revokeActorId, actorName, now, $"撤銷原因：{entity.RevokeReason}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await permissionChangeNotifier.NotifyUsersAsync([entity.UserId.ToString()], "lab-audit-revoked", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[LabAudit] {Actor} 撤銷稽核指派 {Id}（{Reason}）", actorName, id, entity.RevokeReason);
        return (true, "已撤銷稽核指派，該使用者於此實驗室的稽核權限立即失效。");
    }

    public async Task<IReadOnlyList<LabAuditHistoryDto>?> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Set<LabAuditAssignment>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;

        var stored = await dbContext.Set<LabAuditAssignmentHistory>().AsNoTracking()
            .Where(h => h.AssignmentId == id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var events = stored
            .Select(h => new LabAuditHistoryDto(h.Id, h.Action, h.ActorName, h.OccurredAt, h.Summary))
            .ToList();

        // 歷程功能上線前建立的舊資料：由指派本身的欄位補出缺少的事件，讓時間軸完整
        if (stored.All(h => h.Action != "created"))
        {
            events.Add(new LabAuditHistoryDto(Guid.Empty, "created", entity.AssignedByName, entity.CreatedAt, "建立稽核指派（歷程功能上線前的紀錄，原始內容請見明細）"));
        }

        if (entity.UpdatedAt.HasValue && stored.All(h => h.Action != "updated"))
        {
            events.Add(new LabAuditHistoryDto(Guid.Empty, "updated", entity.UpdatedByName, entity.UpdatedAt.Value, entity.UpdateNote ?? "修改稽核指派"));
        }

        if (entity.RevokedAt.HasValue && stored.All(h => h.Action != "revoked"))
        {
            events.Add(new LabAuditHistoryDto(Guid.Empty, "revoked", entity.RevokedByName, entity.RevokedAt.Value, $"撤銷原因：{entity.RevokeReason}"));
        }

        return events.OrderBy(e => e.OccurredAt).ToList();
    }

    private void AddHistory(Guid assignmentId, string action, Guid? actorId, string actorName, DateTimeOffset at, string summary) =>
        dbContext.Set<LabAuditAssignmentHistory>().Add(new LabAuditAssignmentHistory
        {
            AssignmentId = assignmentId,
            Action = action,
            ActorId = actorId,
            ActorName = actorName,
            OccurredAt = at,
            Summary = summary.Length > 1500 ? summary[..1500] : summary
        });

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetActiveLabsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var active = await dbContext.Set<LabAuditAssignment>().AsNoTracking()
            .Where(a => a.UserId == userId && a.RevokedAt == null && a.EffectiveFrom <= now && a.EffectiveTo >= now)
            .Select(a => new { a.TenantLabId, a.EffectiveTo })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return active.GroupBy(a => a.TenantLabId).ToDictionary(g => g.Key, g => g.Max(a => a.EffectiveTo));
    }

    private async Task<List<LabAuditAssignmentDto>> ToDtosAsync(IReadOnlyCollection<LabAuditAssignment> items, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var userIds = items.Select(i => i.UserId).Distinct().ToList();
        var users = await dbContext.Set<ApplicationUser>().AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.FullName })
            .ToDictionaryAsync(u => u.Id, cancellationToken).ConfigureAwait(false);

        var labNames = new Dictionary<Guid, string>();
        foreach (var labId in items.Select(i => i.TenantLabId).Distinct())
        {
            var org = await orgIntegrationService.GetOrganizationByIdAsync(labId, cancellationToken).ConfigureAwait(false);
            labNames[labId] = org?.Name ?? labId.ToString()[..8];
        }

        return items.Select(a =>
        {
            users.TryGetValue(a.UserId, out var u);
            var status = a.RevokedAt is not null ? LabAuditStatuses.Revoked
                : a.EffectiveTo < now ? LabAuditStatuses.Expired
                : a.EffectiveFrom > now ? LabAuditStatuses.Upcoming
                : LabAuditStatuses.Active;
            return new LabAuditAssignmentDto(
                a.Id, a.UserId.ToString(), u?.UserName ?? string.Empty, u?.FullName ?? string.Empty,
                a.TenantLabId, labNames[a.TenantLabId], a.EffectiveFrom, a.EffectiveTo, a.Reason, status,
                a.AssignedByName, a.CreatedAt, a.RevokedAt, a.RevokedByName, a.RevokeReason,
                a.UpdatedAt, a.UpdatedByName, a.UpdateNote);
        }).ToList();
    }

    private static string Display(ApplicationUser user) => string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? string.Empty : user.FullName;

    private (Guid? Id, string Name) CurrentActor()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var raw = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal?.FindFirst("sub")?.Value;
        return (Guid.TryParse(raw, out var id) ? id : null,
                principal?.Identity?.Name ?? principal?.FindFirst(ClaimTypes.Name)?.Value ?? "未知使用者");
    }
}
