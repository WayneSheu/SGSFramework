using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 客戶問題單用例（SOP 6）：建單（單號 <c>專案代號-流水號</c>）、PM 分流與 SLA、TL 指派（修復人≠驗證人）、
/// 修復回報、非作者驗證、PM 結案。所有操作套用專案資料列授權（非成員一律 404）；
/// Stakeholder 僅得見並建立自己的問題單。SLA 燈號於讀取時動態計算，不持久化。
/// </summary>
public sealed class TicketService(PMSDbContext db, HolidayService holidays, TimeProvider clock)
{
    private const int MaxPageSize = 200;
    private const int NumberRetries = 5;

    // ---------- 建單 ----------

    public async Task<PmsResult<TicketDetailDto>> CreateAsync(Guid userId, string actor, CreateTicketRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200)
            return PmsError.Validation("標題為必填且不得超過 200 字。");
        if (string.IsNullOrWhiteSpace(request.Description))
            return PmsError.Validation("問題描述為必填。");
        if (request.Environment is { Length: > 50 } || request.Impact is { Length: > 100 })
            return PmsError.Validation("環境或影響程度欄位過長。");
        if (request.Type is { } requestedType && !Enum.IsDefined(requestedType))
            return PmsError.Validation("問題類型不正確。");

        var project = await db.Projects.AsNoTracking().Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound();

