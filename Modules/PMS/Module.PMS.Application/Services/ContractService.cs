using Microsoft.EntityFrameworkCore;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 專案合約管理（乙方標準版）第一期：合約主檔與條款、審約檢核表（自動比對乙方底線）、PM→法務→高階主管核決（職責分離、送審快照鎖定）、
/// 簽約登錄、附件一／二、與 PMS 專案綁定。僅合約成員可見，非成員一律 404；客戶 Stakeholder 不在合約成員之列，看不到任何合約。
/// </summary>
public sealed class ContractService(PMSDbContext db, TimeProvider clock)
{
    private const int MaxPageSize = 200;
    private const int MaxEvents = 200;
    private const int NumberRetries = 3;

    // 「已驗收」不開放手動切換：由履約管理的驗收（雙方簽認或擬制驗收）自動進入，才會同時放行尾款與保固起算
    private static readonly ContractStatus[] ManualStatusTargets =
        [ContractStatus.Quoting, ContractStatus.Reviewing, ContractStatus.Negotiating, ContractStatus.Warranty,
         ContractStatus.Closed, ContractStatus.Lost, ContractStatus.Cancelled, ContractStatus.Terminated];

    // ---------- 查詢 ----------

    public async Task<PagedResult<ContractListDto>> SearchAsync(Guid userId, ContractSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var filter = request.Filter ?? new ContractSearchFilter();

        var query = db.Contracts.AsNoTracking().Where(c => c.Members.Any(m => m.UserId == userId));
        if (filter.Status is { Count: > 0 } statuses) query = query.Where(c => statuses.Contains(c.Status));
        if (filter.Type is { Count: > 0 } types) query = query.Where(c => types.Contains(c.Type));
        if (filter.Industry is { Count: > 0 } industries) query = query.Where(c => industries.Contains(c.Industry));
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(c => c.ContractNo.Contains(term) || c.Title.Contains(term) || c.CustomerName.Contains(term));
        }

        if (filter.PendingMyDecision) query = query.Where(c => c.Status == ContractStatus.PendingApproval);

        // 待決優先（待核決排最前）、其後依更新時間；以主鍵作次要排序確保分頁穩定
        var ordered = query
            .OrderBy(c => c.Status == ContractStatus.PendingApproval ? 0 : 1)
            .ThenByDescending(c => c.UpdatedAtUtc ?? c.CreatedAtUtc)
            .ThenBy(c => c.Id);

        if (filter.PendingMyDecision)
        {
            // 「待我核決」需逐筆計算步驟與職責分離，筆數有限（僅待核決者），在記憶體中處理
            var all = await ordered.Include(c => c.Members).Include(c => c.ReviewItems).Include(c => c.Approvals).Include(c => c.Projects)
                .AsSplitQuery().ToListAsync(ct).ConfigureAwait(false);
            var mine = all.Select(c => ToListDto(c, userId)).Where(d => d.PendingMyDecision).ToList();
            return new PagedResult<ContractListDto>(
                mine.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList(), mine.Count, pageIndex, pageSize);
        }

        var total = await ordered.CountAsync(ct).ConfigureAwait(false);
        var rows = await ordered.Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Include(c => c.Members).Include(c => c.ReviewItems).Include(c => c.Approvals).Include(c => c.Projects)
            .AsSplitQuery().ToListAsync(ct).ConfigureAwait(false);

