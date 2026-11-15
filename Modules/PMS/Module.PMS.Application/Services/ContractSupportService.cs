using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 非服務時間支援申請與加班計費（規範第二章 階段四）：標準服務時間（平日 09:00–18:00）以外的支援須提前 5 個工作日申請，
/// 依平日加班 1.5 倍、深夜與例假日 2.0 倍、國定假日 2.5 倍計算加權時數；核決主管核准（不得核准自己的申請）後才可動員；
/// 完成後記錄實際起訖，得出實際加權時數作為請款依據。僅合約成員可見；業務與合約 PM 可申請、記錄與取消。
/// </summary>
public sealed class ContractSupportService(PMSDbContext db, HolidayService holidays, TimeProvider clock, ILogger<ContractSupportService> logger)
{
    private const int MaxRequestsPerContract = 500;
    private const int NumberRetries = 3;

    // ---------- 查詢 ----------

    public async Task<PmsResult<SupportOverviewDto>> GetAsync(Guid userId, Guid contractId, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, contractId, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var (c, members) = (ctx.Value!.Contract, ctx.Value.Members);

        var today = Today();
        var calendar = await CalendarAsync(today, ct).ConfigureAwait(false);
        var rows = await db.ContractSupportRequests.AsNoTracking().Where(r => r.ContractId == contractId).OrderByDescending(r => r.Number).ToListAsync(ct).ConfigureAwait(false);

        var manage = ContractRules.CanManage(userId, members);
        var executive = ContractRules.HasRole(userId, ContractMemberRole.Executive, members);
        var terms = TermsOf(c);
        var items = rows.Select(r => ToDto(r, calendar, terms, userId, manage, executive)).ToList();

        return PmsResult<SupportOverviewDto>.Success(new SupportOverviewDto(
            items,
            rows.Where(r => r.Status == SupportStatus.Approved).Sum(r => r.EstimatedWeightedHours),
            rows.Where(r => r.Status == SupportStatus.Completed).Sum(r => r.ActualWeightedHours ?? 0),
            manage && IsPerformance(c.Status), IsPerformance(c.Status), calendar.AddWorkdays(today, terms.AdvanceWorkdays),
            new SupportSettingsDto(terms.AdvanceWorkdays, terms.MaxWindowDays, terms.WeekdayRate, terms.NightRestRate, terms.HolidayRate, terms.RequiresApproval,
                c.SupportSettingsReason, c.SupportSettingsUpdatedBy, c.SupportSettingsUpdatedAt),
            manage && IsPerformance(c.Status)));
    }