        var access = ProjectAccessPolicy.Evaluate(userId, project.Members.Select(m => (m.UserId, m.RaciRole)));
        if (access == ProjectAccessLevel.None) return PmsError.NotFound();
        if (project.Status != ProjectStatus.Active)
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "專案已結案或作廢，不可再建立問題單。");

        var now = clock.GetUtcNow();

        for (var attempt = 1; attempt <= NumberRetries; attempt++)
        {
            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Type = request.Type ?? TicketType.Incident,
                Stage = project.Stage,
                Status = TicketStatus.Received,
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Environment = request.Environment?.Trim(),
                Impact = request.Impact?.Trim(),
                Source = access == ProjectAccessLevel.Stakeholder ? TicketSource.Stakeholder : TicketSource.Manual,
                CustomerVisible = access == ProjectAccessLevel.Stakeholder || (request.CustomerVisible ?? true),
                ReporterUserId = userId,
                ReceivedAt = now,
                CreatedBy = actor,
                CreatedAtUtc = now
            };

            var sequence = await db.TicketSequences.FirstOrDefaultAsync(s => s.ProjectId == project.Id, ct).ConfigureAwait(false);
            if (sequence is null)
            {
                sequence = new TicketSequence { ProjectId = project.Id, CreatedBy = actor, CreatedAtUtc = now };
                db.TicketSequences.Add(sequence);
            }

            sequence.LastNo++;
            sequence.UpdatedBy = actor;
            sequence.UpdatedAtUtc = now;
            ticket.TicketNo = TicketNumbering.Format(project.Code, sequence.LastNo);
            ticket.Events.Add(NewEvent(ticket.Id, TicketEventType.Created, $"建立問題單（來源：{ticket.Source}，提出類型：{ticket.Type}）", userId, actor, now));
            db.Tickets.Add(ticket);

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return await GetAsync(userId, ticket.Id, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsNumberConflict(ex) && attempt < NumberRetries)
            {
                // 併發取得相同流水號：清除追蹤後重試
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

    // ---------- 查詢 ----------

    public async Task<PagedResult<TicketListDto>> SearchAsync(Guid userId, TicketSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var filter = request.Filter ?? new TicketSearchFilter();
        var now = clock.GetUtcNow();

        // 資料列授權：內部專案看全部；Stakeholder 專案僅「客戶可見且自己提出」
        var memberships = await db.ProjectMembers.AsNoTracking().Where(m => m.UserId == userId)
            .Select(m => new { m.ProjectId, m.RaciRole }).ToListAsync(ct).ConfigureAwait(false);
        var internalProjects = memberships.Where(m => m.RaciRole != RaciRole.Stakeholder).Select(m => m.ProjectId).Distinct().ToList();
        var stakeholderProjects = memberships.Select(m => m.ProjectId).Distinct().Except(internalProjects).ToList();

        var query = db.Tickets.AsNoTracking().Where(t =>
            internalProjects.Contains(t.ProjectId) ||
            (stakeholderProjects.Contains(t.ProjectId) && t.CustomerVisible && t.ReporterUserId == userId));

        if (filter.ProjectId is { } projectId) query = query.Where(t => t.ProjectId == projectId);
        if (filter.Status is { Count: > 0 } statuses) query = query.Where(t => statuses.Contains(t.Status));
        if (filter.Priority is { Count: > 0 } priorities) query = query.Where(t => t.Priority != null && priorities.Contains(t.Priority.Value));
        if (filter.Type is { Count: > 0 } types) query = query.Where(t => types.Contains(t.Type));
        if (filter.Source is { Count: > 0 } sources) query = query.Where(t => sources.Contains(t.Source));
        if (filter.AssigneeUserId is { } assignee) query = query.Where(t => t.FixerUserId == assignee || t.VerifierUserId == assignee);
        if (filter.MineOnly) query = query.Where(t => t.ReporterUserId == userId || t.FixerUserId == userId || t.VerifierUserId == userId);
        if (filter.ReceivedFrom is { } from) query = query.Where(t => t.ReceivedAt >= from);
        if (filter.ReceivedTo is { } to) query = query.Where(t => t.ReceivedAt <= to);
        if (filter.SlaStatus is { Count: > 0 } slaFilter) query = query.Where(SlaPredicate(slaFilter, now));

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(t => t.TicketNo.Contains(term) || t.Title.Contains(term));
        }

        var sort = request.Sorts.FirstOrDefault();
        var desc = sort is null || sort.Direction == SortDirection.Descending;
        query = (sort?.SortBy?.ToLowerInvariant()) switch
        {
            "dueat" => desc ? query.OrderByDescending(t => t.DueAt) : query.OrderBy(t => t.DueAt),
            "ticketno" => desc ? query.OrderByDescending(t => t.TicketNo) : query.OrderBy(t => t.TicketNo),
            _ => desc ? query.OrderByDescending(t => t.ReceivedAt) : query.OrderBy(t => t.ReceivedAt)
        };

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var rows = await query.Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(t => new
            {
                t.Id, t.TicketNo, t.ProjectId, ProjectCode = t.Project!.Code, ProjectName = t.Project!.Name, t.Type, t.Priority, t.Status, t.Title, t.Source,
                t.ReceivedAt, t.DueAt, t.ResolvedAt, t.ReporterUserId, t.FixerUserId, t.VerifierUserId
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var items = rows.Select(r => new TicketListDto(
            r.Id, r.TicketNo, r.ProjectId, r.ProjectCode, r.ProjectName, r.Type, r.Priority, r.Status, r.Title, r.Source, r.ReceivedAt, r.DueAt,
            ComputeSla(r.Status, r.DueAt, r.ResolvedAt, now), r.ReporterUserId, r.FixerUserId, r.VerifierUserId)).ToList();

        return new PagedResult<TicketListDto>(items, total, pageIndex, pageSize);
    }

    public async Task<PmsResult<TicketDetailDto>> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, id, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        string? crNo = null;
        if (ctx.Value!.Access == ProjectAccessLevel.Internal && ctx.Value.Ticket.ChangeRequestId is { } crId)
            crNo = await db.ChangeRequests.AsNoTracking().Where(x => x.Id == crId).Select(x => x.CrNo).FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return PmsResult<TicketDetailDto>.Success(ToDetail(ctx.Value!, crNo, clock.GetUtcNow()));
    }

    // ---------- 分流（PM） ----------

    public async Task<PmsResult<TicketDetailDto>> TriageAsync(Guid userId, string actor, Guid id, TriageTicketRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var t = c.Ticket;

        if (!ProjectAccessPolicy.IsPm(userId, c.Members))
            return PmsError.Forbidden("僅專案 PM 可分流問題單。");
        if (!Enum.IsDefined(request.Type)) return PmsError.Validation("type 不正確。");

        // 系統異常必須分級；其他類型不分級
        if (request.Type == TicketType.Incident && request.Priority is null)
            return PmsError.Validation("系統異常須指定優先級（P1／P2／P3）。");
        if (request.Type != TicketType.Incident && request.Priority is not null)
            return PmsError.Validation("僅系統異常可指定優先級。");
        if (request.Priority is { } p && !Enum.IsDefined(p)) return PmsError.Validation("priority 不正確。");

        if (request.Type == TicketType.ChangeRequest && request.CrKind is not { } crKindCheck)
            return PmsError.Validation("分流為需求變更須指定 CR 分類（客戶 CR 或內部 CR）。");
        if (request.Type == TicketType.ChangeRequest && !Enum.IsDefined(request.CrKind!.Value))
            return PmsError.Validation("CR 分類不正確。");

        var verdict = TicketWorkflow.Next(t.Status, TicketAction.Triage, request.Type, out var next);
        if (!verdict.IsOk) return ToError(verdict);

        var now = clock.GetUtcNow();
        var calendar = await holidays.LoadCalendarAsync(t.ReceivedAt, ct).ConfigureAwait(false);

        t.Type = request.Type;
        t.Priority = request.Priority;
        t.Status = next;
        t.DueAt = SlaCalculator.Due(request.Type, request.Priority, t.ReceivedAt, calendar);
        t.FirstResponseAt = now;
        Touch(t, actor, now);

        var label = request.Type == TicketType.Incident ? $"系統異常 {request.Priority}" : request.Type.ToString();
        var due = t.DueAt is { } d ? $"，應解決期限 {d.ToOffset(SlaCalculator.Offset):yyyy-MM-dd HH:mm}（UTC+8）" : "（CR 待報價確認，無 SLA 期限）";
        string? crNote = null;
        if (request.Type == TicketType.ChangeRequest)
        {
            // 分流為 CR：自動建立 CR 單（SOP 6、4），與問題單更新同一交易儲存；評估期限自 CR 受理起算 2 個工作天
            var crCalendar = await holidays.LoadCalendarAsync(now, ct).ConfigureAwait(false);
            var cr = await ChangeRequestService.AddNewAsync(
                db, c.Project.Code, c.Project.Id, request.CrKind!.Value, t.Title, t.Description,
                t.Id, userId, actor, crCalendar, now, $"由問題單 {t.TicketNo} 分流建立", ct).ConfigureAwait(false);
            t.ChangeRequestId = cr.Id;
            crNote = $"已建立 {(request.CrKind == CrKind.Customer ? "客戶" : "內部")} CR {cr.CrNo}";
        }

        AddEvent(t, TicketEventType.Triaged, Join($"分流為 {label}{due}", crNote, request.Note), userId, actor, now);

        return await SaveAsync(c, userId, ct).ConfigureAwait(false);
    }

    // ---------- 指派（Tech Lead） ----------

    public async Task<PmsResult<TicketDetailDto>> AssignAsync(Guid userId, string actor, Guid id, AssignTicketRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var t = c.Ticket;

        if (!ProjectAccessPolicy.IsTechLead(userId, c.Members))
            return PmsError.Forbidden("僅專案 Tech Lead 可指派修復人與驗證人。");
        if (t.Type != TicketType.Incident)
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "僅系統異常需要指派修復人與驗證人。");
        if (t.Status is not (TicketStatus.InProgress or TicketStatus.PendingVerify))
            return PmsError.Conflict("PMS.INVALID_TRANSITION", $"問題單目前狀態為 {t.Status}，不可指派。");

        var sod = TicketSeparationOfDuties.ValidateAssignment(request.FixerUserId, request.VerifierUserId, c.Members);
        if (!sod.IsOk) return ToError(sod);

        var now = clock.GetUtcNow();
        t.FixerUserId = request.FixerUserId;
        t.VerifierUserId = request.VerifierUserId;
        Touch(t, actor, now);
        AddEvent(t, TicketEventType.Assigned, $"指派修復人 {Short(request.FixerUserId)}、驗證人 {Short(request.VerifierUserId)}", userId, actor, now);

        return await SaveAsync(c, userId, ct).ConfigureAwait(false);
    }

    // ---------- 修復回報（修復人） ----------

    public async Task<PmsResult<TicketDetailDto>> ResolveAsync(Guid userId, string actor, Guid id, ResolveTicketRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var t = c.Ticket;

        if (string.IsNullOrWhiteSpace(request.Summary)) return PmsError.Validation("修復說明為必填。");

        var verdict = TicketWorkflow.Next(t.Status, TicketAction.Resolve, t.Type, out var next);
        if (!verdict.IsOk) return ToError(verdict);

        var role = TicketSeparationOfDuties.ValidateResolve(userId, t.FixerUserId);
        if (!role.IsOk) return ToError(role);

        // Bug 驅動測試（SOP 3.4）：修復前須先有能重現問題的失敗測試
        if (!request.ReproTestAdded)
            return PmsError.Unprocessable("PMS.REPRO_TEST_REQUIRED", "修復須附能重現問題的測試（Bug 驅動測試）；請補上重現測試後再回報。");

        var now = clock.GetUtcNow();
        t.Status = next;
        Touch(t, actor, now);

        var detail = Join(request.Summary.Trim(), request.PullRequestUrl is { Length: > 0 } pr ? $"PR：{pr.Trim()}" : null);
        if (request.SpecUpdated == true) detail = Join(detail, "已同步更新 SRS／API 規格");
        AddEvent(t, TicketEventType.Resolved, detail, userId, actor, now);

        return await SaveAsync(c, userId, ct).ConfigureAwait(false);
    }

    // ---------- 驗證（非作者） ----------

    public async Task<PmsResult<TicketDetailDto>> VerifyAsync(Guid userId, string actor, Guid id, VerifyTicketRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var t = c.Ticket;

        var action = request.Passed ? TicketAction.VerifyPass : TicketAction.VerifyFail;
        var verdict = TicketWorkflow.Next(t.Status, action, t.Type, out var next);
        if (!verdict.IsOk) return ToError(verdict);

        var sod = TicketSeparationOfDuties.ValidateVerify(userId, t.FixerUserId, t.VerifierUserId);
        if (!sod.IsOk) return ToError(sod);

        if (request.Passed && !request.RegressionPassed)
            return PmsError.Unprocessable("PMS.VALIDATION_FAILED", "驗證通過須確認已完成回歸測試。");
        if (!request.Passed && string.IsNullOrWhiteSpace(request.Note))
            return PmsError.Validation("驗證未通過須說明原因。");

        var now = clock.GetUtcNow();
        t.Status = next;
        Touch(t, actor, now);
        AddEvent(t, request.Passed ? TicketEventType.Verified : TicketEventType.VerificationFailed,
            Join(request.Passed ? "驗證通過（含回歸測試）" : "驗證未通過，退回修復", request.Note), userId, actor, now);

        return await SaveAsync(c, userId, ct).ConfigureAwait(false);
    }

    // ---------- 結案／重開／取消（PM） ----------

    public async Task<PmsResult<TicketDetailDto>> CloseAsync(Guid userId, string actor, Guid id, CloseTicketRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var t = c.Ticket;

        if (!ProjectAccessPolicy.IsPm(userId, c.Members))
            return PmsError.Forbidden("僅專案 PM 可結案問題單。");

        var verdict = TicketWorkflow.Next(t.Status, TicketAction.Close, t.Type, out var next);
        if (!verdict.IsOk) return ToError(verdict);

        var now = clock.GetUtcNow();
        t.Status = next;
        t.ResolvedAt = now;
        Touch(t, actor, now);

        var sla = SlaCalculator.Status(t.DueAt, t.ResolvedAt, now);
        var slaText = sla switch { SlaStatus.MetOnTime => "SLA 準時達成", SlaStatus.MetLate => "SLA 超時完成", _ => null };
        AddEvent(t, TicketEventType.Closed,
            Join("結案", slaText, request.ReleaseNotesRef is { Length: > 0 } rn ? $"Release Notes：{rn.Trim()}" : null, request.CloseNote), userId, actor, now);

        return await SaveAsync(c, userId, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<TicketDetailDto>> ReopenAsync(Guid userId, string actor, Guid id, ReopenTicketRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var t = c.Ticket;

        if (!ProjectAccessPolicy.IsPm(userId, c.Members)) return PmsError.Forbidden("僅專案 PM 可重開問題單。");
        if (string.IsNullOrWhiteSpace(request.Reason)) return PmsError.Validation("重開原因為必填（例：客戶複測未通過）。");

        var verdict = TicketWorkflow.Next(t.Status, TicketAction.Reopen, t.Type, out var next);
        if (!verdict.IsOk) return ToError(verdict);

        var now = clock.GetUtcNow();
        t.Status = next;
        Touch(t, actor, now);
        AddEvent(t, TicketEventType.Reopened, request.Reason.Trim(), userId, actor, now);

        return await SaveAsync(c, userId, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<TicketDetailDto>> CancelAsync(Guid userId, string actor, Guid id, CancelTicketRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, id, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var t = c.Ticket;

        if (!ProjectAccessPolicy.IsPm(userId, c.Members)) return PmsError.Forbidden("僅專案 PM 可取消問題單。");
        if (string.IsNullOrWhiteSpace(request.Reason)) return PmsError.Validation("取消原因為必填。");

        var verdict = TicketWorkflow.Next(t.Status, TicketAction.Cancel, t.Type, out var next);
        if (!verdict.IsOk) return ToError(verdict);

        var now = clock.GetUtcNow();
        t.Status = next;
        Touch(t, actor, now);
        AddEvent(t, TicketEventType.Cancelled, request.Reason.Trim(), userId, actor, now);

        return await SaveAsync(c, userId, ct).ConfigureAwait(false);
    }

    // ---------- 內部輔助 ----------

    internal sealed record TicketContext(
        Ticket Ticket, Project Project, IReadOnlyList<(Guid UserId, RaciRole Role)> Members, ProjectAccessLevel Access, Guid UserId);

    /// <summary>載入單與專案成員並套用資料列授權（非成員、Stakeholder 看不到者一律 404）。</summary>
    internal async Task<PmsResult<TicketContext>> LoadAsync(Guid userId, Guid id, bool track, CancellationToken ct)
    {
        var query = track ? db.Tickets.AsQueryable() : db.Tickets.AsNoTracking();
        var ticket = await query.Include(t => t.Events).Include(t => t.Attachments.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (ticket is null) return PmsError.NotFound();

        var projectQuery = track ? db.Projects.AsQueryable() : db.Projects.AsNoTracking();
        var project = await projectQuery.Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == ticket.ProjectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound();

        var members = project.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        var access = ProjectAccessPolicy.Evaluate(userId, members);
        if (access == ProjectAccessLevel.None) return PmsError.NotFound();
        if (access == ProjectAccessLevel.Stakeholder && !(ticket.CustomerVisible && ticket.ReporterUserId == userId))
            return PmsError.NotFound();

        return PmsResult<TicketContext>.Success(new TicketContext(ticket, project, members, access, userId));
    }

    /// <summary>載入供狀態轉移：僅內部成員，並以 RowVersion 設定樂觀並行檢查。</summary>
    private async Task<PmsResult<TicketContext>> LoadForActionAsync(Guid userId, Guid id, string? rowVersion, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, id, track: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        if (ctx.Value!.Access != ProjectAccessLevel.Internal) return PmsError.NotFound();

        if (!TryParseRowVersion(rowVersion, out var bytes)) return PmsError.Validation("rowVersion 格式不正確。");
        db.Entry(ctx.Value.Ticket).Property(x => x.RowVersion).OriginalValue = bytes;
        return ctx;
    }

    private async Task<PmsResult<TicketDetailDto>> SaveAsync(TicketContext c, Guid userId, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || ex is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } })
        {
            // 併發更新，或同時建立 CR 取得相同流水號：請使用者重試
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "問題單已被他人更新，請重新載入後再試。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, c.Ticket.Id, ct).ConfigureAwait(false);
    }

    private TicketDetailDto ToDetail(TicketContext c, string? changeRequestNo, DateTimeOffset now)
    {
        var t = c.Ticket;
        var internalView = c.Access == ProjectAccessLevel.Internal;
        var sla = ComputeSla(t.Status, t.DueAt, t.ResolvedAt, now);

        // 首次回應期限於分流後才有意義（依類型／優先級）；顯示用途，以週一至週五估算（不含假日資料）
        DateTimeOffset? firstResponseDue = t.FirstResponseAt is null
            ? null
            : SlaCalculator.FirstResponseDue(t.Type, t.Priority, t.ReceivedAt, new WorkCalendar());

        var isPm = ProjectAccessPolicy.IsPm(c.UserId, c.Members);
        var isTl = ProjectAccessPolicy.IsTechLead(c.UserId, c.Members);
        var open = t.Status is not (TicketStatus.Closed or TicketStatus.Cancelled);

        var actions = !internalView
            ? new TicketActionsDto(false, false, false, false, false, false, false, open)
            : new TicketActionsDto(
                CanTriage: isPm && t.Status == TicketStatus.Received,
                CanAssign: isTl && t.Type == TicketType.Incident && t.Status is TicketStatus.InProgress or TicketStatus.PendingVerify,
                CanResolve: t.Type == TicketType.Incident && t.Status == TicketStatus.InProgress && t.FixerUserId == c.UserId,
                CanVerify: t.Status == TicketStatus.PendingVerify && t.VerifierUserId == c.UserId && t.FixerUserId != c.UserId,
                CanClose: isPm && (t.Status == TicketStatus.PendingRetest || (t.Status == TicketStatus.InProgress && t.Type != TicketType.Incident)),
                CanCancel: isPm && t.Status is TicketStatus.Received or TicketStatus.InProgress,
                CanReopen: isPm && t.Status == TicketStatus.PendingRetest,
                CanUpload: open);

        var attachments = t.Attachments.Where(a => !a.IsDeleted).OrderBy(a => a.CreatedAtUtc).Select(a => new TicketAttachmentDto(
            a.Id, a.FileName, a.OriginalFileName, a.ContentType, a.FileSize, a.MaskConfirmed,
            a.ContentType is { } ct && (ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || ct.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)),
            a.CreatedBy, a.CreatedAtUtc)).ToList();

        // 事件含內部處理細節，Stakeholder 不可見
        var events = internalView
            ? t.Events.OrderBy(e => e.At).Select(e => new TicketEventDto(e.Id, e.EventType, e.Detail, e.ActorName, e.At)).ToList()
            : [];

        return new TicketDetailDto(
            t.Id, t.TicketNo, t.ProjectId, c.Project.Code, t.Type, t.Priority, t.Stage, t.Status, t.Title, t.Description,
            t.Environment, t.Impact, t.Source, t.CustomerVisible, t.ReporterUserId, t.ReceivedAt, t.FirstResponseAt, firstResponseDue,
            t.DueAt, t.ResolvedAt, sla,
            internalView ? t.FixerUserId : null, internalView ? t.VerifierUserId : null,
            RcaRequired: t.Status == TicketStatus.Closed && (t.Priority == TicketPriority.P1 || sla == SlaStatus.MetLate),
            internalView ? t.ChangeRequestId : null, internalView ? changeRequestNo : null,
            attachments, events, actions, Convert.ToBase64String(t.RowVersion));
    }

    internal static SlaStatus ComputeSla(TicketStatus status, DateTimeOffset? due, DateTimeOffset? resolvedAt, DateTimeOffset now) =>
        status == TicketStatus.Cancelled ? SlaStatus.NotApplicable : SlaCalculator.Status(due, resolvedAt, now);

    /// <summary>將 SLA 燈號篩選轉為 SQL 可翻譯的條件（與 <see cref="SlaCalculator.Status"/> 一致）。</summary>
    private static Expression<Func<Ticket, bool>> SlaPredicate(IReadOnlyList<SlaStatus> wanted, DateTimeOffset now)
    {
        var soon = now + SlaCalculator.DueSoonThreshold;
        var set = wanted.ToHashSet();

        return t =>
            (set.Contains(SlaStatus.NotApplicable) && (t.DueAt == null || t.Status == TicketStatus.Cancelled)) ||
            (t.Status != TicketStatus.Cancelled && t.DueAt != null && (
                (set.Contains(SlaStatus.MetOnTime) && t.ResolvedAt != null && t.ResolvedAt <= t.DueAt) ||
                (set.Contains(SlaStatus.MetLate) && t.ResolvedAt != null && t.ResolvedAt > t.DueAt) ||
                (set.Contains(SlaStatus.Overdue) && t.ResolvedAt == null && t.DueAt < now) ||
                (set.Contains(SlaStatus.DueSoon) && t.ResolvedAt == null && t.DueAt >= now && t.DueAt < soon) ||
                (set.Contains(SlaStatus.InProgress) && t.ResolvedAt == null && t.DueAt >= soon)));
    }

    internal static TicketEvent NewEvent(Guid ticketId, TicketEventType type, string? detail, Guid actorId, string actor, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(), TicketId = ticketId, EventType = type, Detail = Trim(detail, 1000), ActorUserId = actorId, ActorName = Trim(actor, 256) ?? string.Empty, At = at
    };

    private void AddEvent(Ticket ticket, TicketEventType type, string? detail, Guid actorId, string actor, DateTimeOffset at) =>
        db.TicketEvents.Add(NewEvent(ticket.Id, type, detail, actorId, actor, at));

    private static void Touch(Ticket t, string actor, DateTimeOffset now)
    {
        t.UpdatedBy = actor;
        t.UpdatedAtUtc = now;
    }

    private static string? Trim(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];

    private static string Join(params string?[] parts) => string.Join("；", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string Short(Guid id) => id.ToString("N")[..8];

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

    private static bool IsNumberConflict(Exception ex) =>
        ex is DbUpdateConcurrencyException || ex is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } };
}
