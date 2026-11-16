using Microsoft.EntityFrameworkCore;
using SGS.Modules.QTN.Application.Contracts;
using SGS.Modules.QTN.Application.Rules;
using SGS.Modules.QTN.Infrastructure.Dbcontexts;
using SGS.Modules.QTN.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.QTN.Application.Services;

/// <summary>
/// 報價主檔：客戶、角色費率（成本單價為機密，僅 QTN.COST.READ 可見）、標準 WBS 工時庫、審批權責矩陣人選。
/// 費率調整寫入歷程，且不回溯影響既有報價（報價使用建立當下的快照）。
/// </summary>
public sealed class MasterDataService(QTNDbContext db, TimeProvider clock)
{
    private const int MaxPageSize = 200;
    private const int MaxTemplateItems = 200;

    // ================= 客戶 =================

    public async Task<PagedResult<CustomerListDto>> SearchCustomersAsync(CustomerSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var query = db.Customers.AsNoTracking();
        if (request.Filter?.IsActive is { } active) query = query.Where(c => c.IsActive == active);
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(c => c.Code.Contains(term) || c.Name.Contains(term) || (c.TaxId != null && c.TaxId.Contains(term)));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var rows = await query.OrderBy(c => c.Code).Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(c => new CustomerListDto(c.Id, c.Code, c.Name, c.TaxId, c.Industry, c.IsStrategic, c.IsActive))
            .ToListAsync(ct).ConfigureAwait(false);
        return new PagedResult<CustomerListDto>(rows, total, pageIndex, pageSize);
    }

    public async Task<QtnResult<CustomerDto>> GetCustomerAsync(Guid id, CancellationToken ct)
    {
        var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        return c is null ? QtnError.NotFound("客戶不存在。") : QtnResult<CustomerDto>.Success(ToDto(c));
    }

    public async Task<QtnResult<CustomerDto>> CreateCustomerAsync(string actor, UpsertCustomerRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invalid = ValidateCustomer(request);
        if (invalid is not null) return QtnError.Validation(invalid);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Customers.AnyAsync(c => c.Code == code, ct).ConfigureAwait(false))
            return QtnError.Conflict("QTN.DUPLICATE_CODE", $"客戶代碼 {code} 已存在。");