    /// <summary>試算：給定時間窗口的各倍率時數與加權時數，並檢查是否符合申請規則（提前 5 個工作日、需有加班時段）。</summary>
    public async Task<PmsResult<OvertimeEstimateDto>> EstimateAsync(Guid userId, Guid contractId, EstimateSupportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadAsync(userId, contractId, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var today = Today();
        var calendar = await CalendarAsync(today, ct).ConfigureAwait(false);
        var terms = TermsOf(ctx.Value!.Contract);
        var problem = OvertimeRules.ValidateRequest("試算", request.Start, request.End, today, calendar, null, terms);
        var analysis = request.End > request.Start ? OvertimeRules.Analyze(request.Start, request.End, calendar, terms) : new OvertimeBreakdown(0, 0, 0, []);

        return PmsResult<OvertimeEstimateDto>.Success(new OvertimeEstimateDto(
            analysis.TotalHours, analysis.OvertimeHours, analysis.WeightedHours, ToSegments(analysis), calendar.AddWorkdays(today, terms.AdvanceWorkdays), problem));
    }

    // ---------- 申請與處理 ----------

    /// <summary>
    /// 調整本合約的非服務時間支援參數（提前申請工作日、窗口上限、三種加班倍率）；法規或雙方約定調整時使用。
    /// 只影響之後的試算、申請與實際時數計算，已記錄的預估與實際數字不會被改寫。
    /// </summary>
    public async Task<PmsResult<SupportOverviewDto>> UpdateSettingsAsync(Guid userId, string actor, Guid contractId, UpdateSupportSettingsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var terms = new OvertimeTerms(request.AdvanceWorkdays, request.MaxWindowDays, request.WeekdayRate, request.NightRestRate, request.HolidayRate, request.RequiresApproval);
        var invalid = terms.Validate();
        if (invalid is not null) return PmsError.Validation(invalid);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500) return PmsError.Validation("請填寫調整原因（500 字內，例如法規修正或雙方約定）。");

        var c = ctx.Value!.Contract;
        var before = TermsOf(c);
        if (before == terms) return PmsError.Validation("參數與目前相同，無需儲存。");

        var now = clock.GetUtcNow();
        c.SupportAdvanceWorkdays = terms.AdvanceWorkdays;
        c.SupportMaxWindowDays = terms.MaxWindowDays;
        c.OvertimeWeekdayRate = terms.WeekdayRate;
        c.OvertimeNightRestRate = terms.NightRestRate;
        c.OvertimeHolidayRate = terms.HolidayRate;
        c.SupportRequiresApproval = terms.RequiresApproval;
        c.SupportSettingsReason = request.Reason.Trim();
        c.SupportSettingsUpdatedBy = actor;
        c.SupportSettingsUpdatedAt = now;
        c.UpdatedBy = actor;
        c.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(contractId,
            $"調整非服務時間支援參數：提前 {before.AdvanceWorkdays}→{terms.AdvanceWorkdays} 個工作日、窗口 {before.MaxWindowDays}→{terms.MaxWindowDays} 天、" +
            $"倍率 {before.WeekdayRate:0.##}/{before.NightRestRate:0.##}/{before.HolidayRate:0.##}→{terms.WeekdayRate:0.##}/{terms.NightRestRate:0.##}/{terms.HolidayRate:0.##}，內部核准{(before.RequiresApproval ? "需要" : "免")}→{(terms.RequiresApproval ? "需要" : "免")}（{request.Reason.Trim()}）",
            userId, actor, now));
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<SupportOverviewDto>> CreateAsync(Guid userId, string actor, Guid contractId, CreateSupportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var today = Today();
        var calendar = await CalendarAsync(today, ct).ConfigureAwait(false);
        var terms = TermsOf(ctx.Value!.Contract);
        var invalid = OvertimeRules.ValidateRequest(request.Title, request.Start, request.End, today, calendar, request.Note, terms);
        if (invalid is not null) return PmsError.Validation(invalid);
        var clientInvalid = OvertimeRules.ValidateClientRequest(
            request.ClientRequestedDate, request.ClientRequestChannel, request.ClientRequestRef, today, DateOnly.FromDateTime(request.Start.ToOffset(SlaCalculator.Offset).DateTime));
        if (clientInvalid is not null) return PmsError.Validation(clientInvalid);
        if (await db.ContractSupportRequests.CountAsync(r => r.ContractId == contractId, ct).ConfigureAwait(false) >= MaxRequestsPerContract)
            return PmsError.Validation($"每份合約最多 {MaxRequestsPerContract} 張支援申請。");

        var analysis = OvertimeRules.Analyze(request.Start, request.End, calendar, terms);
        var now = clock.GetUtcNow();

        for (var attempt = 1; attempt <= NumberRetries; attempt++)
        {
            var number = (await db.ContractSupportRequests.Where(r => r.ContractId == contractId).MaxAsync(r => (int?)r.Number, ct).ConfigureAwait(false) ?? 0) + 1;
            var row = new ContractSupportRequest
            {
                Id = Guid.NewGuid(), ContractId = contractId, Number = number, Title = request.Title.Trim(), WindowStart = request.Start, WindowEnd = request.End,
                EstimatedHours = analysis.TotalHours, EstimatedOvertimeHours = analysis.OvertimeHours, EstimatedWeightedHours = analysis.WeightedHours,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), RequestedBy = userId, RequestedByName = actor, RequestedAt = now,
                ClientRequestedDate = request.ClientRequestedDate, ClientRequestChannel = request.ClientRequestChannel!.Trim(),
                ClientRequestRef = string.IsNullOrWhiteSpace(request.ClientRequestRef) ? null : request.ClientRequestRef.Trim(),
                CreatedBy = actor, CreatedAtUtc = now
            };

            // 合約約定免內部核准：甲方已提出即視為核准（保留紀錄，核准人欄標示依合約參數）
            if (!terms.RequiresApproval)
            {
                row.Status = SupportStatus.Approved;
                row.DecidedByName = "依合約參數免內部核准";
                row.DecidedAt = now;
            }

            db.ContractSupportRequests.Add(row);
            db.ContractEvents.Add(NewEvent(contractId,
                $"登錄甲方提出的非服務時間支援 #{number}「{request.Title.Trim()}」（{request.ClientRequestedDate:yyyy-MM-dd} 以{request.ClientRequestChannel!.Trim()}提出；預估加班 {analysis.OvertimeHours:0.##} 小時，加權 {analysis.WeightedHours:0.##} 小時；{(terms.RequiresApproval ? "待內部核決" : "免內部核准，已視為核准")}）{ContractService.EvidenceNote(request.EvidenceFileName)}",
                userId, actor, now));

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return await GetAsync(userId, contractId, ct).ConfigureAwait(false);
            }
            catch (DbUpdateException) when (attempt < NumberRetries)
            {
                db.ChangeTracker.Clear();
            }
        }

        return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "申請編號產生衝突，請稍後再試。");
    }

    public async Task<PmsResult<SupportOverviewDto>> DecideAsync(Guid userId, string actor, Guid contractId, Guid requestId, DecideSupportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadAsync(userId, contractId, track: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        if (!IsPerformance(ctx.Value!.Contract.Status)) return PmsError.Conflict("PMS.INVALID_TRANSITION", "合約須在履約階段才能處理支援申請。");

        var row = await db.ContractSupportRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.ContractId == contractId, ct).ConfigureAwait(false);
        if (row is null) return PmsError.NotFound("支援申請不存在。");

        var executive = ContractRules.HasRole(userId, ContractMemberRole.Executive, ctx.Value.Members);
        var invalid = OvertimeRules.ValidateDecision(row.Status, request.Approve, request.Note, userId, row.RequestedBy, executive);
        if (invalid is not null) return PmsError.Conflict("PMS.INVALID_TRANSITION", invalid);

        var now = clock.GetUtcNow();
        row.Status = request.Approve ? SupportStatus.Approved : SupportStatus.Rejected;
        row.DecidedBy = userId;
        row.DecidedByName = actor;
        row.DecidedAt = now;
        row.Note = string.IsNullOrWhiteSpace(request.Note) ? row.Note : request.Note.Trim();
        row.UpdatedBy = actor;
        row.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(contractId, $"非服務時間支援申請 #{row.Number}：{(request.Approve ? "核准" : "駁回")}", userId, actor, now));
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<SupportOverviewDto>> CompleteAsync(Guid userId, string actor, Guid contractId, Guid requestId, CompleteSupportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var row = await db.ContractSupportRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.ContractId == contractId, ct).ConfigureAwait(false);
        if (row is null) return PmsError.NotFound("支援申請不存在。");

        var now = clock.GetUtcNow();
        var terms = TermsOf(ctx.Value!.Contract);
        var invalid = OvertimeRules.ValidateComplete(row.Status, request.ActualStart, request.ActualEnd, now, request.Note, terms);
        if (invalid is not null) return PmsError.Conflict("PMS.INVALID_TRANSITION", invalid);

        var calendar = await CalendarAsync(Today(), ct).ConfigureAwait(false);
        var analysis = OvertimeRules.Analyze(request.ActualStart, request.ActualEnd, calendar, terms);

        row.Status = SupportStatus.Completed;
        row.ActualStart = request.ActualStart;
        row.ActualEnd = request.ActualEnd;
        row.ActualOvertimeHours = analysis.OvertimeHours;
        row.ActualWeightedHours = analysis.WeightedHours;
        row.Note = string.IsNullOrWhiteSpace(request.Note) ? row.Note : request.Note.Trim();
        row.UpdatedBy = actor;
        row.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(contractId, $"非服務時間支援 #{row.Number} 完成：實際加班 {analysis.OvertimeHours:0.##} 小時，加權 {analysis.WeightedHours:0.##} 小時", userId, actor, now));
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<SupportOverviewDto>> CancelAsync(Guid userId, string actor, Guid contractId, Guid requestId, CancelSupportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var row = await db.ContractSupportRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.ContractId == contractId, ct).ConfigureAwait(false);
        if (row is null) return PmsError.NotFound("支援申請不存在。");
        var invalid = OvertimeRules.ValidateCancel(row.Status);
        if (invalid is not null) return PmsError.Conflict("PMS.INVALID_TRANSITION", invalid);
        if (request.Note is { Length: > 1000 }) return PmsError.Validation("備註不得超過 1000 字。");

        var now = clock.GetUtcNow();
        row.Status = SupportStatus.Cancelled;
        row.Note = string.IsNullOrWhiteSpace(request.Note) ? row.Note : request.Note.Trim();
        row.UpdatedBy = actor;
        row.UpdatedAtUtc = now;
        db.ContractEvents.Add(NewEvent(contractId, $"取消非服務時間支援申請 #{row.Number}", userId, actor, now));
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    // ---------- 內部輔助 ----------

    private sealed record Ctx(Contract Contract, IReadOnlyList<(Guid UserId, ContractMemberRole Role)> Members);

    private static bool IsPerformance(ContractStatus s) => s is ContractStatus.Active or ContractStatus.Accepted or ContractStatus.Warranty;

    private async Task<PmsResult<Ctx>> LoadAsync(Guid userId, Guid contractId, bool track, CancellationToken ct)
    {
        var query = db.Contracts.AsQueryable();
        if (!track) query = query.AsNoTracking();

        var contract = await query.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == contractId, ct).ConfigureAwait(false);
        var members = contract?.Members.Select(m => (m.UserId, m.Role)).ToList();
        if (contract is null || members is null || !ContractRules.IsMember(userId, members)) return PmsError.NotFound("合約不存在。");

        return PmsResult<Ctx>.Success(new Ctx(contract, members));
    }

    private async Task<PmsResult<Ctx>> LoadManageAsync(Guid userId, Guid contractId, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, contractId, track: true, ct).ConfigureAwait(false);
        if (ctx.Error is not null) return ctx;
        if (!ContractRules.CanManage(userId, ctx.Value!.Members)) return PmsError.Forbidden("僅合約的業務或合約 PM 可執行此操作。");
        if (!IsPerformance(ctx.Value.Contract.Status)) return PmsError.Conflict("PMS.INVALID_TRANSITION", "合約須在履約階段（履約中、已驗收、保固中）才能處理支援申請。");
        return ctx;
    }

    private async Task<PmsResult<SupportOverviewDto>> SaveAsync(Guid userId, Guid contractId, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "資料已被他人更新，請重新載入後再試。");
        }
        catch (DbUpdateException ex)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning(ex, "[PMS] 支援申請儲存失敗：{ContractId}", contractId);
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "資料剛被他人更新或重複，請重新載入後再試。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    internal static OvertimeTerms TermsOf(Contract c) =>
        new(c.SupportAdvanceWorkdays, c.SupportMaxWindowDays, c.OvertimeWeekdayRate, c.OvertimeNightRestRate, c.OvertimeHolidayRate, c.SupportRequiresApproval);

    private static SupportRequestDto ToDto(ContractSupportRequest r, WorkCalendar calendar, OvertimeTerms terms, Guid userId, bool manage, bool executive)
    {
        var segments = ToSegments(OvertimeRules.Analyze(r.WindowStart, r.WindowEnd, calendar, terms));
        var decideBlocked = OvertimeRules.ValidateDecision(r.Status, true, null, userId, r.RequestedBy, executive);
        return new SupportRequestDto(
            r.Id, r.Number, r.Title, r.WindowStart, r.WindowEnd, r.Status, r.EstimatedHours, r.EstimatedOvertimeHours, r.EstimatedWeightedHours, segments,
            r.ActualStart, r.ActualEnd, r.ActualOvertimeHours, r.ActualWeightedHours, r.RequestedByName, r.RequestedAt, r.DecidedByName, r.DecidedAt, r.Note,
            decideBlocked is null, manage && r.Status == SupportStatus.Approved, manage && OvertimeRules.ValidateCancel(r.Status) is null, Convert.ToBase64String(r.RowVersion),
            r.ClientRequestedDate, r.ClientRequestChannel, r.ClientRequestRef);
    }

    private static IReadOnlyList<OvertimeSegmentDto> ToSegments(OvertimeBreakdown b) =>
        b.Segments.Select(s => new OvertimeSegmentDto(s.Multiplier, OvertimeRules.KindName(s.Kind, s.Multiplier), s.Hours)).ToList();

    private static ContractEvent NewEvent(Guid contractId, string message, Guid userId, string actor, DateTimeOffset at) =>
        new() { Id = Guid.NewGuid(), ContractId = contractId, Type = ContractEventType.Support, Message = message.Length > 1000 ? message[..1000] : message, ActorUserId = userId, ActorName = actor, At = at };

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(SlaCalculator.Offset).DateTime);

    private Task<WorkCalendar> CalendarAsync(DateOnly center, CancellationToken ct) => holidays.LoadCalendarAsync(center.AddDays(-30), center.AddDays(400), ct);
}
