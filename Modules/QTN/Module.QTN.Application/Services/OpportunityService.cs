using Microsoft.EntityFrameworkCore;
using SGS.Modules.QTN.Application.Contracts;
using SGS.Modules.QTN.Application.Rules;
using SGS.Modules.QTN.Infrastructure.Dbcontexts;
using SGS.Modules.QTN.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.QTN.Application.Services;

/// <summary>
/// 資料列授權（同 PMS ProjectAccessPolicy 精神）：商機成員（業務／Tech Lead／PM／財務）可見；審批權責矩陣中被指派的主管可見全部報價。
/// 其餘一律 404。每個請求快取一次審批人角色。
/// </summary>
public sealed class QtnAccess(QTNDbContext db)
{
    private readonly Dictionary<Guid, HashSet<ApprovalRole>> _approverRoles = [];

    public async Task<HashSet<ApprovalRole>> ApproverRolesAsync(Guid userId, CancellationToken ct)
    {
        if (_approverRoles.TryGetValue(userId, out var cached)) return cached;
        var roles = (await db.ApproverAssignments.AsNoTracking().Where(a => a.UserId == userId).Select(a => a.Role).ToListAsync(ct).ConfigureAwait(false))
            .ToHashSet();
        _approverRoles[userId] = roles;
        return roles;
    }

    public async Task<bool> IsApproverAsync(Guid userId, CancellationToken ct) => (await ApproverRolesAsync(userId, ct).ConfigureAwait(false)).Count > 0;

    public static IReadOnlyList<OpportunityRole> Roles(Opportunity o, Guid userId) =>
        o.Members.Where(m => m.UserId == userId).Select(m => m.Role).Distinct().ToList();

    public static bool Has(Opportunity o, Guid userId, params OpportunityRole[] roles) =>
        o.Members.Any(m => m.UserId == userId && roles.Contains(m.Role));

    public async Task<bool> CanViewAsync(Opportunity o, Guid userId, CancellationToken ct) =>
        o.Members.Any(m => m.UserId == userId) || await IsApproverAsync(userId, ct).ConfigureAwait(false);

    /// <summary>可見商機的查詢條件（審批人可見全部）。</summary>
    public async Task<IQueryable<Opportunity>> VisibleOpportunitiesAsync(Guid userId, CancellationToken ct)
    {
        var query = db.Opportunities.AsNoTracking();
        return await IsApproverAsync(userId, ct).ConfigureAwait(false) ? query : query.Where(o => o.Members.Any(m => m.UserId == userId));
    }
}

/// <summary>
/// 商機立案與收斂（SOP 階段一）：客戶、業務邊界、預期上線日、非功能性需求（併發量、資安等級）、計價模式與 IP 買斷旗標。
/// 建立者即業務成員；Tech Lead／PM／財務於成員中指派。
/// </summary>
public sealed class OpportunityService(QTNDbContext db, QtnAccess access, TimeProvider clock)
{
    private const int MaxPageSize = 200;
    private const int NumberRetries = 3;
    private const int MaxMembersPerRole = 5;

