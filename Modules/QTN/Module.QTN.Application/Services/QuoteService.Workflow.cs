using Microsoft.EntityFrameworkCore;
using SGS.Modules.QTN.Application.Contracts;
using SGS.Modules.QTN.Application.Rules;
using SGS.Modules.QTN.Infrastructure.Entities;
using SGSFramework.Core.ProjectContracts;

namespace SGS.Modules.QTN.Application.Services;

public sealed partial class QuoteService
{
    // ======================= 技術評估前置 =======================

    /// <summary>Tech Lead 完成工時評估與技術可行性複核（SOP 1.2 技術評估前置原則）；之後 WBS 或期數變動會自動清除。</summary>
    public async Task<QtnResult<QuoteDto>> TechReviewAsync(QtnCaller caller, Guid id, TechReviewRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var q = loaded.Value!;

        var editable = QuoteWorkflow.CanEdit(q.Status, q.IsLocked);
        if (!editable.IsOk) return QtnError.FromRule(editable);
        if (!caller.CanEditEstimate || !QtnAccess.Has(q.Opportunity!, caller.UserId, OpportunityRole.TechLead))
            return QtnError.Forbidden("僅商機 Tech Lead 可執行技術複核。");
        if (!q.WbsItems.Any(w => w.Allocations.Count > 0)) return QtnError.Validation("尚未配置任何工時，無法複核。");
        if (request.Note is { Length: > 1000 }) return QtnError.Validation("複核意見最多 1000 字。");
        if (!RowVersions.Matches(q.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(q).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        var now = clock.GetUtcNow();
        q.TechReviewedBy = caller.UserId;
        q.TechReviewedAt = now;
        q.TechReviewNote = MasterDataService.Clean(request.Note);
        q.UpdatedBy = caller.Name;
        q.UpdatedAtUtc = now;
        AddLog(q, QuoteLogAction.TechReviewed, caller, $"技術複核完成（總報價人天 {q.TotalQuotedDays:0.##}）{(q.TechReviewNote is null ? string.Empty : "：" + q.TechReviewNote)}");
        return await SaveAndGetAsync(caller, id, ct).ConfigureAwait(false);
    }

    // ======================= 送審 =======================

    /// <summary>
    /// 送審：重算金額 → 檢核阻擋條件 → 依燈號（與 IP 條款）產生審批關卡 → 鎖定並凍結內容雜湊 → 通知第一關審批人。
    /// </summary>
    public async Task<QtnResult<QuoteDto>> SubmitAsync(QtnCaller caller, Guid id, SubmitQuoteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var q = loaded.Value!;
        var o = q.Opportunity!;

        if (!QtnAccess.Has(o, caller.UserId, OpportunityRole.Sales) || !caller.CanEditPrice || !caller.Access.SeesPrice())
            return QtnError.Forbidden("僅商機業務可送審報價。");
        var rule = QuoteWorkflow.CanSubmit(q.Status, q.TechReviewedAt is not null);
        if (!rule.IsOk) return QtnError.FromRule(rule);
        if (!RowVersions.Matches(q.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(q).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);
        if (request.Comment is { Length: > 1000 }) return QtnError.Validation("送審說明最多 1000 字。");

        QuoteCalculator.Recalculate(q, o.IpBuyout);
        var ratios = PhaseRatioRules.Evaluate(q.WbsItems.SelectMany(w => w.Allocations.Select(x => (w.Phase, x.QuotedDays))));
        var evaluation = MarginLightPolicy.Evaluate(q.FinalPrice, q.MarginRate, o.IpBuyout, q.SpecialApproval);
        var blockers = SubmitBlockers(q, ratios, evaluation, QuoteFieldAccess.Full);
        if (q.SpecialApproval && !(o.Customer?.IsStrategic ?? false)) blockers.Add("僅戰略標竿客戶可申請毛利特批。");
        if (blockers.Count > 0)
            return evaluation.Blocked
                ? QtnError.Conflict("QTN.MARGIN_LOCKED", string.Join(" ", blockers))
                : QtnError.Validation(string.Join(" ", blockers));

        // 審批人須已指派（Tech Lead 關卡＝商機 Tech Lead）
        if (!o.Members.Any(m => m.Role == OpportunityRole.TechLead)) return QtnError.Validation("商機尚未指派 Tech Lead，無法送審。");
        var assigned = await db.ApproverAssignments.AsNoTracking().Select(a => a.Role).Distinct().ToListAsync(ct).ConfigureAwait(false);
        var missing = evaluation.Route.Where(r => r != ApprovalRole.TechLead && !assigned.Contains(r)).Select(MarginLightPolicy.RoleName).ToList();
        if (missing.Count > 0) return QtnError.Validation($"審批權責矩陣尚未指派：{string.Join("、", missing)}。請管理員於「審批人設定」指派。");

        var now = clock.GetUtcNow();
        q.ApprovalCycle++;
        var seq = 0;
        foreach (var role in evaluation.Route)
            q.ApprovalSteps.Add(new ApprovalStep
            {
                Id = Guid.NewGuid(), QuoteVersionId = q.Id, Cycle = q.ApprovalCycle, Seq = ++seq, Role = role, State = ApprovalStepState.Pending,
                CreatedBy = caller.Name, CreatedAtUtc = now
            });

        q.Status = QuoteStatus.PendingApproval;
        q.IsLocked = true;
        q.SubmittedBy = caller.UserId;
        q.SubmittedAt = now;
        q.ContentHash = QuoteCalculator.ComputeHash(q);
        q.UpdatedBy = caller.Name;
        q.UpdatedAtUtc = now;

        var route = string.Join(" → ", evaluation.Route.Select(MarginLightPolicy.RoleName));
        AddLog(q, QuoteLogAction.Submitted, caller,
            $"送審第 {q.ApprovalCycle} 輪（{MarginLightPolicy.LightName(evaluation.Light)}；路徑 {route}）{(request.Comment is null ? string.Empty : "：" + request.Comment.Trim())}");
        if (q.SpecialApproval) AddLog(q, QuoteLogAction.SpecialApprovalRequested, caller, $"申請戰略客戶毛利特批：{q.SpecialApprovalReason}");

        var result = await SaveAndGetAsync(caller, id, ct).ConfigureAwait(false);
        if (result.IsSuccess) await NotifyStepAsync(q.Id, ct).ConfigureAwait(false);
        return result;
    }

    // ======================= 審批 =======================

    /// <summary>
    /// 審批（核准／退件）：須為目前關卡的審批人、非送審者、同輪未決定過其他關卡（職責分離）；決定前比對內容雜湊。
    /// 全部關卡核准 → Approved；任一關卡退件 → Rejected（須衍生新版本修改）。
    /// </summary>
    public async Task<QtnResult<QuoteDto>> DecideAsync(QtnCaller caller, Guid id, DecideQuoteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var q = loaded.Value!;

        var step = CurrentStep(q);
        if (step is null) return QtnError.Conflict("QTN.INVALID_TRANSITION", "此報價目前不在審批中。");
        if (!request.Approve && string.IsNullOrWhiteSpace(request.Comment)) return QtnError.Validation("退件須填寫意見。");
        if (request.Comment is { Length: > 1000 }) return QtnError.Validation("審批意見最多 1000 字。");

        var approverRoles = await access.ApproverRolesAsync(caller.UserId, ct).ConfigureAwait(false);
        var holds = HoldsRole(q, step.Role, caller.UserId, approverRoles);
        var decided = q.ApprovalSteps.Count(s => s.Cycle == q.ApprovalCycle && s.DecidedBy == caller.UserId);
        var sod = ApprovalSod.CanDecide(caller.UserId, q.SubmittedBy, decided, holds);
        if (!sod.IsOk) return QtnError.FromRule(sod);
        if (!RowVersions.Matches(q.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(q).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        // 防竄改：送審後內容不得變動
        var hash = QuoteCalculator.ComputeHash(q);
        if (!string.Equals(hash, q.ContentHash, StringComparison.Ordinal))
            return QtnError.Conflict("QTN.CONTENT_TAMPERED", "報價內容與送審時不一致，已拒絕審批，請通知系統管理員檢查稽核紀錄。");

        var now = clock.GetUtcNow();
        step.State = request.Approve ? ApprovalStepState.Approved : ApprovalStepState.Rejected;
        step.DecidedBy = caller.UserId;
        step.DecidedByName = caller.Name;
        step.DecidedAt = now;
        step.Comment = MasterDataService.Clean(request.Comment);
        step.ContentHash = hash;
        step.UpdatedBy = caller.Name;
        step.UpdatedAtUtc = now;
        AddLog(q, request.Approve ? QuoteLogAction.Approved : QuoteLogAction.Rejected, caller, step.Comment, step.Role);

        var fullyApproved = false;
        if (!request.Approve)
        {
            foreach (var rest in q.ApprovalSteps.Where(s => s.Cycle == q.ApprovalCycle && s.State == ApprovalStepState.Pending))
                rest.State = ApprovalStepState.Cancelled;
            q.Status = QuoteStatus.Rejected;
        }
        else if (!q.ApprovalSteps.Any(s => s.Cycle == q.ApprovalCycle && s.State == ApprovalStepState.Pending))
        {
            q.Status = QuoteStatus.Approved;
            q.ApprovedAt = now;
            fullyApproved = true;
            AddLog(q, QuoteLogAction.FullyApproved, caller, "全部審批關卡核准，報價成為正式報價單。");
        }

        q.UpdatedBy = caller.Name;
        q.UpdatedAtUtc = now;
        var result = await SaveAndGetAsync(caller, id, ct).ConfigureAwait(false);
        if (!result.IsSuccess) return result;

        if (q.Status == QuoteStatus.PendingApproval) await NotifyStepAsync(q.Id, ct).ConfigureAwait(false);
        else await NotifyMembersAsync(q.Id, fullyApproved ? "已核准" : "已退件", step.Comment, ct).ConfigureAwait(false);
        return result;
    }

    // ======================= 版本衍生 =======================

    /// <summary>
    /// 衍生新版本（SOP 1.2 版本不可變原則）：複製 WBS、配置（沿用費率快照，或選擇改用目前費率）、直接費用、里程碑、條款與計價參數，
    /// 新版本為草稿且須重新技術複核；已核准／已發出的來源版本轉為 Superseded。
    /// </summary>
    public async Task<QtnResult<QuoteDto>> ReviseAsync(QtnCaller caller, Guid id, ReviseQuoteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500) return QtnError.Validation("請填寫衍生新版本的原因（500 字以內）。");

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var source = loaded.Value!;
        var o = source.Opportunity!;

        if (!QtnAccess.Has(o, caller.UserId, OpportunityRole.Sales, OpportunityRole.TechLead, OpportunityRole.Pm))
            return QtnError.Forbidden("僅商機成員可衍生新版本。");
        if (!QuoteWorkflow.CanRevise(source.Status)) return QtnError.Conflict("QTN.INVALID_TRANSITION", "此狀態不可衍生新版本（草稿請直接修改；審批中請等待結果；已成交請走 CR）。");
        if (o.Status != OpportunityStatus.Open) return QtnError.Conflict("QTN.INVALID_TRANSITION", "商機已結案。");

        var siblings = await db.QuoteVersions.Where(x => x.OpportunityId == source.OpportunityId).ToListAsync(ct).ConfigureAwait(false);
        if (siblings.Any(x => x.Id != source.Id && x.Status is QuoteStatus.Draft or QuoteStatus.PendingApproval))
            return QtnError.Conflict("QTN.INVALID_TRANSITION", "此商機已有草稿或審批中的版本，請先處理該版本。");

        var now = clock.GetUtcNow();
        var next = QuoteWorkflow.NextVersion(siblings.Select(x => (x.Major, x.Minor)), source.IsChangeRequest);
        var copy = await CloneAsync(source, next, request.RefreshRates, caller.Name, now, ct).ConfigureAwait(false);
        if (copy.Error is { } cloneError) return cloneError;
        var q = copy.Value!;

        db.QuoteVersions.Add(q);
        AddLog(q, QuoteLogAction.Revised, caller, $"自 {source.VersionLabel} 衍生{(request.RefreshRates ? "（改用目前費率）" : "（沿用費率快照）")}：{request.Reason.Trim()}");
        AddLog(source, QuoteLogAction.Revised, caller, $"衍生新版本 {q.VersionLabel}：{request.Reason.Trim()}");
        if (QuoteWorkflow.SupersedesSource(source.Status))
        {
            source.Status = QuoteStatus.Superseded;
            source.UpdatedBy = caller.Name;
            source.UpdatedAtUtc = now;
            AddLog(source, QuoteLogAction.Superseded, caller, $"由 {q.VersionLabel} 取代");
        }

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return QtnError.Conflict("QTN.CONCURRENCY_CONFLICT", "版本號產生衝突，請稍後再試。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(caller, q.Id, ct).ConfigureAwait(false);
    }

    private async Task<QtnResult<QuoteVersion>> CloneAsync(
        QuoteVersion s, (int Major, int Minor, string Label) next, bool refreshRates, string actor, DateTimeOffset now, CancellationToken ct)
    {
        var q = new QuoteVersion
        {
            Id = Guid.NewGuid(), OpportunityId = s.OpportunityId, Opportunity = s.Opportunity, QuoteNo = s.QuoteNo, Major = next.Major, Minor = next.Minor,
            VersionLabel = next.Label, IsChangeRequest = s.IsChangeRequest, ParentVersionId = s.Id, Status = QuoteStatus.Draft, PricingModel = s.PricingModel,
            Title = s.Title, DiscountAmount = s.DiscountAmount, DiscountReason = s.DiscountReason, IpLicenseFee = s.IpLicenseFee, TaxRate = s.TaxRate,
            SpecialApproval = s.SpecialApproval, SpecialApprovalReason = s.SpecialApprovalReason, SprintCount = s.SprintCount, RetainerMonths = s.RetainerMonths,
            RetainerStart = s.RetainerStart, RetainerMonthlyDaysCap = s.RetainerMonthlyDaysCap, OverageDailyRate = s.OverageDailyRate,
            OffHoursMultiplier = s.OffHoursMultiplier, DurationWeeks = s.DurationWeeks, ValidDays = s.ValidDays, Notes = s.Notes,
            CreatedBy = actor, CreatedAtUtc = now
        };

        var roleIds = s.WbsItems.SelectMany(w => w.Allocations).Select(a => a.RateRoleId).Distinct().ToList();
        Dictionary<Guid, (string Name, decimal Cost, decimal List)>? current = null;
        if (refreshRates)
        {
            current = (await db.RateRoles.AsNoTracking().Where(r => roleIds.Contains(r.Id) && r.IsActive).ToListAsync(ct).ConfigureAwait(false))
                .ToDictionary(r => r.Id, r => (r.Name, r.BaseCostRate, r.BaseListRate));
            if (current.Count != roleIds.Count) return QtnError.Validation("部分角色已停用，無法改用目前費率；請沿用快照或先調整 WBS。");
        }

        var idMap = s.WbsItems.ToDictionary(w => w.Id, _ => Guid.NewGuid());
        foreach (var w in s.WbsItems)
        {
            var item = new QuoteWbsItem
            {
                Id = idMap[w.Id], QuoteVersionId = q.Id, ParentId = w.ParentId is { } p ? idMap[p] : null, SortOrder = w.SortOrder, WbsCode = w.WbsCode,
                Name = w.Name, Deliverable = w.Deliverable, Phase = w.Phase, Stage = w.Stage, KTech = w.KTech, KReq = w.KReq, TemplateItemId = w.TemplateItemId,
                CreatedBy = actor, CreatedAtUtc = now
            };
            foreach (var a in w.Allocations)
            {
                (string Name, decimal Cost, decimal List) rate = current is not null ? current[a.RateRoleId] : (a.RoleName, a.SnapshotCostRate, a.SnapshotListRate);
                item.Allocations.Add(new WbsRoleAllocation
                {
                    Id = Guid.NewGuid(), WbsItemId = item.Id, RateRoleId = a.RateRoleId, RoleName = rate.Name, Optimistic = a.Optimistic, MostLikely = a.MostLikely,
                    Pessimistic = a.Pessimistic, ExpectedDays = a.ExpectedDays, QuotedDays = a.QuotedDays, SnapshotCostRate = rate.Cost, SnapshotListRate = rate.List,
                    CreatedBy = actor, CreatedAtUtc = now
                });
            }

            q.WbsItems.Add(item);
        }

        foreach (var e in s.Expenses)
            q.Expenses.Add(new QuoteDirectExpense { Id = Guid.NewGuid(), QuoteVersionId = q.Id, Category = e.Category, Description = e.Description, Amount = e.Amount, IsCapped = e.IsCapped, CreatedBy = actor, CreatedAtUtc = now });
        foreach (var m in s.Milestones)
            q.Milestones.Add(new QuotePaymentMilestone { Id = Guid.NewGuid(), QuoteVersionId = q.Id, Seq = m.Seq, Name = m.Name, Percent = m.Percent, Trigger = m.Trigger, CreatedBy = actor, CreatedAtUtc = now });
        foreach (var c in s.Clauses)
            q.Clauses.Add(new QuoteClause { Id = Guid.NewGuid(), QuoteVersionId = q.Id, SortOrder = c.SortOrder, Code = c.Code, Title = c.Title, Body = c.Body, CreatedBy = actor, CreatedAtUtc = now });
        foreach (var l in s.SlaLevels)
            q.SlaLevels.Add(new QuoteSlaLevel { Id = Guid.NewGuid(), QuoteVersionId = q.Id, Level = l.Level, Definition = l.Definition, ResponseTime = l.ResponseTime, ResolveTarget = l.ResolveTarget, CreatedBy = actor, CreatedAtUtc = now });

        QuoteCalculator.Recalculate(q, s.Opportunity!.IpBuyout);
        return QtnResult<QuoteVersion>.Success(q);
    }

    // ======================= 對外發出與成交 =======================

    public async Task<QtnResult<QuoteDto>> IssueAsync(QtnCaller caller, Guid id, IssueQuoteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var q = loaded.Value!;

        if (!QtnAccess.Has(q.Opportunity!, caller.UserId, OpportunityRole.Sales)) return QtnError.Forbidden("僅商機業務可對外發出報價。");
        var rule = QuoteWorkflow.CanIssue(q.Status);
        if (!rule.IsOk) return QtnError.FromRule(rule);
        if (!RowVersions.Matches(q.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(q).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        var today = Workdays.TaipeiToday(clock.GetUtcNow());
        var issued = request.IssuedDate ?? today;
        if (issued > today || issued < today.AddDays(-30)) return QtnError.Validation("出具日須為今日或 30 日內的過去日期。");

        q.Status = QuoteStatus.Issued;
        q.IssuedDate = issued;
        q.ValidUntil = QuoteWorkflow.ValidUntil(issued, q.ValidDays);
        q.UpdatedBy = caller.Name;
        q.UpdatedAtUtc = clock.GetUtcNow();
        AddLog(q, QuoteLogAction.Issued, caller, $"對外發出，出具日 {issued:yyyy-MM-dd}，有效至 {q.ValidUntil:yyyy-MM-dd}");
        return await SaveAndGetAsync(caller, id, ct).ConfigureAwait(false);
    }

    /// <summary>登錄成交（客戶簽約）或未得標。主報價成交時商機轉為 Won；CR 版本成交不影響商機狀態。</summary>
    public async Task<QtnResult<QuoteDto>> OutcomeAsync(QtnCaller caller, Guid id, QuoteOutcomeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Note is { Length: > 1000 }) return QtnError.Validation("說明最多 1000 字。");

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var q = loaded.Value!;
        var o = q.Opportunity!;

        if (!QtnAccess.Has(o, caller.UserId, OpportunityRole.Sales)) return QtnError.Forbidden("僅商機業務可登錄成交結果。");
        var today = Workdays.TaipeiToday(clock.GetUtcNow());
        var rule = request.Won ? QuoteWorkflow.CanMarkWon(q.Status, q.ValidUntil, today) : QuoteWorkflow.CanMarkLost(q.Status);
        if (!rule.IsOk) return QtnError.FromRule(rule);
        if (!request.Won && string.IsNullOrWhiteSpace(request.Note)) return QtnError.Validation("未得標請填寫原因（供商機分析）。");
        if (!RowVersions.Matches(q.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(q).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        var now = clock.GetUtcNow();
        q.Status = request.Won ? QuoteStatus.Won : QuoteStatus.Lost;
        q.OutcomeNote = MasterDataService.Clean(request.Note);
        q.UpdatedBy = caller.Name;
        q.UpdatedAtUtc = now;
        AddLog(q, request.Won ? QuoteLogAction.Won : QuoteLogAction.Lost, caller, q.OutcomeNote);

        if (!q.IsChangeRequest && o.Status == OpportunityStatus.Open)
        {
            o.Status = request.Won ? OpportunityStatus.Won : OpportunityStatus.Lost;
            o.UpdatedBy = caller.Name;
            o.UpdatedAtUtc = now;
        }

        return await SaveAndGetAsync(caller, id, ct).ConfigureAwait(false);
    }

    // ======================= 轉 PMS 合約（交付基準對齊） =======================

    /// <summary>
    /// 已成交的主報價一鍵建立 PMS 合約（經 Core <see cref="IQuoteContractIntegration"/>，冪等鍵＝報價版本）：帶入客戶、金額、付款里程碑與 WBS 執行基準。
    /// </summary>
    public async Task<QtnResult<ConvertToContractResultDto>> ConvertToContractAsync(QtnCaller caller, Guid id, ConvertToContractRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var q = loaded.Value!;
        var o = q.Opportunity!;

        if (!QtnAccess.Has(o, caller.UserId, OpportunityRole.Sales)) return QtnError.Forbidden("僅商機業務可建立合約。");
        if (q.Status != QuoteStatus.Won || q.IsChangeRequest) return QtnError.Conflict("QTN.INVALID_TRANSITION", "僅已成交的主報價可建立合約。");
        if (q.PmsContractId is not null) return QtnError.Conflict("QTN.ALREADY_CONVERTED", $"此報價已建立合約 {q.PmsContractNo}。");
        if (request.WarrantyMonths is < 0 or > 120) return QtnError.Validation("保固月數須介於 0–120。");
        if (request.PmUserId is { } pm && !o.Members.Any(m => m.UserId == pm && m.Role == OpportunityRole.Pm))
            return QtnError.Validation("合約 PM 須為商機的 PM 成員。");

        var pmUser = request.PmUserId ?? o.Members.Where(m => m.Role == OpportunityRole.Pm).Select(m => (Guid?)m.UserId).FirstOrDefault();
        var leafItems = q.WbsItems.Where(w => w.Allocations.Count > 0).OrderBy(w => w.WbsCode, WbsCodeComparer.Instance)
            .Select(w => new QuoteContractWbsItem(w.WbsCode, w.Name, w.Deliverable, PhaseRatioRules.PhaseName(w.Phase), w.Allocations.Sum(a => a.QuotedDays)))
            .ToList();

        var contractRequest = new QuoteContractRequest(
            q.Id, q.QuoteNo, q.VersionLabel, q.Title, o.Customer?.Name ?? string.Empty, o.Customer?.ContactName, o.Customer?.ContactEmail,
            q.FinalPrice, q.IssuedDate, request.StartDate, request.DeliveryDate ?? o.ExpectedGoLive, request.WarrantyMonths, o.IpBuyout,
            q.Milestones.OrderBy(m => m.Seq).Select(m => new QuoteContractMilestone(m.Seq, m.Name, m.Percent, m.Amount, m.Trigger)).ToList(),
            leafItems, caller.UserId, pmUser, caller.Name,
            $"由報價 {q.QuoteNo} {q.VersionLabel} 建立（商機 {o.OpportunityNo}；計價模式 {StrategyRules.ModelName(q.PricingModel)}）");

        var created = await contracts.CreateContractAsync(contractRequest, ct).ConfigureAwait(false);
        if (!created.Succeeded)
            return new QtnError(created.StatusCode == 503 ? "QTN.PMS_UNAVAILABLE" : "QTN.CONTRACT_FAILED", created.StatusCode is >= 400 and < 600 ? created.StatusCode : 400,
                "Contract Creation Failed", created.Error ?? "建立合約失敗。");

        q.PmsContractId = created.ContractId;
        q.PmsContractNo = created.ContractNo;
        q.UpdatedBy = caller.Name;
        q.UpdatedAtUtc = clock.GetUtcNow();
        AddLog(q, QuoteLogAction.ConvertedToContract, caller, $"建立 PMS 合約 {created.ContractNo}（WBS 基準 {leafItems.Count} 項）");

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return QtnError.Concurrency();
        }

        return QtnResult<ConvertToContractResultDto>.Success(new ConvertToContractResultDto(created.ContractId!.Value, created.ContractNo!));
    }

    // ======================= 排程：報價到期 =======================

    /// <summary>已發出且逾有效期者轉為 Expired；7 日內到期者提醒業務（每日一次）。由 SCH 以系統帳號呼叫。</summary>
    public async Task<ScanResultDto> ScanExpiryAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = Workdays.TaipeiToday(now);
        var system = new QtnCaller(Guid.Empty, "系統排程", null, QuoteFieldAccess.Full, false, false);

        var expired = await db.QuoteVersions.Where(q => q.Status == QuoteStatus.Issued && q.ValidUntil != null && q.ValidUntil < today)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var q in expired)
        {
            q.Status = QuoteStatus.Expired;
            q.UpdatedBy = system.Name;
            q.UpdatedAtUtc = now;
            AddLog(q, QuoteLogAction.Expired, system, $"報價已逾有效期（{q.ValidUntil:yyyy-MM-dd}）。");
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var notified = 0;
        foreach (var q in expired) if (await NotifyMembersAsync(q.Id, "已逾有效期", "如需續行請衍生新版本並重新評估排程與費率。", ct).ConfigureAwait(false)) notified++;

        var soon = await db.QuoteVersions.AsNoTracking().Where(q => q.Status == QuoteStatus.Issued && q.ValidUntil != null && q.ValidUntil >= today && q.ValidUntil <= today.AddDays(7))
            .Select(q => new { q.Id, q.ValidUntil }).ToListAsync(ct).ConfigureAwait(false);
        foreach (var q in soon)
            if (await NotifyMembersAsync(q.Id, $"將於 {q.ValidUntil:yyyy-MM-dd} 到期", "請追蹤客戶簽約進度。", ct, $"expiring:{today:yyyyMMdd}").ConfigureAwait(false))
                notified++;

        return new ScanResultDto(expired.Count + soon.Count, notified, $"轉為逾期 {expired.Count} 筆；即將到期提醒 {soon.Count} 筆。");
    }

    // ======================= CR 版本（供 ChangeAssessmentService） =======================

    /// <summary>以 CR 評估工時衍生 CR 報價版本（vX.Y-CR，草稿），WBS 為追加工作項，付款為單期追加款。</summary>
    internal async Task<QtnResult<QuoteVersion>> CreateChangeRequestVersionAsync(
        QtnCaller caller, Guid baseQuoteId, string title, IReadOnlyList<(string Name, WbsPhase Phase, Guid RoleId, decimal Days)> lines, CancellationToken ct)
    {
        var loaded = await LoadAsync(caller, baseQuoteId, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var b = loaded.Value!;
        if (!QuoteWorkflow.CanCreateChangeRequest(b.Status)) return QtnError.Conflict("QTN.INVALID_TRANSITION", "僅已成交的報價可衍生 CR 版本。");

        var siblings = await db.QuoteVersions.AsNoTracking().Where(x => x.OpportunityId == b.OpportunityId).Select(x => new { x.Major, x.Minor, x.Status })
            .ToListAsync(ct).ConfigureAwait(false);
        if (siblings.Any(x => x.Status is QuoteStatus.Draft or QuoteStatus.PendingApproval))
            return QtnError.Conflict("QTN.INVALID_TRANSITION", "此商機已有草稿或審批中的版本，請先處理該版本。");

        var now = clock.GetUtcNow();
        var next = QuoteWorkflow.NextVersion(siblings.Select(x => (x.Major, x.Minor)), isChangeRequest: true);
        var q = new QuoteVersion
        {
            Id = Guid.NewGuid(), OpportunityId = b.OpportunityId, Opportunity = b.Opportunity, QuoteNo = b.QuoteNo, Major = next.Major, Minor = next.Minor,
            VersionLabel = next.Label, IsChangeRequest = true, ParentVersionId = b.Id, Status = QuoteStatus.Draft, PricingModel = b.PricingModel,
            Title = title, TaxRate = b.TaxRate, ValidDays = b.ValidDays, CreatedBy = caller.Name, CreatedAtUtc = now
        };

        var roleIds = lines.Select(l => l.RoleId).Distinct().ToList();
        var rates = await RatesForAsync(b, roleIds, refresh: false, ct).ConfigureAwait(false);
        if (rates.Count != roleIds.Count) return QtnError.Validation("指定的計價角色不存在或已停用。");

        var order = 0;
        foreach (var line in lines.Where(l => l.Days > 0))
        {
            var item = new QuoteWbsItem
            {
                Id = Guid.NewGuid(), QuoteVersionId = q.Id, SortOrder = ++order, Name = line.Name, Phase = line.Phase, CreatedBy = caller.Name, CreatedAtUtc = now
            };
            var rate = rates[line.RoleId];
            var allocation = new WbsRoleAllocation
            {
                Id = Guid.NewGuid(), WbsItemId = item.Id, RateRoleId = line.RoleId, RoleName = rate.Name, Optimistic = line.Days, MostLikely = line.Days,
                Pessimistic = line.Days, SnapshotCostRate = rate.Cost, SnapshotListRate = rate.List, CreatedBy = caller.Name, CreatedAtUtc = now
            };
            QuoteCalculator.ApplyPert(item, allocation);
            item.Allocations.Add(allocation);
            q.WbsItems.Add(item);
        }

        QuoteCalculator.AssignWbsCodes(q.WbsItems);
        foreach (var c in b.Clauses.Where(c => c.Code is "VALIDITY" or "CHANGE_CONTROL" or "COOPERATION" or "WARRANTY_IP"))
            q.Clauses.Add(new QuoteClause { Id = Guid.NewGuid(), QuoteVersionId = q.Id, SortOrder = c.SortOrder, Code = c.Code, Title = c.Title, Body = c.Body, CreatedBy = caller.Name, CreatedAtUtc = now });
        AddMilestones(q, [new MilestoneSeed("CR 追加款", 100m, "客戶書面簽署《專案變更追加協議》後支付")], caller.Name, now);

        QuoteCalculator.Recalculate(q, ipBuyout: false);
        db.QuoteVersions.Add(q);
        AddLog(q, QuoteLogAction.ChangeRequestCreated, caller, $"自 {b.VersionLabel} 衍生 CR 版本：{title}");
        AddLog(b, QuoteLogAction.ChangeRequestCreated, caller, $"衍生 CR 版本 {q.VersionLabel}：{title}");
        return QtnResult<QuoteVersion>.Success(q);
    }

    /// <summary>CR 客戶簽署：CR 報價須已核准或已發出，轉為 Won。</summary>
    internal async Task<QtnError?> MarkChangeRequestSignedAsync(QtnCaller caller, Guid crQuoteId, string note, CancellationToken ct)
    {
        var q = await db.QuoteVersions.FirstOrDefaultAsync(x => x.Id == crQuoteId, ct).ConfigureAwait(false);
        if (q is null) return QtnError.NotFound("CR 報價不存在。");
        if (q.Status is not (QuoteStatus.Approved or QuoteStatus.Issued))
            return QtnError.Conflict("QTN.INVALID_TRANSITION", "CR 報價須經內部審批核准後，才能登錄客戶簽署。");

        q.Status = QuoteStatus.Won;
        q.OutcomeNote = note;
        q.UpdatedBy = caller.Name;
        q.UpdatedAtUtc = clock.GetUtcNow();
        AddLog(q, QuoteLogAction.Won, caller, note);
        return null;
    }

    // ======================= 通知 =======================

    private async Task NotifyStepAsync(Guid quoteId, CancellationToken ct)
    {
        if (!notifier.IsAvailable) return;
        var q = await db.QuoteVersions.AsNoTracking().Include(x => x.Opportunity!).ThenInclude(o => o.Members).Include(x => x.Opportunity!).ThenInclude(o => o.Customer)
            .Include(x => x.ApprovalSteps).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == quoteId, ct).ConfigureAwait(false);
        var step = q is null ? null : CurrentStep(q);
        if (q is null || step is null) return;

        var users = step.Role == ApprovalRole.TechLead
            ? q.Opportunity!.Members.Where(m => m.Role == OpportunityRole.TechLead).Select(m => m.UserId).ToList()
            : await db.ApproverAssignments.AsNoTracking().Where(a => a.Role == step.Role).Select(a => a.UserId).ToListAsync(ct).ConfigureAwait(false);
        users.Remove(q.SubmittedBy ?? Guid.Empty);

        await notifier.NotifyAsync(users, $"[報價審批] {q.QuoteNo} {q.VersionLabel} 待您審批（{MarginLightPolicy.RoleName(step.Role)}）",
            [
                $"報價：{q.QuoteNo} {q.VersionLabel}　{q.Title}",
                $"客戶：{q.Opportunity!.Customer?.Name}",
                $"燈號：{MarginLightPolicy.LightName(q.MarginLight)}；關卡：第 {step.Seq} 關 {MarginLightPolicy.RoleName(step.Role)}",
                "請至「報價管理 → 待我審批」處理。"
            ],
            "QTN.ApprovalPending", $"qtn:approval:{q.Id}:{q.ApprovalCycle}:{step.Seq}", ct).ConfigureAwait(false);
    }

    private async Task<bool> NotifyMembersAsync(Guid quoteId, string what, string? detail, CancellationToken ct, string? fingerprintSuffix = null)
    {
        if (!notifier.IsAvailable) return false;
        var q = await db.QuoteVersions.AsNoTracking().Include(x => x.Opportunity!).ThenInclude(o => o.Members).FirstOrDefaultAsync(x => x.Id == quoteId, ct)
            .ConfigureAwait(false);
        if (q is null) return false;

        var users = q.Opportunity!.Members.Where(m => m.Role is OpportunityRole.Sales or OpportunityRole.Pm or OpportunityRole.TechLead).Select(m => m.UserId);
        return await notifier.NotifyAsync(users, $"[報價] {q.QuoteNo} {q.VersionLabel} {what}",
            [$"報價：{q.QuoteNo} {q.VersionLabel}　{q.Title}", $"狀態：{what}", detail ?? string.Empty],
            "QTN.QuoteStatus", $"qtn:status:{q.Id}:{q.Status}:{fingerprintSuffix}", ct).ConfigureAwait(false);
    }
}
