using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 需求變更單 CR 用例（FORM-CR-01，SOP 第 4 章）：PM 建立 → Tech Lead 影響評估（2 個工作天內）→ PM 決策
/// （A 順延／B 範圍交換／C 駁回或暫緩）→ 簽核（客戶 CR：PM＋Tech Lead＋客戶；內部 CR：PM＋Tech Lead）。
/// 決策時固化內容雜湊，簽核者須對該雜湊簽核，簽核後內容鎖定。客戶簽核目前由 PM 代錄（附原因）；
/// 外部 Stakeholder 自行簽核待外部帳號機制完成後啟用。專案資料列授權：非成員一律 404。
/// </summary>
public sealed class ChangeRequestService(PMSDbContext db, HolidayService holidays, TimeProvider clock)
{
    private const int MaxPageSize = 200;
    private const int NumberRetries = 5;

    /// <summary>Stakeholder 僅可見已進入簽核階段以後的客戶 CR（不含工時與內部處理細節）。</summary>
    private static readonly CrStatus[] StakeholderVisible = [CrStatus.PendingSign, CrStatus.Approved, CrStatus.Rejected, CrStatus.Deferred];

    // ---------- 建立 ----------

    public async Task<PmsResult<CrDetailDto>> CreateAsync(Guid userId, string actor, CreateCrRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200)
            return PmsError.Validation("標題為必填且不得超過 200 字。");
        if (string.IsNullOrWhiteSpace(request.Description))
            return PmsError.Validation("需求描述為必填。");
        if (!Enum.IsDefined(request.Kind)) return PmsError.Validation("CR 分類不正確。");

        var project = await db.Projects.AsNoTracking().Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound();

