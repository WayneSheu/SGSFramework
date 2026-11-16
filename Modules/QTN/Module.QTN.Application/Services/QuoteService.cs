using Microsoft.EntityFrameworkCore;
using SGS.Modules.QTN.Application.Contracts;
using SGS.Modules.QTN.Application.Rules;
using SGS.Modules.QTN.Infrastructure.Dbcontexts;
using SGS.Modules.QTN.Infrastructure.Entities;
using SGSFramework.Core.Paginations;
using SGSFramework.Core.ProjectContracts;

namespace SGS.Modules.QTN.Application.Services;

/// <summary>
/// 報價版本：WBS 拆解與 PERT 估時、費率快照、定價與毛利、欄位級權限投影（本檔）；送審、審批、版本衍生、發出與成交、轉合約（QuoteService.Workflow.cs）。
/// 僅草稿可編修；所有金額由後端依規則重算。
/// </summary>
public sealed partial class QuoteService(
    QTNDbContext db,
    QtnAccess access,
    QtnMailNotifier notifier,
    IQuoteContractIntegration contracts,
    TimeProvider clock)
{
    private const int MaxPageSize = 200;
    private const int MaxWbsItems = 500;
    private const int MaxWbsDepth = 6;
    private const int MaxAllocationsPerItem = 10;
    private const int MaxLogs = 300;
    private const int NumberRetries = 3;

    // ======================= 查詢 =======================

    public async Task<PagedResult<QuoteListDto>> SearchAsync(QtnCaller caller, QuoteSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var filter = request.Filter ?? new QuoteFilter();

        var isApprover = await access.IsApproverAsync(caller.UserId, ct).ConfigureAwait(false);
        var query = db.QuoteVersions.AsNoTracking();
        if (!isApprover) query = query.Where(q => q.Opportunity!.Members.Any(m => m.UserId == caller.UserId));
        if (filter.Status is { Count: > 0 } statuses) query = query.Where(q => statuses.Contains(q.Status));
        if (filter.PricingModel is { Count: > 0 } models) query = query.Where(q => models.Contains(q.PricingModel));
        if (filter.MarginLight is { Count: > 0 } lights && caller.Access.SeesPrice()) query = query.Where(q => lights.Contains(q.MarginLight));
        if (filter.LatestOnly)
            query = query.Where(q => !db.QuoteVersions.Any(o => o.OpportunityId == q.OpportunityId && o.IsChangeRequest == q.IsChangeRequest && o.Minor > q.Minor));
        if (filter.PendingMyApproval) query = query.Where(q => q.Status == QuoteStatus.PendingApproval);
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(q => q.QuoteNo.Contains(term) || q.Title.Contains(term) || q.Opportunity!.OpportunityNo.Contains(term) || q.Opportunity.Customer!.Name.Contains(term));
        }

        var ordered = query.OrderBy(q => q.Status == QuoteStatus.PendingApproval ? 0 : 1).ThenByDescending(q => q.UpdatedAtUtc ?? q.CreatedAtUtc).ThenBy(q => q.Id);
        var approverRoles = await access.ApproverRolesAsync(caller.UserId, ct).ConfigureAwait(false);

        if (filter.PendingMyApproval)
        {
            var all = await ordered.Include(q => q.Opportunity!).ThenInclude(o => o.Customer).Include(q => q.Opportunity!).ThenInclude(o => o.Members)
                .Include(q => q.ApprovalSteps).AsSplitQuery().ToListAsync(ct).ConfigureAwait(false);
            var mine = all.Select(q => ToListDto(q, caller, approverRoles)).Where(d => d.PendingMyApproval).ToList();
            return new PagedResult<QuoteListDto>(mine.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList(), mine.Count, pageIndex, pageSize);
        }

        var total = await ordered.CountAsync(ct).ConfigureAwait(false);
        var rows = await ordered.Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Include(q => q.Opportunity!).ThenInclude(o => o.Customer).Include(q => q.Opportunity!).ThenInclude(o => o.Members)
            .Include(q => q.ApprovalSteps).AsSplitQuery().ToListAsync(ct).ConfigureAwait(false);
        return new PagedResult<QuoteListDto>(rows.Select(q => ToListDto(q, caller, approverRoles)).ToList(), total, pageIndex, pageSize);
    }

    public async Task<QtnResult<QuoteDto>> GetAsync(QtnCaller caller, Guid id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var loaded = await LoadAsync(caller, id, track: false, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        return QtnResult<QuoteDto>.Success(await ToDtoAsync(loaded.Value!, caller, ct).ConfigureAwait(false));
    }

    // ======================= 建立 =======================

    /// <summary>建立商機第一版報價（v1.0）：依計價模式帶入預設條款、SLA、付款里程碑與計價參數，並可由標準 WBS 模組匯入。</summary>
    public async Task<QtnResult<QuoteDto>> CreateAsync(QtnCaller caller, Guid opportunityId, CreateQuoteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var o = await db.Opportunities.Include(x => x.Members).Include(x => x.Quotes).Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == opportunityId, ct).ConfigureAwait(false);
        if (o is null || !await access.CanViewAsync(o, caller.UserId, ct).ConfigureAwait(false)) return QtnError.NotFound("商機不存在。");
        if (!QtnAccess.Has(o, caller.UserId, OpportunityRole.Sales)) return QtnError.Forbidden("僅商機業務可建立報價。");
        if (o.Status != OpportunityStatus.Open) return QtnError.Conflict("QTN.INVALID_TRANSITION", "商機已結案。");
        if (o.Quotes.Count > 0) return QtnError.Conflict("QTN.INVALID_TRANSITION", "此商機已有報價，請由既有版本衍生新版本。");
        if (request.Title is { Length: > 200 }) return QtnError.Validation("報價標題最多 200 字。");

        var now = clock.GetUtcNow();
        var today = Workdays.TaipeiToday(now);

        for (var attempt = 1; attempt <= NumberRetries; attempt++)
        {
            var prefix = QuoteNumbering.QuotePrefix(o.PricingModel, today);
            var existing = await db.QuoteVersions.AsNoTracking().Where(q => q.QuoteNo.StartsWith(prefix)).Select(q => q.QuoteNo).Distinct()
                .ToListAsync(ct).ConfigureAwait(false);

            var q = new QuoteVersion
            {
                Id = Guid.NewGuid(), OpportunityId = o.Id, QuoteNo = QuoteNumbering.NextQuoteNo(o.PricingModel, today, existing),
                Major = 1, Minor = 0, VersionLabel = QuoteWorkflow.Label(1, 0, false), PricingModel = o.PricingModel,
                Title = string.IsNullOrWhiteSpace(request.Title) ? o.Title : request.Title.Trim(), Status = QuoteStatus.Draft,
                TaxRate = PricingCalculator.DefaultTaxRate, CreatedBy = caller.Name, CreatedAtUtc = now
            };
            ApplyModelDefaults(q, today);
            SeedClauses(q, o.IpBuyout, caller.Name, now);
            SeedMilestones(q, caller.Name, now);

            if (request.TemplateIds is { Count: > 0 } templateIds)
            {
                var imported = await AppendTemplatesAsync(q, templateIds, caller.Name, now, ct).ConfigureAwait(false);
                if (imported is not null) return QtnError.Validation(imported);
            }

            QuoteCalculator.Recalculate(q, o.IpBuyout);
            db.QuoteVersions.Add(q);
            AddLog(q, QuoteLogAction.Created, caller, $"建立報價 {q.QuoteNo} {q.VersionLabel}");
            o.UpdatedBy = caller.Name;
            o.UpdatedAtUtc = now;

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return await GetAsync(caller, q.Id, ct).ConfigureAwait(false);
            }
            catch (DbUpdateException) when (attempt < NumberRetries)
            {
                db.ChangeTracker.Clear();
                o = await db.Opportunities.Include(x => x.Members).Include(x => x.Quotes).Include(x => x.Customer)
                    .FirstAsync(x => x.Id == opportunityId, ct).ConfigureAwait(false);
            }
        }

        return QtnError.Conflict("QTN.CONCURRENCY_CONFLICT", "報價單號產生衝突，請稍後再試。");
    }

    // ======================= 編修（草稿） =======================

    /// <summary>儲存草稿（null 區段不變）；<paramref name="preview"/>＝true 時僅試算並回傳結果，不寫入。</summary>
    public async Task<QtnResult<QuoteDto>> EditAsync(QtnCaller caller, Guid id, QuoteEditRequest request, bool preview, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var q = loaded.Value!;
        var o = q.Opportunity!;

        var editable = QuoteWorkflow.CanEdit(q.Status, q.IsLocked);
        if (!editable.IsOk) return QtnError.FromRule(editable);
        if (!RowVersions.Matches(q.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(q).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        var canEstimate = CanEditEstimate(q, caller);
        var canPrice = CanEditPrice(q, caller);
        var touchesPrice = request.Terms is not null || request.DiscountAmount is not null || request.DiscountReason is not null || request.IpLicenseFee is not null
            || request.SpecialApproval is not null || request.SpecialApprovalReason is not null || request.Expenses is not null || request.Milestones is not null
            || request.ResetMilestones || request.Clauses is not null || request.SlaLevels is not null;
        if (request.Wbs is not null && !canEstimate) return QtnError.Forbidden("僅商機 Tech Lead／PM 且具估時權限者可編修 WBS 與工時。");
        if (touchesPrice && !canPrice) return QtnError.Forbidden("僅商機業務／財務且具報價編修權限者可編修金額、條款與付款里程碑。");
        if (request.Title is not null && !canEstimate && !canPrice) return QtnError.Forbidden("您不可編修此報價。");

        var now = clock.GetUtcNow();
        var sections = new List<string>();

        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200) return QtnError.Validation("報價標題必填，最多 200 字。");
            q.Title = request.Title.Trim();
            sections.Add("標題");
        }

        if (request.Terms is { } terms)
        {
            var invalid = ApplyTerms(q, terms);
            if (invalid is not null) return QtnError.Validation(invalid);
            sections.Add("計價參數");
        }

        if (request.DiscountAmount is { } discount)
        {
            if (discount < 0) return QtnError.Validation("折扣金額不可為負數。");
            q.DiscountAmount = discount;
            sections.Add("折扣");
        }

        if (request.DiscountReason is not null)
        {
            if (request.DiscountReason.Length > 1000) return QtnError.Validation("折扣說明最多 1000 字。");
            q.DiscountReason = MasterDataService.Clean(request.DiscountReason);
        }

        if (request.IpLicenseFee is { } ipFee)
        {
            if (ipFee < 0) return QtnError.Validation("IP 授權費不可為負數。");
            q.IpLicenseFee = ipFee;
        }

        if (request.SpecialApproval is { } special)
        {
            if (special && !(o.Customer?.IsStrategic ?? false)) return QtnError.Validation("僅戰略標竿客戶可申請毛利特批。");
            q.SpecialApproval = special;
            sections.Add("特批");
        }

        if (request.SpecialApprovalReason is not null)
        {
            if (request.SpecialApprovalReason.Length > 1000) return QtnError.Validation("特批理由最多 1000 字。");
            q.SpecialApprovalReason = MasterDataService.Clean(request.SpecialApprovalReason);
        }

        if (request.Notes is not null)
        {
            if (request.Notes.Length > 2000) return QtnError.Validation("備註最多 2000 字。");
            q.Notes = MasterDataService.Clean(request.Notes);
        }

        if (request.Wbs is { } wbs)
        {
            var invalid = await ReplaceWbsAsync(q, wbs, caller.Name, now, ct).ConfigureAwait(false);
            if (invalid is not null) return QtnError.Validation(invalid);
            sections.Add("WBS 與工時");
        }

        if (request.Expenses is { } expenses)
        {
            var invalid = ReplaceExpenses(q, expenses, caller.Name, now);
            if (invalid is not null) return QtnError.Validation(invalid);
            sections.Add("直接費用");
        }

        if (request.Clauses is { } clauses)
        {
            var invalid = ReplaceClauses(q, clauses, caller.Name, now);
            if (invalid is not null) return QtnError.Validation(invalid);
            sections.Add("商業條款");
        }

        if (request.SlaLevels is { } sla)
        {
            var invalid = ReplaceSla(q, sla, caller.Name, now);
            if (invalid is not null) return QtnError.Validation(invalid);
            sections.Add("SLA");
        }

        // 里程碑依重算後（含 WBS 與計價參數）的金額分配；重設預設值時混合計價需先知道第一階段佔比
        QuoteCalculator.Recalculate(q, o.IpBuyout);
        if (request.ResetMilestones)
        {
            ClearMilestones(q);
            SeedMilestones(q, caller.Name, now);
            sections.Add("付款里程碑");
        }
        else if (request.Milestones is { } milestones)
        {
            var seeds = milestones.Select(m => new MilestoneSeed(m.Name ?? string.Empty, m.Percent, m.Trigger ?? string.Empty)).ToList();
            var invalid = MilestoneRules.Validate(seeds);
            if (invalid is not null) return QtnError.Validation(invalid);
            ClearMilestones(q);
            AddMilestones(q, seeds, caller.Name, now);
            sections.Add("付款里程碑");
        }

        QuoteCalculator.Recalculate(q, o.IpBuyout);

        if (preview)
        {
            var dto = await ToDtoAsync(q, caller, ct).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return QtnResult<QuoteDto>.Success(dto);
        }

        if (sections.Count == 0) return await GetAsync(caller, id, ct).ConfigureAwait(false);

        // 技術評估前置：WBS 或計價期數變動後須重新經 Tech Lead 複核
        if (request.Wbs is not null || request.Terms is not null)
        {
            q.TechReviewedBy = null;
            q.TechReviewedAt = null;
        }

        q.UpdatedBy = caller.Name;
        q.UpdatedAtUtc = now;
        AddLog(q, QuoteLogAction.Updated, caller, $"更新{string.Join("、", sections)}");
        return await SaveAndGetAsync(caller, q.Id, ct).ConfigureAwait(false);
    }

    /// <summary>由標準 WBS 工時庫匯入模組（附加為第一層節點，含預設三點工時與目前費率快照）。</summary>
    public async Task<QtnResult<QuoteDto>> ImportTemplateAsync(QtnCaller caller, Guid id, ImportTemplateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var q = loaded.Value!;

        var editable = QuoteWorkflow.CanEdit(q.Status, q.IsLocked);
        if (!editable.IsOk) return QtnError.FromRule(editable);
        if (!CanEditEstimate(q, caller)) return QtnError.Forbidden("僅商機 Tech Lead／PM 且具估時權限者可匯入工時模組。");
        if (!RowVersions.Matches(q.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(q).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        var now = clock.GetUtcNow();
        var invalid = await AppendTemplatesAsync(q, [request.TemplateId], caller.Name, now, ct).ConfigureAwait(false);
        if (invalid is not null) return QtnError.Validation(invalid);

        QuoteCalculator.Recalculate(q, q.Opportunity!.IpBuyout);
        q.TechReviewedBy = null;
        q.TechReviewedAt = null;
        q.UpdatedBy = caller.Name;
        q.UpdatedAtUtc = now;
        AddLog(q, QuoteLogAction.Updated, caller, "匯入標準 WBS 模組");
        return await SaveAndGetAsync(caller, q.Id, ct).ConfigureAwait(false);
    }

    // ======================= 編修輔助 =======================

    private static string? ApplyTerms(QuoteVersion q, QuoteTermsDto t)
    {
        if (t.ValidDays is < 1 or > 365) return "報價有效天數須介於 1–365。";
        if (t.TaxRate is < 0 or > 0.5m) return "稅率須介於 0–50%。";
        if (t.DurationWeeks is < 0 or > 520) return "預計工期須介於 0–520 週。";

        switch (q.PricingModel)
        {
            case PricingModel.TimeAndMaterials:
                if (t.SprintCount is not (>= 1 and <= 52)) return "T&M 產能包須填寫 1–52 個 Sprint。";
                break;
            case PricingModel.Retainer:
                if (t.RetainerMonths is not (>= 1 and <= 60)) return "維運合約月數須介於 1–60。";
                if (t.RetainerMonthlyDaysCap is not (>= 0 and <= 100)) return "每月維運人天上限須介於 0–100。";
                if (t.OverageDailyRate is < 0) return "超額費率不可為負數。";
                if (t.OffHoursMultiplier is not (>= 1 and <= 5)) return "非上班時間工時加乘須介於 1–5 倍。";
                break;
        }

        q.ValidDays = t.ValidDays;
        q.TaxRate = t.TaxRate;
        q.DurationWeeks = t.DurationWeeks;
        q.SprintCount = q.PricingModel == PricingModel.TimeAndMaterials ? t.SprintCount : null;
        if (q.PricingModel == PricingModel.Retainer)
        {
            q.RetainerMonths = t.RetainerMonths;
            q.RetainerStart = t.RetainerStart;
            q.RetainerMonthlyDaysCap = t.RetainerMonthlyDaysCap;
            q.OverageDailyRate = t.OverageDailyRate;
            q.OffHoursMultiplier = t.OffHoursMultiplier;
        }

        return null;
    }

    private static void ApplyModelDefaults(QuoteVersion q, DateOnly today)
    {
        switch (q.PricingModel)
        {
            case PricingModel.TimeAndMaterials:
                q.SprintCount = 3;
                q.DurationWeeks = 6;
                break;
            case PricingModel.Retainer:
                q.RetainerMonths = 12;
                q.RetainerStart = new DateOnly(today.Year, today.Month, 1).AddMonths(1);
                q.RetainerMonthlyDaysCap = 4;
                q.OverageDailyRate = 16_500m;
                q.OffHoursMultiplier = 1.5m;
                break;
            case PricingModel.FixedPrice:
                q.DurationWeeks = 12;
                break;
        }
    }

    private static void SeedClauses(QuoteVersion q, bool ipBuyout, string actor, DateTimeOffset now)
    {
        var order = 0;
        foreach (var c in ClauseCatalog.Defaults(q.PricingModel, ipBuyout))
            q.Clauses.Add(new QuoteClause { Id = Guid.NewGuid(), SortOrder = ++order, Code = c.Code, Title = c.Title, Body = c.Body, CreatedBy = actor, CreatedAtUtc = now });
        foreach (var s in ClauseCatalog.DefaultSla(q.PricingModel))
            q.SlaLevels.Add(new QuoteSlaLevel
            {
                Id = Guid.NewGuid(), Level = s.Level, Definition = s.Definition, ResponseTime = s.ResponseTime, ResolveTarget = s.ResolveTarget,
                CreatedBy = actor, CreatedAtUtc = now
            });
    }

    private static void SeedMilestones(QuoteVersion q, string actor, DateTimeOffset now) =>
        AddMilestones(q, MilestoneRules.Defaults(q.PricingModel, QuoteCalculator.PeriodCount(q), QuoteCalculator.StageOneShare(q)), actor, now);

    private static void AddMilestones(QuoteVersion q, IReadOnlyList<MilestoneSeed> seeds, string actor, DateTimeOffset now)
    {
        var seq = 0;
        foreach (var s in seeds)
            q.Milestones.Add(new QuotePaymentMilestone
            {
                Id = Guid.NewGuid(), Seq = ++seq, Name = s.Name.Trim(), Percent = s.Percent, Trigger = s.Trigger?.Trim() ?? string.Empty,
                CreatedBy = actor, CreatedAtUtc = now
            });
    }

    private void ClearMilestones(QuoteVersion q)
    {
        db.QuotePaymentMilestones.RemoveRange(q.Milestones);
        q.Milestones.Clear();
    }

    private async Task<string?> AppendTemplatesAsync(QuoteVersion q, IReadOnlyList<Guid> templateIds, string actor, DateTimeOffset now, CancellationToken ct)
    {
        var ids = templateIds.Distinct().ToList();
        if (ids.Count > 20) return "一次最多匯入 20 個模組。";
        var templates = await db.WbsTemplates.AsNoTracking().Include(t => t.Items).Where(t => ids.Contains(t.Id) && t.IsActive)
            .ToListAsync(ct).ConfigureAwait(false);
        if (templates.Count != ids.Count) return "標準模組不存在或已停用。";

        var roleIds = templates.SelectMany(t => t.Items).Select(i => i.RateRoleId).Distinct().ToList();
        var rates = await RatesForAsync(q, roleIds, refresh: false, ct).ConfigureAwait(false);
        if (rates.Count != roleIds.Count) return "模組中的投入角色已停用或不存在，請先更新標準工時庫。";

        var order = q.WbsItems.Where(w => w.ParentId is null).Select(w => w.SortOrder).DefaultIfEmpty(0).Max();
        foreach (var t in ids.Select(id => templates.First(x => x.Id == id)))
        {
            if (q.WbsItems.Count + t.Items.Count + 1 > MaxWbsItems) return $"WBS 項目最多 {MaxWbsItems} 項。";
            var parent = new QuoteWbsItem
            {
                Id = Guid.NewGuid(), QuoteVersionId = q.Id, ParentId = null, SortOrder = ++order, Name = t.Name, Deliverable = t.Description,
                Phase = t.Items.OrderBy(i => i.SortOrder).Select(i => i.Phase).FirstOrDefault(), CreatedBy = actor, CreatedAtUtc = now
            };
            q.WbsItems.Add(parent);

            var childOrder = 0;
            foreach (var item in t.Items.OrderBy(i => i.SortOrder))
            {
                var child = new QuoteWbsItem
                {
                    Id = Guid.NewGuid(), QuoteVersionId = q.Id, ParentId = parent.Id, SortOrder = ++childOrder, Name = item.Name, Deliverable = item.Deliverable,
                    Phase = item.Phase, TemplateItemId = item.Id, CreatedBy = actor, CreatedAtUtc = now
                };
                var rate = rates[item.RateRoleId];
                var allocation = new WbsRoleAllocation
                {
                    Id = Guid.NewGuid(), WbsItemId = child.Id, RateRoleId = item.RateRoleId, RoleName = rate.Name, Optimistic = item.Optimistic,
                    MostLikely = item.MostLikely, Pessimistic = item.Pessimistic, SnapshotCostRate = rate.Cost, SnapshotListRate = rate.List,
                    CreatedBy = actor, CreatedAtUtc = now
                };
                QuoteCalculator.ApplyPert(child, allocation);
                child.Allocations.Add(allocation);
                q.WbsItems.Add(child);
            }
        }

        QuoteCalculator.AssignWbsCodes(q.WbsItems);
        return null;
    }

    /// <summary>
    /// 費率快照（SOP 1.2 數據快照原則）：同一報價中已使用的角色沿用既有快照；新加入的角色取目前費率。
    /// <paramref name="refresh"/>＝true（衍生新版本時選擇改用新費率）則一律取目前費率。
    /// </summary>
    private async Task<Dictionary<Guid, (string Name, decimal Cost, decimal List)>> RatesForAsync(
        QuoteVersion q, IReadOnlyCollection<Guid> roleIds, bool refresh, CancellationToken ct)
    {
        var result = new Dictionary<Guid, (string, decimal, decimal)>();
        if (!refresh)
            foreach (var a in q.WbsItems.SelectMany(w => w.Allocations).Where(a => roleIds.Contains(a.RateRoleId)))
                result.TryAdd(a.RateRoleId, (a.RoleName, a.SnapshotCostRate, a.SnapshotListRate));

        var missing = roleIds.Where(r => !result.ContainsKey(r)).ToList();
        if (missing.Count > 0)
        {
            var current = await db.RateRoles.AsNoTracking().Where(r => missing.Contains(r.Id) && r.IsActive).ToListAsync(ct).ConfigureAwait(false);
            foreach (var r in current) result[r.Id] = (r.Name, r.BaseCostRate, r.BaseListRate);
        }

        return result;
    }

    private async Task<string?> ReplaceWbsAsync(QuoteVersion q, IReadOnlyList<WbsItemInput> inputs, string actor, DateTimeOffset now, CancellationToken ct)
    {
        if (inputs.Count > MaxWbsItems) return $"WBS 項目最多 {MaxWbsItems} 項。";

        var keys = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var depth = new Dictionary<string, int>(StringComparer.Ordinal);
        var hasChildren = inputs.Where(i => i.ParentKey is not null).Select(i => i.ParentKey!).ToHashSet(StringComparer.Ordinal);

        foreach (var i in inputs)
        {
            if (string.IsNullOrWhiteSpace(i.Key) || keys.ContainsKey(i.Key)) return "WBS 節點鍵值重複或空白。";
            if (i.ParentKey is not null && !keys.ContainsKey(i.ParentKey)) return "WBS 上層節點須排在下層節點之前，且必須存在。";
            var d = i.ParentKey is null ? 1 : depth[i.ParentKey] + 1;
            if (d > MaxWbsDepth) return $"WBS 最多 {MaxWbsDepth} 層。";
            if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Trim().Length > 200) return "WBS 名稱必填，最多 200 字。";
            if (i.Deliverable is { Length: > 1000 }) return "核心交付物說明最多 1000 字。";
            if (!Enum.IsDefined(i.Phase)) return $"「{i.Name}」階段不正確。";
            if (i.Stage is not (1 or 2) || (i.Stage == 2 && q.PricingModel != PricingModel.Hybrid)) return $"「{i.Name}」：僅混合計價可使用第二階段。";
            if (!PertCalculator.IsValidTechFactor(i.KTech)) return $"「{i.Name}」技術新穎度係數僅可為 1.0／1.2／1.5。";
            if (!PertCalculator.IsValidRequirementFactor(i.KReq)) return $"「{i.Name}」需求明確度係數僅可為 1.0／1.3／1.6。";
            var allocations = i.Allocations ?? [];
            if (allocations.Count > 0 && hasChildren.Contains(i.Key)) return $"「{i.Name}」有下層項目，工時請配置於最下層項目。";
            if (allocations.Count > MaxAllocationsPerItem) return $"「{i.Name}」角色配置最多 {MaxAllocationsPerItem} 筆。";
            if (allocations.GroupBy(a => a.RateRoleId).Any(g => g.Count() > 1)) return $"「{i.Name}」同一角色只能配置一次。";
            foreach (var a in allocations)
            {
                var pert = PertCalculator.Validate(a.Optimistic, a.MostLikely, a.Pessimistic);
                if (pert is not null) return $"「{i.Name}」：{pert}";
            }

            keys[i.Key] = Guid.NewGuid();
            depth[i.Key] = d;
        }

        var roleIds = inputs.SelectMany(i => i.Allocations ?? []).Select(a => a.RateRoleId).Distinct().ToList();
        var rates = await RatesForAsync(q, roleIds, refresh: false, ct).ConfigureAwait(false);
        if (rates.Count != roleIds.Count) return "配置的角色不存在或已停用。";

        var templateIds = inputs.Where(i => i.TemplateItemId is not null).Select(i => i.TemplateItemId!.Value).Distinct().ToList();
        var validTemplates = templateIds.Count == 0
            ? new HashSet<Guid>()
            : (await db.WbsTemplateItems.AsNoTracking().Where(t => templateIds.Contains(t.Id)).Select(t => t.Id).ToListAsync(ct).ConfigureAwait(false)).ToHashSet();

        db.QuoteWbsItems.RemoveRange(q.WbsItems);
        q.WbsItems.Clear();

        var sortByParent = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var i in inputs)
        {
            var parentKey = i.ParentKey ?? string.Empty;
            sortByParent[parentKey] = sortByParent.GetValueOrDefault(parentKey) + 1;

            var item = new QuoteWbsItem
            {
                Id = keys[i.Key], QuoteVersionId = q.Id, ParentId = i.ParentKey is null ? null : keys[i.ParentKey], SortOrder = sortByParent[parentKey],
                Name = i.Name.Trim(), Deliverable = MasterDataService.Clean(i.Deliverable), Phase = i.Phase, Stage = i.Stage, KTech = i.KTech, KReq = i.KReq,
                TemplateItemId = i.TemplateItemId is { } tid && validTemplates.Contains(tid) ? tid : null, CreatedBy = actor, CreatedAtUtc = now
            };

            foreach (var a in i.Allocations ?? [])
            {
                var rate = rates[a.RateRoleId];
                var allocation = new WbsRoleAllocation
                {
                    Id = Guid.NewGuid(), WbsItemId = item.Id, RateRoleId = a.RateRoleId, RoleName = rate.Name, Optimistic = a.Optimistic,
                    MostLikely = a.MostLikely, Pessimistic = a.Pessimistic, SnapshotCostRate = rate.Cost, SnapshotListRate = rate.List,
                    CreatedBy = actor, CreatedAtUtc = now
                };
                QuoteCalculator.ApplyPert(item, allocation);
                item.Allocations.Add(allocation);
            }

            q.WbsItems.Add(item);
            db.QuoteWbsItems.Add(item);
        }

        QuoteCalculator.AssignWbsCodes(q.WbsItems);
        return null;
    }

    private string? ReplaceExpenses(QuoteVersion q, IReadOnlyList<ExpenseInput> inputs, string actor, DateTimeOffset now)
    {
        if (inputs.Count > 100) return "直接費用最多 100 筆。";
        foreach (var e in inputs)
        {
            if (!Enum.IsDefined(e.Category)) return "費用類別不正確。";
            if (string.IsNullOrWhiteSpace(e.Description) || e.Description.Trim().Length > 300) return "費用說明必填，最多 300 字。";
            if (e.Amount is < 0 or > 10_000_000_000m) return "費用金額不正確。";
        }

        db.QuoteDirectExpenses.RemoveRange(q.Expenses);
        q.Expenses.Clear();
        foreach (var e in inputs)
            q.Expenses.Add(new QuoteDirectExpense
            {
                Id = Guid.NewGuid(), QuoteVersionId = q.Id, Category = e.Category, Description = e.Description.Trim(), Amount = PricingCalculator.Round0(e.Amount),
                IsCapped = e.IsCapped, CreatedBy = actor, CreatedAtUtc = now
            });
        return null;
    }

    private string? ReplaceClauses(QuoteVersion q, IReadOnlyList<ClauseInput> inputs, string actor, DateTimeOffset now)
    {
        if (inputs.Count > 30) return "商業條款最多 30 條。";
        foreach (var c in inputs)
        {
            if (string.IsNullOrWhiteSpace(c.Code) || c.Code.Trim().Length > 40) return "條款代碼必填，最多 40 字。";
            if (string.IsNullOrWhiteSpace(c.Title) || c.Title.Trim().Length > 100) return "條款標題必填，最多 100 字。";
            if (string.IsNullOrWhiteSpace(c.Body) || c.Body.Trim().Length > 4000) return "條款內容必填，最多 4000 字。";
        }

        db.QuoteClauses.RemoveRange(q.Clauses);
        q.Clauses.Clear();
        var order = 0;
        foreach (var c in inputs)
            q.Clauses.Add(new QuoteClause
            {
                Id = Guid.NewGuid(), QuoteVersionId = q.Id, SortOrder = ++order, Code = c.Code.Trim().ToUpperInvariant(), Title = c.Title.Trim(), Body = c.Body.Trim(),
                CreatedBy = actor, CreatedAtUtc = now
            });
        return null;
    }

    private string? ReplaceSla(QuoteVersion q, IReadOnlyList<SlaLevelDto> inputs, string actor, DateTimeOffset now)
    {
        if (inputs.Count > 10) return "SLA 等級最多 10 級。";
        if (inputs.Any(s => string.IsNullOrWhiteSpace(s.Level) || s.Level.Length > 10 || string.IsNullOrWhiteSpace(s.Definition) || s.Definition.Length > 500
                            || string.IsNullOrWhiteSpace(s.ResponseTime) || s.ResponseTime.Length > 50 || string.IsNullOrWhiteSpace(s.ResolveTarget) || s.ResolveTarget.Length > 50))
            return "SLA 等級、定義、回應時間與解決時間皆必填（等級 10 字、定義 500 字、時間 50 字以內）。";

        db.QuoteSlaLevels.RemoveRange(q.SlaLevels);
        q.SlaLevels.Clear();
        foreach (var s in inputs)
            q.SlaLevels.Add(new QuoteSlaLevel
            {
                Id = Guid.NewGuid(), QuoteVersionId = q.Id, Level = s.Level.Trim(), Definition = s.Definition.Trim(), ResponseTime = s.ResponseTime.Trim(),
                ResolveTarget = s.ResolveTarget.Trim(), CreatedBy = actor, CreatedAtUtc = now
            });
        return null;
    }

    // ======================= 載入、權限、投影 =======================

    private async Task<QtnResult<QuoteVersion>> LoadAsync(QtnCaller caller, Guid id, bool track, CancellationToken ct)
    {
        IQueryable<QuoteVersion> query = db.QuoteVersions;
        if (!track) query = query.AsNoTracking();
        var q = await query
            .Include(x => x.Opportunity!).ThenInclude(o => o.Members)
            .Include(x => x.Opportunity!).ThenInclude(o => o.Customer)
            .Include(x => x.WbsItems).ThenInclude(w => w.Allocations)
            .Include(x => x.Expenses).Include(x => x.Milestones).Include(x => x.Clauses).Include(x => x.SlaLevels)
            .Include(x => x.ApprovalSteps).Include(x => x.Attachments)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);

        return q is null || !await access.CanViewAsync(q.Opportunity!, caller.UserId, ct).ConfigureAwait(false)
            ? QtnError.NotFound("報價不存在。")
            : QtnResult<QuoteVersion>.Success(q);
    }

    private static bool CanEditEstimate(QuoteVersion q, QtnCaller caller) =>
        QuoteWorkflow.IsEditable(q.Status, q.IsLocked) && caller.CanEditEstimate && QtnAccess.Has(q.Opportunity!, caller.UserId, OpportunityRole.TechLead, OpportunityRole.Pm);

    private static bool CanEditPrice(QuoteVersion q, QtnCaller caller) =>
        QuoteWorkflow.IsEditable(q.Status, q.IsLocked) && caller.CanEditPrice && caller.Access.SeesPrice()
        && QtnAccess.Has(q.Opportunity!, caller.UserId, OpportunityRole.Sales, OpportunityRole.Finance);

    private async Task<QtnResult<QuoteDto>> SaveAndGetAsync(QtnCaller caller, Guid id, CancellationToken ct)
    {
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

    private void AddLog(QuoteVersion q, QuoteLogAction action, QtnCaller caller, string? message, ApprovalRole? role = null) =>
        db.ApprovalLogs.Add(new ApprovalLog
        {
            Id = Guid.NewGuid(), QuoteVersionId = q.Id, Action = action, Role = role, Cycle = q.ApprovalCycle, ActorUserId = caller.UserId, ActorName = caller.Name,
            IpAddress = caller.IpAddress, At = clock.GetUtcNow(), Message = message, ContentHash = q.ContentHash
        });

    /// <summary>目前輪到的審批關卡（本輪最小序號的待決關卡）。</summary>
    internal static ApprovalStep? CurrentStep(QuoteVersion q) =>
        q.Status != QuoteStatus.PendingApproval
            ? null
            : q.ApprovalSteps.Where(s => s.Cycle == q.ApprovalCycle && s.State == ApprovalStepState.Pending).OrderBy(s => s.Seq).FirstOrDefault();

    private static bool HoldsRole(QuoteVersion q, ApprovalRole role, Guid userId, IReadOnlySet<ApprovalRole> approverRoles) =>
        role == ApprovalRole.TechLead ? QtnAccess.Has(q.Opportunity!, userId, OpportunityRole.TechLead) : approverRoles.Contains(role);

    private static (bool CanDecide, ApprovalRole? Role, string? Blocked) DecideState(QuoteVersion q, Guid userId, IReadOnlySet<ApprovalRole> approverRoles)
    {
        var step = CurrentStep(q);
        if (step is null) return (false, null, null);
        var holds = HoldsRole(q, step.Role, userId, approverRoles);
        if (!holds) return (false, null, null);
        var decided = q.ApprovalSteps.Count(s => s.Cycle == q.ApprovalCycle && s.DecidedBy == userId);
        var rule = ApprovalSod.CanDecide(userId, q.SubmittedBy, decided, holds);
        return rule.IsOk ? (true, step.Role, null) : (false, step.Role, rule.Detail);
    }

    private QuoteListDto ToListDto(QuoteVersion q, QtnCaller caller, IReadOnlySet<ApprovalRole> approverRoles)
    {
        var step = CurrentStep(q);
        var decide = DecideState(q, caller.UserId, approverRoles);
        return new QuoteListDto(
            q.Id, q.OpportunityId, q.Opportunity!.OpportunityNo, q.Title, q.Opportunity.Customer?.Name ?? string.Empty, q.QuoteNo, q.VersionLabel, q.IsChangeRequest,
            q.Status, q.PricingModel, q.TotalQuotedDays, caller.Access.Price(q.FinalPrice), caller.Access.SeesPrice() ? q.MarginLight : null,
            step?.Role, decide.CanDecide, q.ValidUntil, q.CreatedAtUtc, q.UpdatedAtUtc);
    }

    private async Task<QuoteDto> ToDtoAsync(QuoteVersion q, QtnCaller caller, CancellationToken ct)
    {
        var o = q.Opportunity!;
        var a = caller.Access;
        var approverRoles = await access.ApproverRolesAsync(caller.UserId, ct).ConfigureAwait(false);
        var today = Workdays.TaipeiToday(clock.GetUtcNow());
        var periods = QuoteCalculator.PeriodCount(q);

        var logs = await db.ApprovalLogs.AsNoTracking().Where(l => l.QuoteVersionId == q.Id).OrderByDescending(l => l.At).Take(MaxLogs)
            .ToListAsync(ct).ConfigureAwait(false);
        var siblingsOpen = await db.QuoteVersions.AsNoTracking()
            .AnyAsync(x => x.OpportunityId == q.OpportunityId && x.Id != q.Id && (x.Status == QuoteStatus.Draft || x.Status == QuoteStatus.PendingApproval), ct)
            .ConfigureAwait(false);

        var expected = QuoteCalculator.ExpectedDays(q);
        var totals = new QuoteTotalsDto(
            q.TotalQuotedDays, expected, StrategyRules.RiskFactor(expected, q.TotalQuotedDays),
            a.Cost(q.LaborCost), a.Cost(q.Overhead), a.Cost(q.TotalCost), a.SeesCost() ? q.MarginRate : null,
            a.Price(q.DirectExpense), a.Price(q.LaborListTotal), a.Price(q.IpLicenseFee), a.Price(q.ListTotal), a.Price(q.DiscountAmount),
            a.Price(q.FinalPrice), q.TaxRate, a.Price(q.TaxAmount), a.Price(q.FinalPriceWithTax),
            a.SeesPrice() ? PeriodPrice(q, periods) : null, a.SeesPrice() ? q.MarginLight : null, a.SeesPrice() ? q.BonusFactor : null);

        var wbs = q.WbsItems.OrderBy(w => w.WbsCode, WbsCodeComparer.Instance).Select(w =>
        {
            var allocations = w.Allocations.OrderBy(x => x.RoleName).Select(x => new AllocationDto(
                x.Id, x.RateRoleId, x.RoleName, x.Optimistic, x.MostLikely, x.Pessimistic, x.ExpectedDays, x.QuotedDays,
                a.Cost(x.SnapshotCostRate), a.Price(x.SnapshotListRate),
                a.Cost(PricingCalculator.Round0(x.QuotedDays * x.SnapshotCostRate)), a.Price(PricingCalculator.Round0(x.QuotedDays * x.SnapshotListRate)))).ToList();
            var subtree = Subtree(q, w.Id);
            return new WbsItemDto(
                w.Id, w.ParentId, w.SortOrder, w.WbsCode, w.Name, w.Deliverable, w.Phase, w.Stage, w.KTech, w.KReq, w.TemplateItemId, allocations,
                subtree.Sum(s => s.Allocations.Sum(x => x.QuotedDays)),
                a.Price(subtree.Sum(s => s.Allocations.Sum(x => PricingCalculator.Round0(x.QuotedDays * x.SnapshotListRate)))));
        }).ToList();

        var ratios = PhaseRatioRules.Evaluate(q.WbsItems.SelectMany(w => w.Allocations.Select(x => (w.Phase, x.QuotedDays))));
        var evaluation = MarginLightPolicy.Evaluate(q.FinalPrice, q.MarginRate, o.IpBuyout, q.SpecialApproval);
        var warnings = new List<string>();
        warnings.AddRange(PhaseRatioRules.Warnings(q.PricingModel, ratios));
        warnings.AddRange(StrategyRules.Warnings(o.Category, q.PricingModel, totals.RiskFactor, a.SeesCost() ? q.MarginRate : null));
        var blocking = SubmitBlockers(q, ratios, evaluation, a);

        var decide = DecideState(q, caller.UserId, approverRoles);
        var isSales = QtnAccess.Has(o, caller.UserId, OpportunityRole.Sales);
        var isMember = o.Members.Any(m => m.UserId == caller.UserId);
        var editable = QuoteWorkflow.IsEditable(q.Status, q.IsLocked);

        var actions = new QuoteActionsDto(
            CanEditEstimate(q, caller),
            CanEditPrice(q, caller),
            editable && caller.CanEditEstimate && QtnAccess.Has(o, caller.UserId, OpportunityRole.TechLead) && q.WbsItems.Count > 0,
            editable && isSales && caller.CanEditPrice && a.SeesPrice() && q.TechReviewedAt is not null,
            decide.CanDecide, decide.Role, decide.Blocked,
            QuoteWorkflow.CanRevise(q.Status) && !siblingsOpen && QtnAccess.Has(o, caller.UserId, OpportunityRole.Sales, OpportunityRole.TechLead, OpportunityRole.Pm),
            q.Status == QuoteStatus.Approved && isSales,
            isSales && (QuoteWorkflow.CanMarkWon(q.Status, q.ValidUntil, today).IsOk || QuoteWorkflow.CanMarkLost(q.Status).IsOk),
            QuoteWorkflow.CanCreateChangeRequest(q.Status) && QtnAccess.Has(o, caller.UserId, OpportunityRole.Sales, OpportunityRole.Pm, OpportunityRole.TechLead),
            q.Status == QuoteStatus.Won && !q.IsChangeRequest && q.PmsContractId is null && isSales && contracts.IsAvailable,
            a.SeesPrice(),
            a.SeesCost(),
            isMember && q.Status != QuoteStatus.Superseded);

        return new QuoteDto(
            q.Id, q.OpportunityId, o.OpportunityNo, o.Title, o.Customer?.Name ?? string.Empty, o.Category, o.IpBuyout,
            q.QuoteNo, q.VersionLabel, q.IsChangeRequest, q.ParentVersionId, q.Status, q.PricingModel, q.Title,
            q.IsLocked, q.TechReviewedBy, q.TechReviewedAt, q.TechReviewNote,
            totals, a.SeesPrice() ? q.DiscountReason : null, q.SpecialApproval, a.SeesPrice() ? q.SpecialApprovalReason : null,
            new QuoteTermsDto(q.SprintCount, q.RetainerMonths, q.RetainerStart, q.RetainerMonthlyDaysCap, a.SeesPrice() ? q.OverageDailyRate : null,
                q.OffHoursMultiplier, q.DurationWeeks, q.ValidDays, q.TaxRate),
            q.IssuedDate, q.ValidUntil, q.ApprovalCycle, q.ContentHash, q.SubmittedBy, q.SubmittedAt, q.ApprovedAt, q.OutcomeNote, q.PmsContractId, q.PmsContractNo,
            a.SeesPrice() ? q.Notes : null,
            wbs,
            q.Expenses.OrderBy(e => e.Category).ThenBy(e => e.Description).Select(e => new ExpenseDto(e.Id, e.Category, e.Description, a.Price(e.Amount), e.IsCapped)).ToList(),
            q.Milestones.OrderBy(m => m.Seq).Select(m => new MilestoneDto(m.Seq, m.Name, m.Percent, a.Price(m.Amount), a.Price(m.AmountWithTax), m.Trigger)).ToList(),
            q.Clauses.OrderBy(c => c.SortOrder).Select(c => new ClauseDto(c.Id, c.SortOrder, c.Code, c.Title, c.Body)).ToList(),
            q.SlaLevels.OrderBy(s => s.Level).Select(s => new SlaLevelDto(s.Level, s.Definition, s.ResponseTime, s.ResolveTarget)).ToList(),
            new QuoteEvaluationDto(ratios, warnings, blocking, a.SeesPrice() ? evaluation.Route : null, evaluation.RequiresDiscountExplanation && a.SeesPrice()),
            q.ApprovalSteps.OrderBy(s => s.Cycle).ThenBy(s => s.Seq).Select(s => new ApprovalStepDto(
                s.Cycle, s.Seq, s.Role, MarginLightPolicy.RoleName(s.Role), s.State, s.DecidedBy, s.DecidedByName, s.DecidedAt, s.Comment)).ToList(),
            logs.Select(l => new QuoteLogDto(l.Action, l.Role, l.Cycle, l.ActorName, l.IpAddress, l.At, l.Message)).ToList(),
            q.Attachments.Where(x => !x.IsDeleted).OrderByDescending(x => x.CreatedAtUtc).Select(QuoteAttachmentService.ToDto).ToList(),
            a, actions, RowVersions.Format(q.RowVersion));
    }

    private static decimal PeriodPrice(QuoteVersion q, int periods) =>
        periods <= 1 ? q.ListTotal : PricingCalculator.Round0((q.LaborListTotal + q.DirectExpense) / periods);

    private static List<QuoteWbsItem> Subtree(QuoteVersion q, Guid rootId)
    {
        var result = new List<QuoteWbsItem>();
        var stack = new Stack<Guid>();
        stack.Push(rootId);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            var node = q.WbsItems.FirstOrDefault(w => w.Id == id);
            if (node is null) continue;
            result.Add(node);
            foreach (var child in q.WbsItems.Where(w => w.ParentId == id)) stack.Push(child.Id);
        }

        return result;
    }

    /// <summary>送審阻擋條件（同時供畫面提示與送審檢核）。</summary>
    private static List<string> SubmitBlockers(QuoteVersion q, IReadOnlyList<PhaseRatioDto> ratios, MarginEvaluation evaluation, QuoteFieldAccess fieldAccess)
    {
        var list = new List<string>();
        if (!q.WbsItems.Any(w => w.Allocations.Count > 0)) list.Add("尚未配置任何工時。");
        if (PhaseRatioRules.BlockingError(q.PricingModel, ratios) is { } phase && !q.IsChangeRequest) list.Add(phase);
        if (q.TechReviewedAt is null) list.Add("尚未經 Tech Lead 技術複核。");
        if (MilestoneRules.Validate(q.Milestones.Select(m => new MilestoneSeed(m.Name, m.Percent, m.Trigger)).ToList()) is { } milestone) list.Add(milestone);
        if (q.FinalPrice <= 0) list.Add("成交總價須大於 0。");
        if (fieldAccess.SeesPrice())
        {
            if (evaluation.Blocked) list.Add(evaluation.BlockReason!);
            if (evaluation.RequiresDiscountExplanation && string.IsNullOrWhiteSpace(q.DiscountReason)) list.Add("黃燈以上須填寫折扣說明（《折扣說明書》）。");
            if (q.SpecialApproval && string.IsNullOrWhiteSpace(q.SpecialApprovalReason)) list.Add("申請特批須填寫理由。");
        }

        return list;
    }

    /// <summary>WBS 編號自然排序（1.0 &lt; 1.1 &lt; 1.2 &lt; 1.10 &lt; 2.0）。</summary>
    internal sealed class WbsCodeComparer : IComparer<string>
    {
        public static readonly WbsCodeComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            var a = (x ?? string.Empty).Split('.');
            var b = (y ?? string.Empty).Split('.');
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                var ai = i < a.Length && int.TryParse(a[i], out var va) ? va : -1;
                var bi = i < b.Length && int.TryParse(b[i], out var vb) ? vb : -1;
                if (ai != bi) return ai.CompareTo(bi);
            }

            return 0;
        }
    }
}