    public async Task<PagedResult<OpportunityListDto>> SearchAsync(QtnCaller caller, OpportunitySearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var filter = request.Filter ?? new OpportunityFilter();

        var query = await access.VisibleOpportunitiesAsync(caller.UserId, ct).ConfigureAwait(false);
        if (filter.Status is { Count: > 0 } statuses) query = query.Where(o => statuses.Contains(o.Status));
        if (filter.PricingModel is { Count: > 0 } models) query = query.Where(o => models.Contains(o.PricingModel));
        if (filter.CustomerId is { } customerId) query = query.Where(o => o.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(o => o.OpportunityNo.Contains(term) || o.Title.Contains(term) || o.Customer!.Name.Contains(term) || (o.CrmRef != null && o.CrmRef.Contains(term)));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var rows = await query.OrderByDescending(o => o.UpdatedAtUtc ?? o.CreatedAtUtc).ThenBy(o => o.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Include(o => o.Customer).Include(o => o.Members).Include(o => o.Quotes)
            .AsSplitQuery().ToListAsync(ct).ConfigureAwait(false);

        return new PagedResult<OpportunityListDto>(rows.Select(o => new OpportunityListDto(
            o.Id, o.OpportunityNo, o.Title, o.Customer?.Name ?? string.Empty, o.Category, o.PricingModel, o.Status,
            o.Quotes.Where(q => !q.IsChangeRequest).OrderByDescending(q => q.Minor).Select(q => Summary(q, caller.Access)).FirstOrDefault(),
            QtnAccess.Roles(o, caller.UserId), o.CreatedAtUtc, o.UpdatedAtUtc)).ToList(), total, pageIndex, pageSize);
    }

    public async Task<QtnResult<OpportunityDto>> GetAsync(QtnCaller caller, Guid id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var o = await db.Opportunities.AsNoTracking().Include(x => x.Customer).Include(x => x.Members).Include(x => x.Quotes)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (o is null || !await access.CanViewAsync(o, caller.UserId, ct).ConfigureAwait(false)) return QtnError.NotFound("商機不存在。");
        return QtnResult<OpportunityDto>.Success(ToDto(o, caller));
    }

    public async Task<QtnResult<OpportunityDto>> CreateAsync(QtnCaller caller, UpsertOpportunityRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        var invalid = Validate(request);
        if (invalid is not null) return QtnError.Validation(invalid);

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct).ConfigureAwait(false);
        if (customer is null || !customer.IsActive) return QtnError.Validation("客戶不存在或已停用。");

        var now = clock.GetUtcNow();
        var year = Workdays.TaipeiToday(now).Year;
        for (var attempt = 1; attempt <= NumberRetries; attempt++)
        {
            var prefix = $"OP-{year}-";
            var existing = await db.Opportunities.AsNoTracking().Where(o => o.OpportunityNo.StartsWith(prefix)).Select(o => o.OpportunityNo)
                .ToListAsync(ct).ConfigureAwait(false);

            var o = new Opportunity
            {
                Id = Guid.NewGuid(), OpportunityNo = QuoteNumbering.NextOpportunityNo(year, existing), CreatedBy = caller.Name, CreatedAtUtc = now
            };
            Apply(o, request);
            o.Members.Add(new OpportunityMember { Id = Guid.NewGuid(), UserId = caller.UserId, Role = OpportunityRole.Sales, CreatedBy = caller.Name, CreatedAtUtc = now });
            db.Opportunities.Add(o);

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return await GetAsync(caller, o.Id, ct).ConfigureAwait(false);
            }
            catch (DbUpdateException) when (attempt < NumberRetries)
            {
                db.ChangeTracker.Clear();
            }
        }

        return QtnError.Conflict("QTN.CONCURRENCY_CONFLICT", "商機編號產生衝突，請稍後再試。");
    }