        var members = project.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        var access = ProjectAccessPolicy.Evaluate(userId, members);
        if (access != ProjectAccessLevel.Internal) return PmsError.NotFound();
        if (!ProjectAccessPolicy.IsPm(userId, members)) return PmsError.Forbidden("僅專案 PM 可建立 CR（SOP 4.1：新需求一律由 PM 建立 CR 單）。");
        if (project.Status != ProjectStatus.Active)
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "專案已結案或作廢，不可再建立 CR。");

        var now = clock.GetUtcNow();
        var calendar = await holidays.LoadCalendarAsync(now, ct).ConfigureAwait(false);

        for (var attempt = 1; attempt <= NumberRetries; attempt++)
        {
            Ticket? source = null;
            if (request.SourceTicketId is { } sourceId)
            {
                source = await db.Tickets.FirstOrDefaultAsync(t => t.Id == sourceId && t.ProjectId == project.Id, ct).ConfigureAwait(false);
                if (source is null) return PmsError.NotFound("來源問題單不存在。");
                if (source.ChangeRequestId is not null)
                    return PmsError.Conflict("PMS.INVALID_TRANSITION", "此問題單已建立過 CR。");
            }

            var cr = await AddNewAsync(db, project.Code, project.Id, request.Kind, request.Title, request.Description,
                source?.Id, userId, actor, calendar, now,
                source is null ? "建立 CR 單" : $"由問題單 {source.TicketNo} 建立 CR 單", ct).ConfigureAwait(false);
            if (source is not null) source.ChangeRequestId = cr.Id;

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return await GetAsync(userId, cr.Id, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsNumberConflict(ex) && attempt < NumberRetries)
            {
                db.ChangeTracker.Clear();
            }
            catch (Exception ex) when (IsNumberConflict(ex))
            {
                db.ChangeTracker.Clear();
                return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "目前建單人數較多，請稍後再試。");
            }
        }

        return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "目前建單人數較多，請稍後再試。");
    }

    /// <summary>
    /// 新增 CR（含單號流水號遞增與建立事件），不呼叫 SaveChanges，由呼叫端與其他異動一併儲存
    /// （問題單分流為 CR 時與問題單更新同一交易）。流水號以 RowVersion 樂觀並行防重複。
    /// </summary>
    internal static async Task<ChangeRequest> AddNewAsync(
        PMSDbContext db, string projectCode, Guid projectId, CrKind kind, string title, string description,
        Guid? sourceTicketId, Guid requesterId, string actor, WorkCalendar calendar, DateTimeOffset now, string createdDetail, CancellationToken ct)
    {
        var sequence = await db.ChangeRequestSequences.FirstOrDefaultAsync(s => s.ProjectId == projectId, ct).ConfigureAwait(false);
        if (sequence is null)
        {
            sequence = new ChangeRequestSequence { ProjectId = projectId, CreatedBy = actor, CreatedAtUtc = now };
            db.ChangeRequestSequences.Add(sequence);
        }

        sequence.LastNo++;
        sequence.UpdatedBy = actor;
        sequence.UpdatedAtUtc = now;

        var cr = new ChangeRequest
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CrNo = CrNumbering.Format(projectCode, sequence.LastNo),
            Kind = kind,
            Status = CrStatus.Received,
            Title = title.Trim(),
            Description = description.Trim(),
            SourceTicketId = sourceTicketId,
            RequesterUserId = requesterId,
            ReceivedAt = now,
            EvalDueAt = CrSla.EvalDue(now, calendar),
            CreatedBy = actor,
            CreatedAtUtc = now
        };
        cr.Events.Add(NewEvent(cr.Id, CrEventType.Created, createdDetail, requesterId, actor, now));
        db.ChangeRequests.Add(cr);
        return cr;
    }

    // ---------- 查詢 ----------

    public async Task<PagedResult<CrListDto>> SearchAsync(Guid userId, CrSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var filter = request.Filter ?? new CrSearchFilter();
        var now = clock.GetUtcNow();

        var memberships = await db.ProjectMembers.AsNoTracking().Where(m => m.UserId == userId)
            .Select(m => new { m.ProjectId, m.RaciRole }).ToListAsync(ct).ConfigureAwait(false);
        var internalProjects = memberships.Where(m => m.RaciRole != RaciRole.Stakeholder).Select(m => m.ProjectId).Distinct().ToList();
        var stakeholderProjects = memberships.Select(m => m.ProjectId).Distinct().Except(internalProjects).ToList();

        var visibleToStakeholder = StakeholderVisible;
        var query = db.ChangeRequests.AsNoTracking().Where(c =>
            internalProjects.Contains(c.ProjectId) ||
            (stakeholderProjects.Contains(c.ProjectId) && c.Kind == CrKind.Customer && visibleToStakeholder.Contains(c.Status)));

        if (filter.ProjectId is { } projectId) query = query.Where(c => c.ProjectId == projectId);
        if (filter.Status is { Count: > 0 } statuses) query = query.Where(c => statuses.Contains(c.Status));
        if (filter.Kind is { Count: > 0 } kinds) query = query.Where(c => kinds.Contains(c.Kind));
        if (filter.MineOnly) query = query.Where(c => c.RequesterUserId == userId);
        if (filter.EvalOverdue) query = query.Where(c => c.Assessment == null && c.Status == CrStatus.Received && c.EvalDueAt < now);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(c => c.CrNo.Contains(term) || c.Title.Contains(term));
        }

        var sort = request.Sorts.FirstOrDefault();
        var desc = sort is null || sort.Direction == SortDirection.Descending;
        query = (sort?.SortBy?.ToLowerInvariant()) switch
        {
            "evaldueat" => desc ? query.OrderByDescending(c => c.EvalDueAt) : query.OrderBy(c => c.EvalDueAt),
            "crno" => desc ? query.OrderByDescending(c => c.CrNo) : query.OrderBy(c => c.CrNo),
            _ => desc ? query.OrderByDescending(c => c.ReceivedAt) : query.OrderBy(c => c.ReceivedAt)
        };

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var rows = await query.Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(c => new
            {
                c.Id, c.CrNo, c.ProjectId, ProjectCode = c.Project!.Code, ProjectName = c.Project!.Name, c.Kind, c.Status, c.Title,
                c.ReceivedAt, c.EvalDueAt, c.Decision, c.RequesterUserId,
                EvaluatedAt = c.Assessment != null ? (DateTimeOffset?)c.Assessment.CreatedAtUtc : null,
                SourceTicketNo = db.Tickets.Where(t => t.Id == c.SourceTicketId).Select(t => t.TicketNo).FirstOrDefault(),
                SignedRoles = db.CrSignoffs.Where(s => s.ChangeRequestId == c.Id).Select(s => s.Role).ToList()
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var items = rows.Select(r => new CrListDto(
            r.Id, r.CrNo, r.ProjectId, r.ProjectCode, r.ProjectName, r.Kind, r.Status, r.Title, r.ReceivedAt, r.EvalDueAt,
            CrSla.EvalStatus(r.EvalDueAt, r.EvaluatedAt, now, r.Status), r.Decision, r.RequesterUserId, r.SourceTicketNo,
            r.Status == CrStatus.PendingSign ? CrSignoffRules.Pending(r.Kind, r.SignedRoles) : [])).ToList();

        return new PagedResult<CrListDto>(items, total, pageIndex, pageSize);
    }

    public async Task<PmsResult<CrDetailDto>> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, id, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var c = ctx.Value!;
        string? sourceNo = null;
        if (c.Cr.SourceTicketId is { } tid)
            sourceNo = await db.Tickets.AsNoTracking().Where(t => t.Id == tid).Select(t => t.TicketNo).FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return PmsResult<CrDetailDto>.Success(ToDetail(c, sourceNo, clock.GetUtcNow()));
    }

    // ---------- 影響評估（Tech Lead） ----------

    public async Task<PmsResult<CrDetailDto>> EvaluateAsync(Guid userId, string actor, Guid id, EvaluateCrRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var cr = c.Cr;

        if (!ProjectAccessPolicy.IsTechLead(userId, c.Members))
            return PmsError.Forbidden("僅專案 Tech Lead 可進行影響評估（SOP 4.3）。");

        var (layers, layerError) = CrInputRules.NormalizeLayers(request.AffectedLayers);
        if (layers is null) return PmsError.Validation(layerError!);
        if (!Enum.IsDefined(request.ApiImpact) || !Enum.IsDefined(request.CouplingImpact))
            return PmsError.Validation("API 契約影響或模組耦合度不正確。");
        if (CrInputRules.ValidateHours(request.HoursBe, request.HoursFe, request.HoursVerify) is { } hoursError)
            return PmsError.Validation(hoursError);
        if (request.Note is { Length: > 2000 }) return PmsError.Validation("備註不得超過 2000 字。");

        var verdict = CrWorkflow.Next(cr.Status, CrAction.Evaluate, null, false, out var next);
        if (!verdict.IsOk) return ToError(verdict);

        var now = clock.GetUtcNow();
        var assessment = cr.Assessment;
        if (assessment is null)
        {
            assessment = new CrImpactAssessment { Id = Guid.NewGuid(), ChangeRequestId = cr.Id, CreatedBy = actor, CreatedAtUtc = now };
            db.CrImpactAssessments.Add(assessment);
        }
        else
        {
            assessment.UpdatedBy = actor;
            assessment.UpdatedAtUtc = now;
        }

        assessment.AffectedLayers = string.Join(',', layers);
        assessment.ApiImpact = request.ApiImpact;
        assessment.CouplingImpact = request.CouplingImpact;
        assessment.SecurityImpact = request.SecurityImpact;
        assessment.HoursBe = request.HoursBe;
        assessment.HoursFe = request.HoursFe;
        assessment.HoursVerify = request.HoursVerify;
        assessment.Note = request.Note?.Trim();
        assessment.EvaluatedBy = userId;
        assessment.EvaluatedAt = now;

        cr.Status = next;
        Touch(cr, actor, now);

        var flags = new List<string>();
        if (CrInputRules.IsHighRisk(layers)) flags.Add("涉及 Domain／Infrastructure（高風險）");
        if (request.ApiImpact == CrApiImpact.Breaking) flags.Add("API 破壞性變更");
        if (request.CouplingImpact == CrCouplingImpact.ArchitectureWarning) flags.Add("架構警告");
        if (request.SecurityImpact) flags.Add("資安影響：須 Design Gate 重審");
        var total = request.HoursBe + request.HoursFe + request.HoursVerify;
        db.CrEvents.Add(NewEvent(cr.Id, CrEventType.Evaluated,
            $"影響評估完成：層級 {string.Join('、', layers)}；工時 BE {request.HoursBe}／FE {request.HoursFe}／驗證 {request.HoursVerify}（共 {total} 小時）" +
            (flags.Count > 0 ? "；" + string.Join("；", flags) : string.Empty), userId, actor, now));

        return await SaveAsync(userId, cr.Id, ct).ConfigureAwait(false);
    }

    // ---------- 決策（PM） ----------

    public async Task<PmsResult<CrDetailDto>> DecideAsync(Guid userId, string actor, Guid id, DecideCrRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var cr = c.Cr;

        if (!ProjectAccessPolicy.IsPm(userId, c.Members)) return PmsError.Forbidden("僅專案 PM 可拍板 CR 決策（SOP 4.4）。");
        if (!Enum.IsDefined(request.Decision)) return PmsError.Validation("決策方案不正確。");
        if (cr.Assessment is null) return PmsError.Conflict("PMS.INVALID_TRANSITION", "尚未完成影響評估，不可決策。");

        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.ToOffset(SlaCalculator.Offset).DateTime);
        if (CrInputRules.ValidateDecision(cr.Kind, request.Decision, request.QuoteAmount, request.NewReleaseDate,
                request.SwappedOutItems, request.Rationale, today) is { } inputError)
            return PmsError.Unprocessable("PMS.VALIDATION_FAILED", inputError);
        if (request.Rationale is { Length: > 2000 } || request.SwappedOutItems is { Length: > 2000 })
            return PmsError.Validation("說明欄位不得超過 2000 字。");

        var verdict = CrWorkflow.Next(cr.Status, CrAction.Decide, request.Decision, request.Defer, out var next);
        if (!verdict.IsOk) return ToError(verdict);

        cr.Decision = request.Decision;
        cr.DecisionRationale = request.Rationale?.Trim();
        cr.NewReleaseDate = request.Decision == CrDecision.A_Delay ? request.NewReleaseDate : null;
        cr.SwappedOutItems = request.Decision == CrDecision.B_Swap ? request.SwappedOutItems?.Trim() : null;
        cr.QuoteAmount = request.Decision == CrDecision.C_Reject ? null : request.QuoteAmount;
        cr.DecidedBy = userId;
        cr.DecidedAt = now;
        cr.Status = next;
        // 進入簽核階段時固化內容雜湊；駁回／暫緩不需簽核
        cr.ContentHash = next == CrStatus.PendingSign ? CrContentHash.Compute(ToContent(cr, cr.Assessment)) : null;
        Touch(cr, actor, now);

        var label = request.Decision switch
        {
            CrDecision.A_Delay => $"方案 A 時程順延，新上線日 {request.NewReleaseDate:yyyy-MM-dd}",
            CrDecision.B_Swap => "方案 B 範圍交換",
            _ => request.Defer ? "方案 C 暫緩（轉 Backlog）" : "方案 C 駁回"
        };
        db.CrEvents.Add(NewEvent(cr.Id, CrEventType.Decided,
            Join(label, cr.QuoteAmount is { } q ? $"報價 {q:N0}" : null, cr.DecisionRationale), userId, actor, now));

        return await SaveAsync(userId, cr.Id, ct).ConfigureAwait(false);
    }

    // ---------- 簽核 ----------

    /// <summary>PM／Tech Lead 簽核：簽的是目前的內容雜湊；全部簽完即核准。</summary>
    public async Task<PmsResult<CrDetailDto>> SignAsync(Guid userId, string actor, Guid id, SignCrRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var cr = c.Cr;

        var role = CrSignoffRules.RoleOf(userId, c.Members);
        if (role is null) return PmsError.Forbidden("僅專案 PM 或 Tech Lead 可簽核。");

        var stateVerdict = CrWorkflow.Next(cr.Status, CrAction.Sign, null, false, out _);
        if (!stateVerdict.IsOk) return ToError(stateVerdict);

        var hashError = CheckHash(cr, request.ContentHash);
        if (hashError is not null) return hashError;

        var signRule = CrSignoffRules.ValidateSign(cr.Kind, role.Value, cr.Signoffs.Select(s => s.Role));
        if (!signRule.IsOk) return ToError(signRule);

        return await AddSignoffAsync(c, actor, role.Value, isProxy: false, request.Comment, ct).ConfigureAwait(false);
    }

    /// <summary>PM 代錄客戶簽核（例外路徑）：須填原因並保留客戶簽核證明，稽核標示為代錄。</summary>
    public async Task<PmsResult<CrDetailDto>> ProxyCustomerSignAsync(Guid userId, string actor, Guid id, ProxyCustomerSignRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var cr = c.Cr;

        var stateVerdict = CrWorkflow.Next(cr.Status, CrAction.Sign, null, false, out _);
        if (!stateVerdict.IsOk) return ToError(stateVerdict);

        var proxyRule = CrSignoffRules.ValidateCustomerProxy(cr.Kind, userId, c.Members, cr.Signoffs.Select(s => s.Role), request.Reason);
        if (!proxyRule.IsOk) return ToError(proxyRule);

        var hashError = CheckHash(cr, request.ContentHash);
        if (hashError is not null) return hashError;

        return await AddSignoffAsync(c, actor, CrSignRole.Customer, isProxy: true, request.Reason.Trim(), ct).ConfigureAwait(false);
    }

    private async Task<PmsResult<CrDetailDto>> AddSignoffAsync(CrContext c, string actor, CrSignRole role, bool isProxy, string? comment, CancellationToken ct)
    {
        var cr = c.Cr;
        var now = clock.GetUtcNow();

        db.CrSignoffs.Add(new CrSignoff
        {
            Id = Guid.NewGuid(),
            ChangeRequestId = cr.Id,
            Role = role,
            SignerUserId = c.UserId,
            SignerName = actor,
            ContentHash = cr.ContentHash!,
            SignedAt = now,
            IsProxy = isProxy,
            Comment = comment is { Length: > 1000 } ? comment[..1000] : comment
        });

        var signedRoles = cr.Signoffs.Select(s => s.Role).Append(role).ToList();
        Touch(cr, actor, now);
        db.CrEvents.Add(NewEvent(cr.Id, CrEventType.Signed,
            Join($"{RoleLabel(role)}簽核{(isProxy ? "（PM 代錄客戶簽核）" : string.Empty)}", comment), c.UserId, actor, now));

        if (CrSignoffRules.IsComplete(cr.Kind, signedRoles))
        {
            cr.Status = CrStatus.Approved;
            db.CrEvents.Add(NewEvent(cr.Id, CrEventType.Approved, "簽核齊備，CR 已核准；請依決策調整排程並更新 SRS／API 規格", c.UserId, actor, now));

            // 專案綁定了履約中的合約時，自動把追加金額與順延上線日掛帳為合約變更（同一交易、冪等）
            await ContractCrHook.TryRecordAsync(db, cr, actor, now, ct).ConfigureAwait(false);
        }

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "CR 已被他人更新，請重新載入後再試。");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // 同角色重複簽核（併發）：唯一索引兜底
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.INVALID_TRANSITION", $"{RoleLabel(role)}已簽核，不可重複簽核。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(c.UserId, cr.Id, ct).ConfigureAwait(false);
    }

    // ---------- 取消（PM） ----------

    public async Task<PmsResult<CrDetailDto>> CancelAsync(Guid userId, string actor, Guid id, CancelCrRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var cr = c.Cr;

        if (!ProjectAccessPolicy.IsPm(userId, c.Members)) return PmsError.Forbidden("僅專案 PM 可取消 CR。");
        if (string.IsNullOrWhiteSpace(request.Reason)) return PmsError.Validation("取消原因為必填。");

        var verdict = CrWorkflow.Next(cr.Status, CrAction.Cancel, null, false, out var next);
        if (!verdict.IsOk) return ToError(verdict);

        var now = clock.GetUtcNow();
        cr.Status = next;
        Touch(cr, actor, now);
        db.CrEvents.Add(NewEvent(cr.Id, CrEventType.Cancelled, request.Reason.Trim(), userId, actor, now));

        return await SaveAsync(userId, cr.Id, ct).ConfigureAwait(false);
    }

    /// <summary>重啟暫緩的 CR（僅專案 PM）：清除舊評估與決策並重新起算評估期限；舊內容留在時間軸供參考。</summary>
    public async Task<PmsResult<CrDetailDto>> ReopenAsync(Guid userId, string actor, Guid id, ReopenCrRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var cr = c.Cr;

        if (!ProjectAccessPolicy.IsPm(userId, c.Members)) return PmsError.Forbidden("僅專案 PM 可重啟 CR。");
        if (string.IsNullOrWhiteSpace(request.Reason)) return PmsError.Validation("重啟原因為必填。");

        var verdict = CrWorkflow.Next(cr.Status, CrAction.Reopen, null, false, out var next);
        if (!verdict.IsOk) return ToError(verdict);

        var now = clock.GetUtcNow();
        var calendar = await holidays.LoadCalendarAsync(now, ct).ConfigureAwait(false);

        var previous = cr.Decision is { } d ? $"原決策：{d}" : "原決策：—";
        if (cr.Assessment is not null)
        {
            db.CrImpactAssessments.Remove(cr.Assessment);
            cr.Assessment = null;
        }

        cr.Status = next;
        cr.Decision = null;
        cr.DecisionRationale = null;
        cr.NewReleaseDate = null;
        cr.SwappedOutItems = null;
        cr.QuoteAmount = null;
        cr.DecidedBy = null;
        cr.DecidedAt = null;
        cr.ContentHash = null;
        cr.EvalDueAt = CrSla.EvalDue(now, calendar);
        cr.LastEvalReminderAt = null;
        Touch(cr, actor, now);
        db.CrEvents.Add(NewEvent(cr.Id, CrEventType.Reopened, $"{request.Reason.Trim()}（{previous}；評估期限重新起算）", userId, actor, now));

        return await SaveAsync(userId, cr.Id, ct).ConfigureAwait(false);
    }

    // ---------- 內部輔助 ----------

    private sealed record CrContext(
        ChangeRequest Cr, Project Project, IReadOnlyList<(Guid UserId, RaciRole Role)> Members, ProjectAccessLevel Access, Guid UserId);

    private async Task<PmsResult<CrContext>> LoadAsync(Guid userId, Guid id, bool track, CancellationToken ct)
    {
        var query = track ? db.ChangeRequests.AsQueryable() : db.ChangeRequests.AsNoTracking();
        var cr = await query.Include(c => c.Assessment).Include(c => c.Signoffs).Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);
        if (cr is null) return PmsError.NotFound();

        var projectQuery = track ? db.Projects.AsQueryable() : db.Projects.AsNoTracking();
        var project = await projectQuery.Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == cr.ProjectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound();

        var members = project.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        var access = ProjectAccessPolicy.Evaluate(userId, members);
        if (access == ProjectAccessLevel.None) return PmsError.NotFound();
        if (access == ProjectAccessLevel.Stakeholder && !(cr.Kind == CrKind.Customer && StakeholderVisible.Contains(cr.Status)))
            return PmsError.NotFound();

        return PmsResult<CrContext>.Success(new CrContext(cr, project, members, access, userId));
    }

    private async Task<PmsResult<CrContext>> LoadForActionAsync(Guid userId, Guid id, string? rowVersion, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        if (ctx.Value!.Access != ProjectAccessLevel.Internal) return PmsError.NotFound();

        if (!TryParseRowVersion(rowVersion, out var bytes)) return PmsError.Validation("rowVersion 格式不正確。");
        db.Entry(ctx.Value.Cr).Property(x => x.RowVersion).OriginalValue = bytes;
        return ctx;
    }

    private async Task<PmsResult<CrDetailDto>> SaveAsync(Guid userId, Guid crId, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "CR 已被他人更新，請重新載入後再試。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, crId, ct).ConfigureAwait(false);
    }

    /// <summary>簽核者須對目前內容簽核；雜湊不符表示內容已變動（或看到的是舊版本）。</summary>
    private static PmsError? CheckHash(ChangeRequest cr, string? provided)
    {
        if (string.IsNullOrEmpty(cr.ContentHash))
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "CR 尚未決策，沒有可簽核的內容。");
        if (!string.Equals(cr.ContentHash, provided?.Trim(), StringComparison.OrdinalIgnoreCase))
            return PmsError.Conflict("PMS.CONTENT_LOCKED", "簽核內容與目前版本不符（內容已變動），請重新載入後再簽核。");
        return null;
    }

    private CrDetailDto ToDetail(CrContext c, string? sourceTicketNo, DateTimeOffset now)
    {
        var cr = c.Cr;
        var internalView = c.Access == ProjectAccessLevel.Internal;
        var assessment = cr.Assessment;
        var signedRoles = cr.Signoffs.Select(s => s.Role).ToList();

        var isPm = ProjectAccessPolicy.IsPm(c.UserId, c.Members);
        var isTl = ProjectAccessPolicy.IsTechLead(c.UserId, c.Members);
        var myRole = CrSignoffRules.RoleOf(c.UserId, c.Members);
        var pendingSign = cr.Status == CrStatus.PendingSign;

        var actions = !internalView
            ? new CrActionsDto(false, false, false, false, false, false)
            : new CrActionsDto(
                CanEvaluate: isTl && cr.Status is CrStatus.Received or CrStatus.PendingDecision,
                CanDecide: isPm && cr.Status == CrStatus.PendingDecision,
                CanSign: pendingSign && myRole is { } r && CrSignoffRules.ValidateSign(cr.Kind, r, signedRoles).IsOk,
                CanProxyCustomerSign: pendingSign && CrSignoffRules.ValidateCustomerProxy(cr.Kind, c.UserId, c.Members, signedRoles, "x").IsOk,
                CanCancel: isPm && cr.Status is CrStatus.Received or CrStatus.PendingDecision,
                CanReopen: isPm && cr.Status == CrStatus.Deferred);

        CrAssessmentDto? assessmentDto = null;
        if (assessment is not null && internalView)
        {
            var layers = assessment.AffectedLayers.Split(',', StringSplitOptions.RemoveEmptyEntries);
            assessmentDto = new CrAssessmentDto(
                layers, CrInputRules.IsHighRisk(layers), assessment.ApiImpact, assessment.CouplingImpact, assessment.SecurityImpact,
                assessment.HoursBe, assessment.HoursFe, assessment.HoursVerify,
                assessment.HoursBe + assessment.HoursFe + assessment.HoursVerify, assessment.Note, assessment.EvaluatedBy, assessment.EvaluatedAt);
        }

        var evaluatedAt = assessment?.CreatedAtUtc;
        var today = DateOnly.FromDateTime(now.ToOffset(SlaCalculator.Offset).DateTime);

        return new CrDetailDto(
            cr.Id, cr.CrNo, cr.ProjectId, c.Project.Code, c.Project.Name, cr.Kind, cr.Status, cr.Title, cr.Description,
            internalView ? cr.SourceTicketId : null, internalView ? sourceTicketNo : null, cr.RequesterUserId,
            cr.ReceivedAt, cr.EvalDueAt, CrSla.EvalStatus(cr.EvalDueAt, evaluatedAt, now, cr.Status),
            AfterFreezeLine: c.Project.FreezeLineDate is { } freeze && freeze <= today,
            assessmentDto, DesignGateReviewRequired: internalView && assessment is { SecurityImpact: true },
            cr.Decision, cr.DecisionRationale, cr.NewReleaseDate, cr.SwappedOutItems, cr.QuoteAmount,
            cr.DecidedBy, cr.DecidedAt, cr.ContentHash,
            CrSignoffRules.Required(cr.Kind), CrSignoffRules.Pending(cr.Kind, signedRoles),
            cr.Signoffs.OrderBy(s => s.SignedAt).Select(s => new CrSignoffDto(s.Role, s.SignerName, s.SignedAt, s.IsProxy, s.Comment, s.ContentHash)).ToList(),
            internalView ? cr.Events.OrderBy(e => e.At).Select(e => new CrEventDto(e.Id, e.EventType, e.Detail, e.ActorName, e.At)).ToList() : [],
            actions, Convert.ToBase64String(cr.RowVersion));
    }

    private static CrContent ToContent(ChangeRequest cr, CrImpactAssessment a) => new(
        cr.CrNo, cr.Kind, cr.Title, cr.Description, a.AffectedLayers, a.ApiImpact, a.CouplingImpact, a.SecurityImpact,
        a.HoursBe, a.HoursFe, a.HoursVerify, cr.Decision!.Value, cr.QuoteAmount, cr.NewReleaseDate, cr.SwappedOutItems, cr.DecisionRationale);

    private static CrEvent NewEvent(Guid crId, CrEventType type, string? detail, Guid actorId, string actor, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(), ChangeRequestId = crId, EventType = type,
        Detail = detail is { Length: > 1000 } ? detail[..1000] : detail,
        ActorUserId = actorId, ActorName = actor.Length > 256 ? actor[..256] : actor, At = at
    };

    private static void Touch(ChangeRequest cr, string actor, DateTimeOffset now)
    {
        cr.UpdatedBy = actor;
        cr.UpdatedAtUtc = now;
    }

    private static string RoleLabel(CrSignRole role) => role switch
    {
        CrSignRole.Pm => "PM",
        CrSignRole.TechLead => "Tech Lead",
        _ => "客戶"
    };

    private static string Join(params string?[] parts) => string.Join("；", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static PmsError ToError(RuleResult result) => result.Outcome switch
    {
        RuleOutcome.SeparationOfDuties => PmsError.Conflict("PMS.SOD_VIOLATION", result.Detail ?? "違反角色分離。"),
        RuleOutcome.RoleNotAllowed => PmsError.Forbidden(result.Detail ?? "角色不符。"),
        _ => PmsError.Conflict("PMS.INVALID_TRANSITION", result.Detail ?? "狀態不允許該轉移。")
    };

    private static bool TryParseRowVersion(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { bytes = Convert.FromBase64String(value); return bytes.Length > 0; }
        catch (FormatException) { return false; }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static bool IsNumberConflict(Exception ex) => ex is DbUpdateConcurrencyException || (ex is DbUpdateException d && IsUniqueViolation(d));
}