        return new PagedResult<ContractListDto>(rows.Select(c => ToListDto(c, userId)).ToList(), total, pageIndex, pageSize);
    }

    public async Task<PmsResult<ContractDto>> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var loaded = await LoadAsync(userId, id, track: false, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        return PmsResult<ContractDto>.Success(await ToDtoAsync(loaded.Value!, userId, ct).ConfigureAwait(false));
    }

    // ---------- 建立與更新 ----------

    public async Task<PmsResult<ContractDto>> CreateAsync(Guid userId, string actor, CreateContractRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Terms);

        var probe = new Contract();
        ApplyTerms(probe, request.Terms);
        var invalid = ContractRules.ValidateTerms(probe);
        if (invalid is not null) return PmsError.Validation(invalid);

        var now = clock.GetUtcNow();
        var year = TaipeiToday().Year;

        for (var attempt = 1; attempt <= NumberRetries; attempt++)
        {
            var prefix = $"CT-{year}-";
            var existing = await db.Contracts.AsNoTracking().Where(c => c.ContractNo.StartsWith(prefix)).Select(c => c.ContractNo)
                .ToListAsync(ct).ConfigureAwait(false);

            var contract = new Contract { Id = Guid.NewGuid(), ContractNo = ContractRules.NextContractNo(year, existing), CreatedBy = actor, CreatedAtUtc = now };
            ApplyTerms(contract, request.Terms);
            contract.Status = ContractStatus.Quoting;

            // 建立者即業務（合約 PM、法務、核決人於「成員」另行指派）
            contract.Members.Add(new ContractMember { Id = Guid.NewGuid(), UserId = userId, Role = ContractMemberRole.Sales, CreatedBy = actor, CreatedAtUtc = now });

            foreach (var spec in ContractRules.ReviewCatalog)
                contract.ReviewItems.Add(new ContractReviewItem { Id = Guid.NewGuid(), Code = spec.Code, CreatedBy = actor, CreatedAtUtc = now });

            var order = 0;
            foreach (var seed in ContractRules.DefaultDeliverables)
                contract.DeliverableItems.Add(new ContractDeliverableItem
                {
                    Id = Guid.NewGuid(), SortOrder = ++order, Stage = seed.Stage, Name = seed.Name, Format = seed.Format,
                    AcceptMethod = seed.AcceptMethod, RequiresCustomerSignoff = seed.RequiresCustomerSignoff, CreatedBy = actor, CreatedAtUtc = now
                });
            foreach (var seed in ContractRules.DefaultSlaLevels)
                contract.SlaLevels.Add(new ContractSlaLevel
                {
                    Id = Guid.NewGuid(), Level = seed.Level, Name = seed.Name, Definition = seed.Definition,
                    ResponseHours = seed.ResponseHours, Target = seed.Target, CreatedBy = actor, CreatedAtUtc = now
                });

            db.Contracts.Add(contract);
            db.ContractEvents.Add(NewEvent(contract.Id, ContractEventType.Created, $"建立合約 {contract.ContractNo}", userId, actor, now));

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return await GetAsync(userId, contract.Id, ct).ConfigureAwait(false);
            }
            catch (DbUpdateException) when (attempt < NumberRetries)
            {
                // 合約編號唯一鍵衝突（同時建立）：重算流水號再試
                db.ChangeTracker.Clear();
            }
        }

        return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "合約編號產生衝突，請稍後再試。");
    }

    public async Task<PmsResult<ContractDto>> UpdateAsync(Guid userId, string actor, Guid id, UpdateContractRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Terms);

        var loaded = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var c = loaded.Value!;

        var blocked = RequireManage(userId, c);
        if (blocked is not null) return blocked;
        if (!ContractRules.CanEditTerms(c.Status))
            return PmsError.Conflict("PMS.CONTENT_LOCKED", "合約已送審或已簽約，條款鎖定；簽約後的異動請走合約變更。");
        if (!TryParseRowVersion(request.RowVersion, out var bytes)) return PmsError.Validation("rowVersion 格式不正確。");
        db.Entry(c).Property(x => x.RowVersion).OriginalValue = bytes;

        ApplyTerms(c, request.Terms);
        var invalid = ContractRules.ValidateTerms(c);
        if (invalid is not null)
        {
            db.ChangeTracker.Clear();
            return PmsError.Validation(invalid);
        }

        var now = clock.GetUtcNow();
        c.UpdatedBy = actor;
        c.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.Updated, "更新合約條款", userId, actor, now));

        return await SaveAsync(userId, id, ct).ConfigureAwait(false);
    }

    // ---------- 成員 ----------

    public async Task<PmsResult<ContractDto>> SetMembersAsync(Guid userId, string actor, Guid id, SetContractMembersRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var c = loaded.Value!;

        var blocked = RequireManage(userId, c);
        if (blocked is not null) return blocked;
        if (c.Status == ContractStatus.PendingApproval) return PmsError.Conflict("PMS.CONTENT_LOCKED", "核決進行中不可異動成員，請先退回。");
        if (c.Status is ContractStatus.Closed or ContractStatus.Lost or ContractStatus.Cancelled or ContractStatus.Terminated)
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "合約已結束，不可異動成員。");

        var wanted = (request.Members ?? []).Where(m => m.UserId != Guid.Empty).Select(m => (m.UserId, m.Role)).Distinct().ToList();
        if (wanted.Any(m => !Enum.IsDefined(m.Role))) return PmsError.Validation("成員角色不正確。");
        if (wanted.GroupBy(m => m.Role).Any(g => g.Count() > ContractRules.MaxMembersPerRole))
            return PmsError.Validation($"每個角色最多 {ContractRules.MaxMembersPerRole} 人。");
        if (!wanted.Any(m => m.Role is ContractMemberRole.Sales or ContractMemberRole.Pm))
            return PmsError.Validation("至少要有一位業務或合約 PM，否則無人可維護合約。");

        var now = clock.GetUtcNow();
        foreach (var existing in c.Members.Where(m => !wanted.Contains((m.UserId, m.Role))).ToList())
        {
            c.Members.Remove(existing);
            db.ContractMembers.Remove(existing);
        }

        foreach (var (memberId, role) in wanted.Where(w => !c.Members.Any(m => m.UserId == w.UserId && m.Role == w.Role)))
            db.ContractMembers.Add(new ContractMember { Id = Guid.NewGuid(), ContractId = c.Id, UserId = memberId, Role = role, CreatedBy = actor, CreatedAtUtc = now });

        c.UpdatedBy = actor;
        c.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.MembersChanged, $"更新合約成員（共 {wanted.Count} 人次）", userId, actor, now));

        return await SaveAsync(userId, id, ct).ConfigureAwait(false);
    }

    // ---------- 審約檢核表 ----------

    public async Task<PmsResult<ContractDto>> UpdateReviewItemAsync(Guid userId, string actor, Guid id, string code, UpdateReviewItemRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var c = loaded.Value!;

        if (!ContractRules.CanReview(userId, Members(c))) return PmsError.Forbidden("僅合約 PM 或法務可填寫審閱結果。");
        if (!ContractRules.CanEditTerms(c.Status))
            return PmsError.Conflict("PMS.CONTENT_LOCKED", "合約已送審或已簽約，審閱結果鎖定。");
        if (!ContractRules.IsKnownReviewCode(code)) return PmsError.NotFound("審閱項目不存在。");
        if (!Enum.IsDefined(request.Stance) || !Enum.IsDefined(request.Result)) return PmsError.Validation("立場或結果不正確。");
        if (request.ClientClause is { Length: > 1500 } || request.Note is { Length: > 1500 }) return PmsError.Validation("條款摘錄與說明各不得超過 1500 字。");

        var item = c.ReviewItems.FirstOrDefault(i => i.Code == code);
        if (item is null) return PmsError.NotFound("審閱項目不存在。");

        var now = clock.GetUtcNow();
        item.ClientClause = string.IsNullOrWhiteSpace(request.ClientClause) ? null : request.ClientClause.Trim();
        item.Stance = request.Stance;
        item.Result = request.Result;
        item.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        item.ReviewedBy = userId;
        item.ReviewedAt = now;
        item.UpdatedBy = actor;
        item.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.ReviewItemUpdated,
            $"審閱「{ContractRules.ReviewCatalog.First(r => r.Code == code).Name}」：{ResultName(request.Result)}", userId, actor, now));

        return await SaveAsync(userId, id, ct).ConfigureAwait(false);
    }

    // ---------- 狀態、送審、核決、簽約 ----------

    public async Task<PmsResult<ContractDto>> ChangeStatusAsync(Guid userId, string actor, Guid id, ChangeContractStatusRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var c = loaded.Value!;

        var blocked = RequireManage(userId, c);
        if (blocked is not null) return blocked;
        if (!ManualStatusTargets.Contains(request.Status) || !ContractRules.CanTransition(c.Status, request.Status))
            return PmsError.Conflict("PMS.INVALID_TRANSITION", $"合約目前為「{StatusName(c.Status)}」，不能直接改為「{StatusName(request.Status)}」。");
        if (request.Status is ContractStatus.Lost or ContractStatus.Cancelled or ContractStatus.Terminated && string.IsNullOrWhiteSpace(request.Reason))
            return PmsError.Validation("流標、取消或終止合約須填寫原因。");
        if (request.Reason is { Length: > 500 }) return PmsError.Validation("原因不得超過 500 字。");

        var now = clock.GetUtcNow();
        var from = c.Status;
        c.Status = request.Status;
        c.UpdatedBy = actor;
        c.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.StatusChanged,
            $"狀態 {StatusName(from)} → {StatusName(request.Status)}{(string.IsNullOrWhiteSpace(request.Reason) ? string.Empty : $"：{request.Reason.Trim()}")}", userId, actor, now));

        return await SaveAsync(userId, id, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<ContractDto>> SubmitAsync(Guid userId, string actor, Guid id, CancellationToken ct)
    {
        var loaded = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var c = loaded.Value!;

        var blocked = RequireManage(userId, c);
        if (blocked is not null) return blocked;
        if (c.Status is not (ContractStatus.Reviewing or ContractStatus.Negotiating))
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "僅「審約」或「談判」階段的合約可送審；請先將狀態改為審約。");

        var problems = ContractRules.ValidateSubmit(c, c.ReviewItems.ToList(), Members(c));
        if (problems.Count > 0) return PmsError.Validation("尚不能送審：" + string.Join("；", problems));

        var now = clock.GetUtcNow();
        c.Status = ContractStatus.PendingApproval;
        c.ApprovalCycle++;
        c.SnapshotHash = ContractRules.ComputeSnapshotHash(c, c.ReviewItems);
        c.SubmittedBy = userId;
        c.SubmittedAt = now;
        c.UpdatedBy = actor;
        c.UpdatedAtUtc = now;

        var steps = ContractRules.RequiredSteps(c.ReviewItems, ContractRules.AutoFlags(c));
        db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.Submitted,
            $"送審（第 {c.ApprovalCycle} 次）；核決流程：{string.Join(" → ", steps.Select(ContractRules.StepName))}", userId, actor, now));

        return await SaveAsync(userId, id, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 撤回送審：業務或合約 PM 在簽約前可撤回，合約回到談判中並清除快照；既有核決紀錄保留供稽核，重新送審為新週期，須重跑全部核決步驟。
    /// </summary>
    public async Task<PmsResult<ContractDto>> WithdrawAsync(Guid userId, string actor, Guid id, ContractDecisionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var c = loaded.Value!;

        var blocked = RequireManage(userId, c);
        if (blocked is not null) return blocked;
        if (c.Status != ContractStatus.PendingApproval) return PmsError.Conflict("PMS.INVALID_TRANSITION", "僅待核決的合約可撤回送審。");
        if (string.IsNullOrWhiteSpace(request.Comment)) return PmsError.Validation("撤回送審請填寫原因。");
        if (request.Comment.Length > 1000) return PmsError.Validation("原因不得超過 1000 字。");

        var now = clock.GetUtcNow();
        c.Status = ContractStatus.Negotiating;
        c.SnapshotHash = null;
        c.UpdatedBy = actor;
        c.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.Withdrawn, $"撤回送審（第 {c.ApprovalCycle} 次）：{request.Comment.Trim()}", userId, actor, now));

        return await SaveAsync(userId, id, ct).ConfigureAwait(false);
    }

    /// <summary>PM 初審或法務審核（高階主管走 <see cref="ExecutiveDecisionAsync"/>）。</summary>
    public Task<PmsResult<ContractDto>> ReviewDecisionAsync(Guid userId, string actor, Guid id, ContractReviewDecisionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Step == ContractApprovalStep.Executive)
            return Task.FromResult<PmsResult<ContractDto>>(PmsError.Validation("高階主管核決請使用核決端點。"));
        if (request.Decision == ApprovalDecision.Rejected)
            return Task.FromResult<PmsResult<ContractDto>>(PmsError.Validation("PM 與法務僅能核准或退回；否決（不承接）由高階主管決定。"));

        return DecideAsync(userId, actor, id, request.Step, request.Decision, request.Comment, ct);
    }

    public Task<PmsResult<ContractDto>> ExecutiveDecisionAsync(Guid userId, string actor, Guid id, ContractDecisionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DecideAsync(userId, actor, id, ContractApprovalStep.Executive, request.Decision, request.Comment, ct);
    }

    private async Task<PmsResult<ContractDto>> DecideAsync(
        Guid userId, string actor, Guid id, ContractApprovalStep step, ApprovalDecision decision, string? comment, CancellationToken ct)
    {
        if (!Enum.IsDefined(decision) || !Enum.IsDefined(step)) return PmsError.Validation("核決內容不正確。");

        var loaded = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var c = loaded.Value!;

        if (c.Status != ContractStatus.PendingApproval) return PmsError.Conflict("PMS.INVALID_TRANSITION", "合約目前不在核決中。");

        var required = ContractRules.RequiredSteps(c.ReviewItems, ContractRules.AutoFlags(c));
        var cycle = c.Approvals.Where(a => a.Cycle == c.ApprovalCycle).ToList();
        var next = ContractRules.NextStep(required, cycle);
        var invalid = ContractRules.ValidateDecision(step, decision, comment, userId, c.SubmittedBy, next, Members(c), cycle);
        if (invalid is not null) return PmsError.Conflict("PMS.INVALID_TRANSITION", invalid);

        var now = clock.GetUtcNow();
        db.ContractApprovals.Add(new ContractApproval
        {
            Id = Guid.NewGuid(), ContractId = c.Id, Cycle = c.ApprovalCycle, Step = step, Decision = decision,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(), DecidedBy = userId, DecidedByName = actor, DecidedAt = now
        });

        var stepName = ContractRules.StepName(step);
        switch (decision)
        {
            case ApprovalDecision.Approved:
                db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.Approved, $"{stepName}：核准", userId, actor, now));
                break;
            case ApprovalDecision.Returned:
                c.Status = ContractStatus.Negotiating;
                c.SnapshotHash = null;
                db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.Returned, $"{stepName}：退回修改（{comment!.Trim()}）", userId, actor, now));
                break;
            default:
                c.Status = ContractStatus.Cancelled;
                c.SnapshotHash = null;
                db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.Rejected, $"{stepName}：否決，合約取消（{comment!.Trim()}）", userId, actor, now));
                break;
        }

        c.UpdatedBy = actor;
        c.UpdatedAtUtc = now;
        return await SaveAsync(userId, id, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<ContractDto>> SignAsync(Guid userId, string actor, Guid id, SignContractRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var c = loaded.Value!;

        var blocked = RequireManage(userId, c);
        if (blocked is not null) return blocked;
        if (!TryParseRowVersion(request.RowVersion, out var bytes)) return PmsError.Validation("rowVersion 格式不正確。");
        db.Entry(c).Property(x => x.RowVersion).OriginalValue = bytes;

        var required = ContractRules.RequiredSteps(c.ReviewItems, ContractRules.AutoFlags(c));
        var next = ContractRules.NextStep(required, c.Approvals.Where(a => a.Cycle == c.ApprovalCycle));
        var invalid = ContractRules.ValidateSign(c, next, ContractRules.ComputeSnapshotHash(c, c.ReviewItems));
        if (invalid is not null) return PmsError.Conflict("PMS.INVALID_TRANSITION", invalid);
        if (request.SignedDate > TaipeiToday()) return PmsError.Validation("簽約日不得晚於今天。");
        if (request.StartDate is { } start && c.DeliveryDate is { } delivery && delivery < start) return PmsError.Validation("合約起日不得晚於原定交付日。");

        var now = clock.GetUtcNow();
        c.Status = ContractStatus.Active;
        c.SignedDate = request.SignedDate;
        c.StartDate = request.StartDate ?? c.StartDate ?? request.SignedDate;
        c.SignedBy = userId;
        c.UpdatedBy = actor;
        c.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.Signed, $"簽約完成（簽約日 {request.SignedDate:yyyy-MM-dd}），合約進入履約中", userId, actor, now));

        return await SaveAsync(userId, id, ct).ConfigureAwait(false);
    }

    // ---------- 附件一／二 ----------

    public async Task<PmsResult<ContractDto>> UpdateDeliverableListAsync(Guid userId, string actor, Guid id, UpdateDeliverableListRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var c = loaded.Value!;

        var blocked = RequireManage(userId, c);
        if (blocked is not null) return blocked;
        if (!ContractRules.CanEditTerms(c.Status)) return PmsError.Conflict("PMS.CONTENT_LOCKED", "合約已送審或已簽約，附件一鎖定。");

        var seeds = (request.Items ?? []).Select(i => new DeliverableSeed(i.Stage ?? string.Empty, i.Name ?? string.Empty, i.Format ?? string.Empty, i.AcceptMethod ?? string.Empty, i.RequiresCustomerSignoff)).ToList();
        var invalid = ContractRules.ValidateDeliverableItems(seeds);
        if (invalid is not null) return PmsError.Validation(invalid);

        var now = clock.GetUtcNow();
        db.ContractDeliverableItems.RemoveRange(c.DeliverableItems);
        c.DeliverableItems.Clear();
        var order = 0;
        foreach (var s in seeds)
            db.ContractDeliverableItems.Add(new ContractDeliverableItem
            {
                Id = Guid.NewGuid(), ContractId = c.Id, SortOrder = ++order, Stage = s.Stage.Trim(), Name = s.Name.Trim(), Format = s.Format.Trim(),
                AcceptMethod = s.AcceptMethod.Trim(), RequiresCustomerSignoff = s.RequiresCustomerSignoff, CreatedBy = actor, CreatedAtUtc = now
            });

        c.UpdatedBy = actor;
        c.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.AppendixUpdated, $"更新附件一交付項目清單（{seeds.Count} 項）", userId, actor, now));
        return await SaveAsync(userId, id, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<ContractDto>> UpdateSlaLevelsAsync(Guid userId, string actor, Guid id, UpdateSlaLevelsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var c = loaded.Value!;

        var blocked = RequireManage(userId, c);
        if (blocked is not null) return blocked;
        if (!ContractRules.CanEditTerms(c.Status)) return PmsError.Conflict("PMS.CONTENT_LOCKED", "合約已送審或已簽約，附件二鎖定。");

        var seeds = (request.Levels ?? []).Select(l => new SlaLevelSeed(l.Level, l.Name ?? string.Empty, l.Definition ?? string.Empty, l.ResponseHours, l.Target ?? string.Empty)).ToList();
        var invalid = ContractRules.ValidateSlaLevels(seeds);
        if (invalid is not null) return PmsError.Validation(invalid);

        var now = clock.GetUtcNow();
        db.ContractSlaLevels.RemoveRange(c.SlaLevels);
        c.SlaLevels.Clear();
        foreach (var s in seeds.OrderBy(s => s.Level))
            db.ContractSlaLevels.Add(new ContractSlaLevel
            {
                Id = Guid.NewGuid(), ContractId = c.Id, Level = s.Level, Name = s.Name.Trim(), Definition = s.Definition.Trim(),
                ResponseHours = s.ResponseHours, Target = s.Target.Trim(), CreatedBy = actor, CreatedAtUtc = now
            });

        c.UpdatedBy = actor;
        c.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.AppendixUpdated, $"更新附件二 SLA 分級（{seeds.Count} 級）", userId, actor, now));
        return await SaveAsync(userId, id, ct).ConfigureAwait(false);
    }

    // ---------- 綁定專案 ----------

    public async Task<PmsResult<ContractDto>> BindProjectAsync(Guid userId, string actor, Guid id, BindContractProjectRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var c = loaded.Value!;

        var blocked = RequireManage(userId, c);
        if (blocked is not null) return blocked;
        if (!ContractRules.CanBindProjects(c.Status)) return PmsError.Conflict("PMS.INVALID_TRANSITION", "合約簽約後才能綁定專案。");
        if (c.Projects.Count >= ContractRules.MaxProjectsPerContract) return PmsError.Validation($"每份合約最多綁定 {ContractRules.MaxProjectsPerContract} 個專案。");

        var project = await db.Projects.AsNoTracking().Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct).ConfigureAwait(false);
        if (project is null || ProjectAccessPolicy.Evaluate(userId, project.Members.Select(m => (m.UserId, m.RaciRole)).ToList()) != ProjectAccessLevel.Internal)
            return PmsError.NotFound("專案不存在。");

        var boundTo = await db.ContractProjects.AsNoTracking().Where(p => p.ProjectId == project.Id).Select(p => p.Contract!.ContractNo).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (boundTo is not null) return PmsError.Conflict("PMS.INVALID_TRANSITION", $"專案已綁定合約 {boundTo}。");

        var now = clock.GetUtcNow();
        db.ContractProjects.Add(new ContractProject { Id = Guid.NewGuid(), ContractId = c.Id, ProjectId = project.Id, CreatedBy = actor, CreatedAtUtc = now });
        c.UpdatedBy = actor;
        c.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.ProjectBound, $"綁定專案 {project.Code} {project.Name}", userId, actor, now));

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "專案已被綁定到其他合約。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, id, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<ContractDto>> UnbindProjectAsync(Guid userId, string actor, Guid id, Guid projectId, CancellationToken ct)
    {
        var loaded = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var c = loaded.Value!;

        var blocked = RequireManage(userId, c);
        if (blocked is not null) return blocked;

        var link = c.Projects.FirstOrDefault(p => p.ProjectId == projectId);
        if (link is null) return PmsError.NotFound("此專案未綁定在這份合約。");

        var now = clock.GetUtcNow();
        var label = link.Project is { } p ? $"{p.Code} {p.Name}" : projectId.ToString();
        db.ContractProjects.Remove(link);
        c.UpdatedBy = actor;
        c.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.ProjectUnbound, $"解除綁定專案 {label}", userId, actor, now));

        return await SaveAsync(userId, id, ct).ConfigureAwait(false);
    }

    /// <summary>某專案所屬合約的編號與識別碼（供專案詳情顯示；僅回傳編號，不洩漏金額與條款）。</summary>
    public async Task<(Guid Id, string No)?> FindContractOfProjectAsync(Guid projectId, CancellationToken ct)
    {
        var row = await db.ContractProjects.AsNoTracking().Where(p => p.ProjectId == projectId)
            .Select(p => new { p.ContractId, p.Contract!.ContractNo }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return row is null ? null : (row.ContractId, row.ContractNo);
    }

    // ---------- 內部輔助 ----------

    private static IReadOnlyList<(Guid UserId, ContractMemberRole Role)> Members(Contract c) =>
        c.Members.Select(m => (m.UserId, m.Role)).ToList();

    private static PmsError? RequireManage(Guid userId, Contract c) =>
        ContractRules.CanManage(userId, Members(c)) ? null : PmsError.Forbidden("僅合約的業務或合約 PM 可執行此操作。");

    private async Task<PmsResult<Contract>> LoadAsync(Guid userId, Guid id, bool track, CancellationToken ct)
    {
        var query = db.Contracts.AsQueryable();
        if (!track) query = query.AsNoTracking();

        var contract = await query
            .Include(c => c.Members).Include(c => c.ReviewItems).Include(c => c.Approvals)
            .Include(c => c.DeliverableItems).Include(c => c.SlaLevels).Include(c => c.Projects).ThenInclude(p => p.Project)
            .AsSplitQuery().FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);

        // 非成員一律 404（防 IDOR），不洩漏合約是否存在
        if (contract is null || !ContractRules.IsMember(userId, Members(contract))) return PmsError.NotFound("合約不存在。");
        return PmsResult<Contract>.Success(contract);
    }

    private async Task<PmsResult<ContractDto>> SaveAsync(Guid userId, Guid id, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "合約已被他人更新，請重新載入後再試。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, id, ct).ConfigureAwait(false);
    }

    private static void ApplyTerms(Contract c, ContractTermsDto t)
    {
        c.Title = (t.Title ?? string.Empty).Trim();
        c.CustomerName = (t.CustomerName ?? string.Empty).Trim();
        c.CustomerContactName = string.IsNullOrWhiteSpace(t.CustomerContactName) ? null : t.CustomerContactName.Trim();
        c.CustomerContactEmail = string.IsNullOrWhiteSpace(t.CustomerContactEmail) ? null : t.CustomerContactEmail.Trim();
        c.Type = t.Type;
        c.Industry = t.Industry;
        c.TotalAmount = t.TotalAmount;
        c.QuoteDate = t.QuoteDate;
        c.StartDate = t.StartDate;
        c.DeliveryDate = t.DeliveryDate;
        c.OpenEndedScope = t.OpenEndedScope;
        c.ReviewDeadlineWorkdays = t.ReviewDeadlineWorkdays;
        c.MaxReviewRounds = t.MaxReviewRounds;
        c.DeemedPassOnTimeout = t.DeemedPassOnTimeout;
        c.IpRule = t.IpRule;
        c.ScanRule = t.ScanRule;
        c.DeemedAcceptanceWorkdays = t.DeemedAcceptanceWorkdays;
        c.FinalPaymentHeldForMinorDefects = t.FinalPaymentHeldForMinorDefects;
        c.LiabilityCap = t.LiabilityCap;
        c.LiabilityCapAmount = t.LiabilityCap == LiabilityCapType.FixedAmount ? t.LiabilityCapAmount : null;
        c.ExcludesIndirectLoss = t.ExcludesIndirectLoss;
        c.WarrantyMonths = t.WarrantyMonths;
        c.IncidentNotifyHours = t.IncidentNotifyHours;
        c.AuditNoticeDays = t.AuditNoticeDays;
        c.AuditPerYear = t.AuditPerYear;
        c.AuditCostOnVendor = t.AuditCostOnVendor;
        c.CoopNoticeWorkdays = t.CoopNoticeWorkdays;
        c.ServiceHours = (t.ServiceHours ?? string.Empty).Trim();
        c.SupportChannel = string.IsNullOrWhiteSpace(t.SupportChannel) ? null : t.SupportChannel.Trim();
        c.Notes = string.IsNullOrWhiteSpace(t.Notes) ? null : t.Notes.Trim();
    }

    private static ContractEvent NewEvent(Guid contractId, ContractEventType type, string message, Guid userId, string actor, DateTimeOffset at) =>
        new() { Id = Guid.NewGuid(), ContractId = contractId, Type = type, Message = message, ActorUserId = userId, ActorName = actor, At = at };

    private DateOnly TaipeiToday() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(SlaCalculator.Offset).DateTime);

    private static bool TryParseRowVersion(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { bytes = Convert.FromBase64String(value); return bytes.Length > 0; }
        catch (FormatException) { return false; }
    }

    /// <summary>目前待決步驟與我可否決定（含職責分離）；擁有該步驟角色卻被擋下時回傳原因，讓畫面說明為何沒有核決按鈕。</summary>
    private static (ContractApprovalStep? Next, bool CanDecide, IReadOnlyList<ContractApprovalStep> Required, string? BlockedReason) Decision(Contract c, Guid userId)
    {
        var required = ContractRules.RequiredSteps(c.ReviewItems, ContractRules.AutoFlags(c));
        if (c.Status != ContractStatus.PendingApproval) return (null, false, required, null);

        var cycle = c.Approvals.Where(a => a.Cycle == c.ApprovalCycle).ToList();
        var next = ContractRules.NextStep(required, cycle);
        if (next is not { } step) return (next, false, required, null);

        var problem = ContractRules.ValidateDecision(step, ApprovalDecision.Approved, null, userId, c.SubmittedBy, next, Members(c), cycle);
        // 只有「擁有該步驟角色」的人才需要看到被擋下的原因；非該角色成員不顯示
        var reason = problem is not null && ContractRules.HasRole(userId, ContractRules.RoleOfStep(step), Members(c)) ? problem : null;
        return (next, problem is null, required, reason);
    }

    private static ContractListDto ToListDto(Contract c, Guid userId)
    {
        var (next, can, _, _) = Decision(c, userId);
        var flags = ContractRules.AutoFlags(c);
        return new ContractListDto(
            c.Id, c.ContractNo, c.Title, c.CustomerName, c.Type, c.Industry, c.Status, c.TotalAmount, c.DeliveryDate, c.SignedDate, c.Projects.Count,
            c.Members.Where(m => m.UserId == userId).Select(m => m.Role).Distinct().ToList(), ContractRules.WorstSeverity(flags), next, can,
            c.CreatedAtUtc, c.UpdatedAtUtc);
    }

    private async Task<ContractDto> ToDtoAsync(Contract c, Guid userId, CancellationToken ct)
    {
        var events = await db.ContractEvents.AsNoTracking().Where(e => e.ContractId == c.Id).OrderByDescending(e => e.At).Take(MaxEvents)
            .Select(e => new ContractEventDto(e.Type, e.Message, e.ActorName, e.At)).ToListAsync(ct).ConfigureAwait(false);
        var attachments = await db.ContractAttachments.AsNoTracking().Where(a => a.ContractId == c.Id && !a.IsDeleted).OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct).ConfigureAwait(false);

        var members = Members(c);
        var flags = ContractRules.AutoFlags(c);
        var (next, canDecide, required, blockedReason) = Decision(c, userId);
        var manage = ContractRules.CanManage(userId, members);
        var editable = ContractRules.CanEditTerms(c.Status);
        var allowed = ManualStatusTargets.Where(t => ContractRules.CanTransition(c.Status, t)).ToList();

        var actions = new ContractActionsDto(
            CanEditTerms: manage && editable,
            CanManage: manage,
            CanReviewItems: ContractRules.CanReview(userId, members) && editable,
            CanSubmit: manage && c.Status is ContractStatus.Reviewing or ContractStatus.Negotiating,
            CanWithdraw: manage && c.Status == ContractStatus.PendingApproval,
            CanDecide: canDecide, DecideStep: canDecide ? next : null, DecideBlockedReason: blockedReason,
            CanSign: manage && c.Status == ContractStatus.PendingApproval && next is null,
            CanBindProjects: manage && ContractRules.CanBindProjects(c.Status),
            CanEditAppendix: manage && editable,
            CanUpload: manage && c.Status is not (ContractStatus.Closed or ContractStatus.Lost or ContractStatus.Cancelled or ContractStatus.Terminated),
            AllowedStatuses: manage ? allowed : []);

        var terms = new ContractTermsDto(
            c.Title, c.CustomerName, c.CustomerContactName, c.CustomerContactEmail, c.Type, c.Industry, c.TotalAmount, c.QuoteDate, c.StartDate, c.DeliveryDate,
            c.OpenEndedScope, c.ReviewDeadlineWorkdays, c.MaxReviewRounds, c.DeemedPassOnTimeout, c.IpRule, c.ScanRule, c.DeemedAcceptanceWorkdays,
            c.FinalPaymentHeldForMinorDefects, c.LiabilityCap, c.LiabilityCapAmount, c.ExcludesIndirectLoss, c.WarrantyMonths, c.IncidentNotifyHours,
            c.AuditNoticeDays, c.AuditPerYear, c.AuditCostOnVendor, c.ServiceHours, c.SupportChannel, c.Notes, c.CoopNoticeWorkdays);

        var reviewItems = ContractRules.ReviewCatalog.Select(spec =>
        {
            var item = c.ReviewItems.FirstOrDefault(i => i.Code == spec.Code);
            return new ContractReviewItemDto(
                spec.Code, spec.Name, spec.ClientTrap, spec.Defense, item?.ClientClause, item?.Stance ?? ReviewStance.Accept,
                item?.Result ?? ReviewResult.Pending, item?.Note, item?.ReviewedBy, item?.ReviewedAt);
        }).ToList();

        return new ContractDto(
            c.Id, c.ContractNo, c.Status, terms, c.SignedDate, c.ApprovalCycle, c.SubmittedAt, c.SubmittedBy, c.SnapshotHash,
            c.Members.Select(m => new ContractMemberDto(m.UserId, m.Role)).ToList(),
            reviewItems,
            flags.Select(f => new ContractFlagDto(f.ItemCode, f.Severity, f.Message)).ToList(),
            c.Approvals.OrderBy(a => a.DecidedAt).Select(a => new ContractApprovalDto(a.Cycle, a.Step, a.Decision, a.Comment, a.DecidedBy, a.DecidedByName, a.DecidedAt)).ToList(),
            required, next,
            c.DeliverableItems.OrderBy(i => i.SortOrder).Select(i => new ContractDeliverableItemDto(i.Id, i.SortOrder, i.Stage, i.Name, i.Format, i.AcceptMethod, i.RequiresCustomerSignoff)).ToList(),
            c.SlaLevels.OrderBy(l => l.Level).Select(l => new ContractSlaLevelDto(l.Id, l.Level, l.Name, l.Definition, l.ResponseHours, l.Target)).ToList(),
            c.Projects.Where(p => p.Project is not null).OrderBy(p => p.Project!.Code)
                .Select(p => new ContractProjectDto(p.ProjectId, p.Project!.Code, p.Project.Name, p.Project.Status, p.CreatedBy, p.CreatedAtUtc)).ToList(),
            attachments.Select(ContractAttachmentService.ToDto).ToList(),
            events, actions, c.CreatedBy, c.CreatedAtUtc, Convert.ToBase64String(c.RowVersion));
    }

    public static string StatusName(ContractStatus s) => s switch
    {
        ContractStatus.Quoting => "報價評估",
        ContractStatus.Reviewing => "審約中",
        ContractStatus.Negotiating => "談判中",
        ContractStatus.PendingApproval => "待核決",
        ContractStatus.Active => "履約中",
        ContractStatus.Accepted => "已驗收",
        ContractStatus.Warranty => "保固中",
        ContractStatus.Closed => "已結案",
        ContractStatus.Lost => "流標",
        ContractStatus.Cancelled => "已取消",
        _ => "已終止"
    };

    private static string ResultName(ReviewResult r) => r switch
    {
        ReviewResult.Compliant => "符合底線",
        ReviewResult.Deviation => "偏離需核決",
        ReviewResult.Compromised => "折衷採用",
        _ => "待審閱"
    };
}