        var c = new Customer { Id = Guid.NewGuid(), Code = code, CreatedBy = actor, CreatedAtUtc = clock.GetUtcNow() };
        Apply(c, request);
        db.Customers.Add(c);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return QtnResult<CustomerDto>.Success(ToDto(c));
    }

    public async Task<QtnResult<CustomerDto>> UpdateCustomerAsync(string actor, Guid id, UpsertCustomerRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invalid = ValidateCustomer(request);
        if (invalid is not null) return QtnError.Validation(invalid);

        var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (c is null) return QtnError.NotFound("客戶不存在。");
        if (!RowVersions.Matches(c.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(c).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        var code = request.Code.Trim().ToUpperInvariant();
        if (code != c.Code && await db.Customers.AnyAsync(x => x.Code == code, ct).ConfigureAwait(false))
            return QtnError.Conflict("QTN.DUPLICATE_CODE", $"客戶代碼 {code} 已存在。");

        c.Code = code;
        Apply(c, request);
        c.UpdatedBy = actor;
        c.UpdatedAtUtc = clock.GetUtcNow();
        return await SaveAsync(() => ToDto(c), ct).ConfigureAwait(false);
    }

    private static string? ValidateCustomer(UpsertCustomerRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || r.Code.Trim().Length > 30) return "客戶代碼必填，最多 30 字。";
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 200) return "客戶名稱必填，最多 200 字。";
        if (r.TaxId is { Length: > 20 }) return "統一編號最多 20 字。";
        if (r.ContactEmail is { Length: > 0 } email && (!email.Contains('@', StringComparison.Ordinal) || email.Length > 200)) return "聯絡 Email 格式不正確。";
        return null;
    }

    private static void Apply(Customer c, UpsertCustomerRequest r)
    {
        c.Name = r.Name.Trim();
        c.TaxId = Clean(r.TaxId);
        c.Industry = Clean(r.Industry);
        c.Address = Clean(r.Address);
        c.ContactName = Clean(r.ContactName);
        c.ContactEmail = Clean(r.ContactEmail);
        c.ContactPhone = Clean(r.ContactPhone);
        c.IsStrategic = r.IsStrategic;
        c.IsActive = r.IsActive;
        c.Notes = Clean(r.Notes);
    }

    private static CustomerDto ToDto(Customer c) => new(
        c.Id, c.Code, c.Name, c.TaxId, c.Industry, c.Address, c.ContactName, c.ContactEmail, c.ContactPhone, c.IsStrategic, c.IsActive, c.Notes,
        RowVersions.Format(c.RowVersion));

    // ================= 角色費率 =================

    public async Task<IReadOnlyList<RateRoleDto>> ListRateRolesAsync(bool canReadCost, bool includeInactive, CancellationToken ct)
    {
        var query = db.RateRoles.AsNoTracking();
        if (!includeInactive) query = query.Where(r => r.IsActive);
        var rows = await query.OrderBy(r => r.SortOrder).ThenBy(r => r.Code).ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(r => ToDto(r, canReadCost)).ToList();
    }

    public async Task<QtnResult<IReadOnlyList<RateRoleHistoryDto>>> RateHistoryAsync(Guid id, bool canReadCost, CancellationToken ct)
    {
        if (!await db.RateRoles.AnyAsync(r => r.Id == id, ct).ConfigureAwait(false)) return QtnError.NotFound("角色不存在。");
        var rows = await db.RateRoleHistories.AsNoTracking().Where(h => h.RateRoleId == id).OrderByDescending(h => h.EffectiveAt).Take(100)
            .ToListAsync(ct).ConfigureAwait(false);
        return QtnResult<IReadOnlyList<RateRoleHistoryDto>>.Success(
            rows.Select(h => new RateRoleHistoryDto(h.EffectiveAt, canReadCost ? h.CostRate : null, h.ListRate, h.Reason, h.CreatedBy)).ToList());
    }

    public async Task<QtnResult<RateRoleDto>> CreateRateRoleAsync(string actor, UpsertRateRoleRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CostRate is null) return QtnError.Validation("建立角色須填寫內部成本單價。");
        var invalid = ValidateRate(request);
        if (invalid is not null) return QtnError.Validation(invalid);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.RateRoles.AnyAsync(r => r.Code == code, ct).ConfigureAwait(false))
            return QtnError.Conflict("QTN.DUPLICATE_CODE", $"角色代碼 {code} 已存在。");

        var now = clock.GetUtcNow();
        var role = new RateRole
        {
            Id = Guid.NewGuid(), Code = code, Name = request.Name.Trim(), BaseCostRate = request.CostRate.Value, BaseListRate = request.ListRate,
            SortOrder = request.SortOrder, IsActive = request.IsActive, CreatedBy = actor, CreatedAtUtc = now
        };
        role.History.Add(new RateRoleHistory
        {
            Id = Guid.NewGuid(), CostRate = role.BaseCostRate, ListRate = role.BaseListRate, EffectiveAt = now, Reason = Clean(request.Reason) ?? "建立",
            CreatedBy = actor, CreatedAtUtc = now
        });
        db.RateRoles.Add(role);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return QtnResult<RateRoleDto>.Success(ToDto(role, true));
    }

    public async Task<QtnResult<RateRoleDto>> UpdateRateRoleAsync(string actor, Guid id, UpsertRateRoleRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invalid = ValidateRate(request);
        if (invalid is not null) return QtnError.Validation(invalid);

        var role = await db.RateRoles.FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
        if (role is null) return QtnError.NotFound("角色不存在。");
        if (!RowVersions.Matches(role.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(role).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        var code = request.Code.Trim().ToUpperInvariant();
        if (code != role.Code && await db.RateRoles.AnyAsync(r => r.Code == code, ct).ConfigureAwait(false))
            return QtnError.Conflict("QTN.DUPLICATE_CODE", $"角色代碼 {code} 已存在。");

        var newCost = request.CostRate ?? role.BaseCostRate;
        var rateChanged = newCost != role.BaseCostRate || request.ListRate != role.BaseListRate;
        if (rateChanged && string.IsNullOrWhiteSpace(request.Reason)) return QtnError.Validation("調整費率須填寫原因。");

        var now = clock.GetUtcNow();
        role.Code = code;
        role.Name = request.Name.Trim();
        role.BaseCostRate = newCost;
        role.BaseListRate = request.ListRate;
        role.SortOrder = request.SortOrder;
        role.IsActive = request.IsActive;
        role.UpdatedBy = actor;
        role.UpdatedAtUtc = now;
        if (rateChanged)
            db.RateRoleHistories.Add(new RateRoleHistory
            {
                Id = Guid.NewGuid(), RateRoleId = role.Id, CostRate = newCost, ListRate = request.ListRate, EffectiveAt = now, Reason = request.Reason!.Trim(),
                CreatedBy = actor, CreatedAtUtc = now
            });

        return await SaveAsync(() => ToDto(role, true), ct).ConfigureAwait(false);
    }

    private static string? ValidateRate(UpsertRateRoleRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || r.Code.Trim().Length > 30) return "角色代碼必填，最多 30 字。";
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 100) return "角色名稱必填，最多 100 字。";
        if (r.CostRate is < 0 or > 1_000_000) return "成本單價須介於 0–1,000,000。";
        if (r.ListRate is < 0 or > 1_000_000) return "牌價須介於 0–1,000,000。";
        if (r.Reason is { Length: > 500 }) return "原因最多 500 字。";
        return null;
    }

    private static RateRoleDto ToDto(RateRole r, bool canReadCost) =>
        new(r.Id, r.Code, r.Name, canReadCost ? r.BaseCostRate : null, r.BaseListRate, r.SortOrder, r.IsActive, RowVersions.Format(r.RowVersion));

    // ================= 標準 WBS 工時庫 =================

    public async Task<IReadOnlyList<WbsTemplateListDto>> ListTemplatesAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.WbsTemplates.AsNoTracking();
        if (!includeInactive) query = query.Where(t => t.IsActive);
        var rows = await query.Include(t => t.Items).OrderBy(t => t.Code).ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(t => new WbsTemplateListDto(
            t.Id, t.Code, t.Name, t.Description, t.IsActive, t.Items.Count,
            t.Items.Sum(i => PertCalculator.Expected(i.Optimistic, i.MostLikely, i.Pessimistic)))).ToList();
    }

    public async Task<QtnResult<WbsTemplateDto>> GetTemplateAsync(Guid id, CancellationToken ct)
    {
        var t = await db.WbsTemplates.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (t is null) return QtnError.NotFound("標準模組不存在。");
        return QtnResult<WbsTemplateDto>.Success(await ToDtoAsync(t, ct).ConfigureAwait(false));
    }

    public async Task<QtnResult<WbsTemplateDto>> CreateTemplateAsync(string actor, UpsertWbsTemplateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invalid = await ValidateTemplateAsync(request, ct).ConfigureAwait(false);
        if (invalid is not null) return QtnError.Validation(invalid);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.WbsTemplates.AnyAsync(t => t.Code == code, ct).ConfigureAwait(false))
            return QtnError.Conflict("QTN.DUPLICATE_CODE", $"模組代碼 {code} 已存在。");

        var now = clock.GetUtcNow();
        var t = new WbsTemplate { Id = Guid.NewGuid(), Code = code, CreatedBy = actor, CreatedAtUtc = now };
        t.Name = request.Name.Trim();
        t.Description = Clean(request.Description);
        t.IsActive = request.IsActive;
        var order = 0;
        foreach (var i in request.Items)
            t.Items.Add(NewTemplateItem(i, ++order, actor, now));
        db.WbsTemplates.Add(t);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return QtnResult<WbsTemplateDto>.Success(await ToDtoAsync(t, ct).ConfigureAwait(false));
    }

    public async Task<QtnResult<WbsTemplateDto>> UpdateTemplateAsync(string actor, Guid id, UpsertWbsTemplateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invalid = await ValidateTemplateAsync(request, ct).ConfigureAwait(false);
        if (invalid is not null) return QtnError.Validation(invalid);

        var t = await db.WbsTemplates.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (t is null) return QtnError.NotFound("標準模組不存在。");
        if (!RowVersions.Matches(t.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(t).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        var code = request.Code.Trim().ToUpperInvariant();
        if (code != t.Code && await db.WbsTemplates.AnyAsync(x => x.Code == code, ct).ConfigureAwait(false))
            return QtnError.Conflict("QTN.DUPLICATE_CODE", $"模組代碼 {code} 已存在。");

        var now = clock.GetUtcNow();
        t.Code = code;
        t.Name = request.Name.Trim();
        t.Description = Clean(request.Description);
        t.IsActive = request.IsActive;
        t.UpdatedBy = actor;
        t.UpdatedAtUtc = now;

        // 保留既有工作項 Id（報價與復盤以 TemplateItemId 追溯校準）；基準工時異動時版本 +1
        var keep = request.Items.Where(i => i.Id is not null).Select(i => i.Id!.Value).ToHashSet();
        foreach (var removed in t.Items.Where(i => !keep.Contains(i.Id)).ToList())
        {
            t.Items.Remove(removed);
            db.WbsTemplateItems.Remove(removed);
        }

        var order = 0;
        foreach (var input in request.Items)
        {
            order++;
            var existing = input.Id is { } itemId ? t.Items.FirstOrDefault(i => i.Id == itemId) : null;
            if (existing is null)
            {
                var item = NewTemplateItem(input, order, actor, now);
                item.TemplateId = t.Id;
                db.WbsTemplateItems.Add(item);
                continue;
            }

            var baselineChanged = existing.Optimistic != input.Optimistic || existing.MostLikely != input.MostLikely || existing.Pessimistic != input.Pessimistic;
            existing.SortOrder = order;
            existing.Name = input.Name.Trim();
            existing.Deliverable = Clean(input.Deliverable);
            existing.Phase = input.Phase;
            existing.RateRoleId = input.RateRoleId;
            existing.Optimistic = input.Optimistic;
            existing.MostLikely = input.MostLikely;
            existing.Pessimistic = input.Pessimistic;
            if (baselineChanged)
            {
                existing.BaselineVersion++;
                existing.LastCalibratedAt = now;
            }

            existing.UpdatedBy = actor;
            existing.UpdatedAtUtc = now;
        }

        var saved = await SaveAsync(() => true, ct).ConfigureAwait(false);
        if (saved.Error is { } error) return error;
        return await GetTemplateAsync(id, ct).ConfigureAwait(false);
    }

    private async Task<string?> ValidateTemplateAsync(UpsertWbsTemplateRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || r.Code.Trim().Length > 30) return "模組代碼必填，最多 30 字。";
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 200) return "模組名稱必填，最多 200 字。";
        if (r.Items is null || r.Items.Count == 0) return "至少需一個工作項。";
        if (r.Items.Count > MaxTemplateItems) return $"工作項最多 {MaxTemplateItems} 項。";

        foreach (var i in r.Items)
        {
            if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Trim().Length > 200) return "工作項名稱必填，最多 200 字。";
            if (!Enum.IsDefined(i.Phase)) return "工作項階段不正確。";
            var pert = PertCalculator.Validate(i.Optimistic, i.MostLikely, i.Pessimistic);
            if (pert is not null) return $"「{i.Name}」：{pert}";
        }

        var roleIds = r.Items.Select(i => i.RateRoleId).Distinct().ToList();
        var found = await db.RateRoles.CountAsync(x => roleIds.Contains(x.Id), ct).ConfigureAwait(false);
        return found == roleIds.Count ? null : "工作項的投入角色不存在。";
    }

    private static WbsTemplateItem NewTemplateItem(WbsTemplateItemInput i, int order, string actor, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), SortOrder = order, Name = i.Name.Trim(), Deliverable = Clean(i.Deliverable), Phase = i.Phase, RateRoleId = i.RateRoleId,
        Optimistic = i.Optimistic, MostLikely = i.MostLikely, Pessimistic = i.Pessimistic, CreatedBy = actor, CreatedAtUtc = now
    };

    private async Task<WbsTemplateDto> ToDtoAsync(WbsTemplate t, CancellationToken ct)
    {
        var roleIds = t.Items.Select(i => i.RateRoleId).Distinct().ToList();
        var roles = await db.RateRoles.AsNoTracking().Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct).ConfigureAwait(false);
        return new WbsTemplateDto(
            t.Id, t.Code, t.Name, t.Description, t.IsActive,
            t.Items.OrderBy(i => i.SortOrder).Select(i => new WbsTemplateItemDto(
                i.Id, i.SortOrder, i.Name, i.Deliverable, i.Phase, i.RateRoleId, roles.GetValueOrDefault(i.RateRoleId, "（已刪除角色）"),
                i.Optimistic, i.MostLikely, i.Pessimistic, PertCalculator.Expected(i.Optimistic, i.MostLikely, i.Pessimistic), i.BaselineVersion, i.LastCalibratedAt))
                .ToList(),
            RowVersions.Format(t.RowVersion));
    }

    // ================= 審批人 =================

    public async Task<IReadOnlyList<ApproverRoleDto>> ListApproversAsync(CancellationToken ct)
    {
        var rows = await db.ApproverAssignments.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        return Enum.GetValues<ApprovalRole>().Where(r => r != ApprovalRole.TechLead)
            .Select(r => new ApproverRoleDto(r, MarginLightPolicy.RoleName(r), rows.Where(x => x.Role == r).Select(x => x.UserId).ToList()))
            .ToList();
    }

    public async Task<QtnResult<IReadOnlyList<ApproverRoleDto>>> SetApproversAsync(string actor, SetApproversRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Role)) return QtnError.Validation("審批角色不正確。");
        if (request.Role == ApprovalRole.TechLead) return QtnError.Validation("Tech Lead 關卡由各商機的 Tech Lead 成員負責，無須指派。");
        var wanted = (request.UserIds ?? []).Where(u => u != Guid.Empty).Distinct().ToList();
        if (wanted.Count > 20) return QtnError.Validation("每個審批角色最多 20 人。");

        var now = clock.GetUtcNow();
        var existing = await db.ApproverAssignments.Where(a => a.Role == request.Role).ToListAsync(ct).ConfigureAwait(false);
        db.ApproverAssignments.RemoveRange(existing.Where(e => !wanted.Contains(e.UserId)));
        foreach (var userId in wanted.Where(u => existing.All(e => e.UserId != u)))
            db.ApproverAssignments.Add(new ApproverAssignment { Id = Guid.NewGuid(), Role = request.Role, UserId = userId, CreatedBy = actor, CreatedAtUtc = now });

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return QtnResult<IReadOnlyList<ApproverRoleDto>>.Success(await ListApproversAsync(ct).ConfigureAwait(false));
    }

    // ================= 共用 =================

    private async Task<QtnResult<T>> SaveAsync<T>(Func<T> projection, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return QtnResult<T>.Success(projection());
        }
        catch (DbUpdateConcurrencyException)
        {
            return QtnError.Concurrency();
        }
    }

    internal static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

/// <summary>RowVersion 與前端字串（Base64）互轉。</summary>
public static class RowVersions
{
    public static string Format(byte[]? v) => v is { Length: > 0 } ? Convert.ToBase64String(v) : string.Empty;

    public static byte[] Parse(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return [];
        try
        {
            return Convert.FromBase64String(v);
        }
        catch (FormatException)
        {
            return [];
        }
    }

    public static bool Matches(byte[]? current, string? provided) =>
        !string.IsNullOrWhiteSpace(provided) && (current ?? []).AsSpan().SequenceEqual(Parse(provided));
}