    public async Task<QtnResult<OpportunityDto>> UpdateAsync(QtnCaller caller, Guid id, UpsertOpportunityRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        var invalid = Validate(request);
        if (invalid is not null) return QtnError.Validation(invalid);

        var o = await db.Opportunities.Include(x => x.Members).Include(x => x.Quotes).FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (o is null || !await access.CanViewAsync(o, caller.UserId, ct).ConfigureAwait(false)) return QtnError.NotFound("商機不存在。");
        if (!QtnAccess.Has(o, caller.UserId, OpportunityRole.Sales)) return QtnError.Forbidden("僅商機業務可修改商機資料。");
        if (o.Status != OpportunityStatus.Open) return QtnError.Conflict("QTN.INVALID_TRANSITION", "商機已結案，不可修改。");
        if (!RowVersions.Matches(o.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(o).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        // 已有報價後，計價模式與客戶即為報價單號與快照的一部分，不可再改（請另立商機）
        if (o.Quotes.Count > 0 && (request.PricingModel != o.PricingModel || request.CustomerId != o.CustomerId))
            return QtnError.Conflict("QTN.CONTENT_LOCKED", "已建立報價的商機不可變更客戶或計價模式。");

        if (request.CustomerId != o.CustomerId && !await db.Customers.AnyAsync(c => c.Id == request.CustomerId && c.IsActive, ct).ConfigureAwait(false))
            return QtnError.Validation("客戶不存在或已停用。");

        Apply(o, request);
        o.UpdatedBy = caller.Name;
        o.UpdatedAtUtc = clock.GetUtcNow();

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return QtnError.Concurrency();
        }

        db.ChangeTracker.Clear();
        return await GetAsync(caller, id, ct).ConfigureAwait(false);
    }

    public async Task<QtnResult<OpportunityDto>> SetMembersAsync(QtnCaller caller, Guid id, SetOpportunityMembersRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var o = await db.Opportunities.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (o is null || !await access.CanViewAsync(o, caller.UserId, ct).ConfigureAwait(false)) return QtnError.NotFound("商機不存在。");
        if (!QtnAccess.Has(o, caller.UserId, OpportunityRole.Sales)) return QtnError.Forbidden("僅商機業務可維護成員。");

        var wanted = (request.Members ?? []).Where(m => m.UserId != Guid.Empty).Select(m => (m.UserId, m.Role)).Distinct().ToList();
        if (wanted.Any(m => !Enum.IsDefined(m.Role))) return QtnError.Validation("成員角色不正確。");
        if (wanted.GroupBy(m => m.Role).Any(g => g.Count() > MaxMembersPerRole)) return QtnError.Validation($"每個角色最多 {MaxMembersPerRole} 人。");
        if (!wanted.Any(m => m.Role == OpportunityRole.Sales)) return QtnError.Validation("至少要有一位業務。");

        var now = clock.GetUtcNow();
        foreach (var existing in o.Members.Where(m => !wanted.Contains((m.UserId, m.Role))).ToList())
        {
            o.Members.Remove(existing);
            db.OpportunityMembers.Remove(existing);
        }

        foreach (var (userId, role) in wanted.Where(w => !o.Members.Any(m => m.UserId == w.UserId && m.Role == w.Role)))
            db.OpportunityMembers.Add(new OpportunityMember { Id = Guid.NewGuid(), OpportunityId = o.Id, UserId = userId, Role = role, CreatedBy = caller.Name, CreatedAtUtc = now });

        o.UpdatedBy = caller.Name;
        o.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        db.ChangeTracker.Clear();
        return await GetAsync(caller, id, ct).ConfigureAwait(false);
    }

    private static string? Validate(UpsertOpportunityRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length > 200) return "專案名稱必填，最多 200 字。";
        if (!Enum.IsDefined(r.Category)) return "專案類型不正確。";
        if (!Enum.IsDefined(r.PricingModel)) return "計價模式不正確。";
        if (r.CrmRef is { Length: > 50 }) return "CRM 商機編號最多 50 字。";
        if (r.Scope is { Length: > 4000 }) return "業務邊界最多 4000 字。";
        if (r.ConcurrentUsers is < 0) return "併發量不可為負數。";
        return null;
    }

    private static void Apply(Opportunity o, UpsertOpportunityRequest r)
    {
        o.CustomerId = r.CustomerId;
        o.Title = r.Title.Trim();
        o.CrmRef = MasterDataService.Clean(r.CrmRef);
        o.Category = r.Category;
        o.PricingModel = r.PricingModel;
        o.Scope = MasterDataService.Clean(r.Scope);
        o.ExpectedGoLive = r.ExpectedGoLive;
        o.ConcurrentUsers = r.ConcurrentUsers;
        o.SecurityLevel = MasterDataService.Clean(r.SecurityLevel);
        o.NonFunctional = MasterDataService.Clean(r.NonFunctional);
        o.IpBuyout = r.IpBuyout;
    }

    internal static QuoteSummaryDto Summary(QuoteVersion q, QuoteFieldAccess fieldAccess) => new(
        q.Id, q.QuoteNo, q.VersionLabel, q.IsChangeRequest, q.Status, q.PricingModel, q.TotalQuotedDays,
        fieldAccess.Price(q.FinalPrice), fieldAccess.SeesPrice() ? q.MarginLight : null, q.ValidUntil, q.CreatedAtUtc);

    private static OpportunityDto ToDto(Opportunity o, QtnCaller caller)
    {
        var isSales = QtnAccess.Has(o, caller.UserId, OpportunityRole.Sales);
        return new OpportunityDto(
            o.Id, o.OpportunityNo, o.CrmRef, o.CustomerId, o.Customer?.Name ?? string.Empty, o.Customer?.IsStrategic ?? false, o.Title,
            o.Category, o.PricingModel, o.Status, o.Scope, o.ExpectedGoLive, o.ConcurrentUsers, o.SecurityLevel, o.NonFunctional, o.IpBuyout,
            o.Members.Select(m => new OpportunityMemberDto(m.UserId, m.Role)).ToList(),
            o.Quotes.OrderBy(q => q.Major).ThenBy(q => q.Minor).Select(q => Summary(q, caller.Access)).ToList(),
            StrategyRules.Matrix[o.Category].Defense,
            isSales && o.Status == OpportunityStatus.Open,
            isSales && o.Status == OpportunityStatus.Open && o.Quotes.Count == 0,
            RowVersions.Format(o.RowVersion));
    }
}
