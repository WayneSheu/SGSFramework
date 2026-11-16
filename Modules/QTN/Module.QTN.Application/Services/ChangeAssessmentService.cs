using Microsoft.EntityFrameworkCore;
using SGS.Modules.QTN.Application.Contracts;
using SGS.Modules.QTN.Application.Rules;
using SGS.Modules.QTN.Infrastructure.Dbcontexts;
using SGS.Modules.QTN.Infrastructure.Entities;
using SGSFramework.Core.Calendars;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.QTN.Application.Services;

/// <summary>
/// 專案變更管理（SOP 4.3 / 附錄 B）：收到請求後 3 個工作日內，PM 會同 Tech Lead 產出《變更影響評估單》，量化新增開發、既有重工、
/// 回歸測試與專案管理工時及上線日順延；系統以評估工時衍生 vX.Y-CR 報價版本，經內部毛利審批並獲客戶書面簽署後方得排入開發。
/// </summary>
public sealed class ChangeAssessmentService(
    QTNDbContext db,
    QtnAccess access,
    QuoteService quotes,
    QtnMailNotifier notifier,
    IWorkCalendarProvider calendar,
    TimeProvider clock)
{
    public const int AssessmentWorkdays = 3;
    private const int MaxPageSize = 200;

    public async Task<PagedResult<ChangeAssessmentDto>> SearchAsync(QtnCaller caller, ChangeAssessmentSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var filter = request.Filter ?? new ChangeAssessmentFilter();
        var today = Workdays.TaipeiToday(clock.GetUtcNow());

        var visible = (await access.VisibleOpportunitiesAsync(caller.UserId, ct).ConfigureAwait(false)).Select(o => o.Id);
        var query = db.ChangeAssessments.AsNoTracking().Where(a => visible.Contains(a.OpportunityId));
        if (filter.Status is { Count: > 0 } statuses) query = query.Where(a => statuses.Contains(a.Status));
        if (filter.OpportunityId is { } oid) query = query.Where(a => a.OpportunityId == oid);
        if (filter.OverdueOnly) query = query.Where(a => a.Status == ChangeAssessmentStatus.Assessing && a.AssessmentDueDate < today);
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(a => a.AssessmentNo.Contains(term) || a.Title.Contains(term));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var rows = await query.OrderByDescending(a => a.CreatedAtUtc).ThenBy(a => a.Id).Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
        var dtos = new List<ChangeAssessmentDto>(rows.Count);
        foreach (var a in rows) dtos.Add(await ToDtoAsync(a, caller, ct).ConfigureAwait(false));
        return new PagedResult<ChangeAssessmentDto>(dtos, total, pageIndex, pageSize);
    }

    public async Task<QtnResult<ChangeAssessmentDto>> GetAsync(QtnCaller caller, Guid id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var loaded = await LoadAsync(caller, id, track: false, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        return QtnResult<ChangeAssessmentDto>.Success(await ToDtoAsync(loaded.Value!.Assessment, caller, ct).ConfigureAwait(false));
    }

    public async Task<QtnResult<ChangeAssessmentDto>> CreateAsync(QtnCaller caller, CreateChangeAssessmentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        var invalid = ValidateText(request.Title, request.OriginalSpec, request.ChangedSpec);
        if (invalid is not null) return QtnError.Validation(invalid);
        if (!Enum.IsDefined(request.Requester)) return QtnError.Validation("變更提出方不正確。");
        if (string.IsNullOrWhiteSpace(request.RequesterName) || request.RequesterName.Length > 100) return QtnError.Validation("提出人必填，最多 100 字。");

        var today = Workdays.TaipeiToday(clock.GetUtcNow());
        if (request.ReceivedDate > today) return QtnError.Validation("收到請求日不可晚於今日。");

        var b = await db.QuoteVersions.AsNoTracking().Include(q => q.Opportunity!).ThenInclude(o => o.Members)
            .FirstOrDefaultAsync(q => q.Id == request.BaseQuoteVersionId, ct).ConfigureAwait(false);
        if (b is null || !await access.CanViewAsync(b.Opportunity!, caller.UserId, ct).ConfigureAwait(false)) return QtnError.NotFound("原報價不存在。");
        if (!QtnAccess.Has(b.Opportunity!, caller.UserId, OpportunityRole.Pm, OpportunityRole.TechLead, OpportunityRole.Sales))
            return QtnError.Forbidden("僅商機 PM、Tech Lead 或業務可建立變更評估單。");
        if (!QuoteWorkflow.CanCreateChangeRequest(b.Status)) return QtnError.Conflict("QTN.INVALID_TRANSITION", "僅已成交（簽約）的報價可提出 CR。");

        var existing = await db.ChangeAssessments.AsNoTracking().Where(a => a.AssessmentNo.StartsWith(b.QuoteNo + "-CR")).Select(a => a.AssessmentNo)
            .ToListAsync(ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var a = new ChangeAssessment
        {
            Id = Guid.NewGuid(), OpportunityId = b.OpportunityId, AssessmentNo = QuoteNumbering.NextAssessmentNo(b.QuoteNo, existing), BaseQuoteVersionId = b.Id,
            Requester = request.Requester, RequesterName = request.RequesterName.Trim(), ReceivedDate = request.ReceivedDate,
            AssessmentDueDate = await Workdays.AddWorkdaysAsync(calendar, request.ReceivedDate, AssessmentWorkdays, ct).ConfigureAwait(false),
            Title = request.Title.Trim(), OriginalSpec = request.OriginalSpec.Trim(), ChangedSpec = request.ChangedSpec.Trim(),
            CreatedBy = caller.Name, CreatedAtUtc = now
        };
        db.ChangeAssessments.Add(a);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return QtnResult<ChangeAssessmentDto>.Success(await ToDtoAsync(a, caller, ct).ConfigureAwait(false));
    }

    public async Task<QtnResult<ChangeAssessmentDto>> UpdateAsync(QtnCaller caller, Guid id, UpdateChangeAssessmentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        var invalid = ValidateText(request.Title, request.OriginalSpec, request.ChangedSpec);
        if (invalid is not null) return QtnError.Validation(invalid);
        if (new[] { request.NewDevDays, request.ReworkDays, request.RegressionDays, request.PmDays }.Any(d => d is < 0 or > 10_000))
            return QtnError.Validation("各類工時須介於 0–10,000 人天。");
        if (request.DelayWorkdays is < 0 or > 1000) return QtnError.Validation("順延工作日須介於 0–1000。");

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var (a, o) = loaded.Value!;
        if (!CanEdit(a, o, caller)) return QtnError.Forbidden("僅商機 PM 或 Tech Lead 可於評估階段編修評估單。");
        if (!RowVersions.Matches(a.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(a).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        a.Title = request.Title.Trim();
        a.OriginalSpec = request.OriginalSpec.Trim();
        a.ChangedSpec = request.ChangedSpec.Trim();
        a.NewDevDays = PertCalculator.Round2(request.NewDevDays);
        a.ReworkDays = PertCalculator.Round2(request.ReworkDays);
        a.RegressionDays = PertCalculator.Round2(request.RegressionDays);
        a.PmDays = PertCalculator.Round2(request.PmDays);
        a.DelayWorkdays = request.DelayWorkdays;
        a.UpdatedBy = caller.Name;
        a.UpdatedAtUtc = clock.GetUtcNow();
        return await SaveAndGetAsync(caller, id, ct).ConfigureAwait(false);
    }

    /// <summary>以評估工時衍生 CR 報價版本（新增開發＋重工以開發角色、回歸測試以 QA 角色、管理以 PM 角色計價）。</summary>
    public async Task<QtnResult<ChangeAssessmentDto>> CreateQuoteAsync(QtnCaller caller, Guid id, CreateCrQuoteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var (a, o) = loaded.Value!;
        if (!CanEdit(a, o, caller)) return QtnError.Forbidden("僅商機 PM 或 Tech Lead 可於評估階段衍生 CR 報價。");
        if (a.NewDevDays + a.ReworkDays + a.RegressionDays + a.PmDays <= 0) return QtnError.Validation("請先填寫評估工時。");
        if (!RowVersions.Matches(a.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(a).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        var lines = new List<(string, WbsPhase, Guid, decimal)>
        {
            ("新增開發工時", WbsPhase.Development, request.DevRoleId, a.NewDevDays),
            ("既有重工（Rework）工時", WbsPhase.Development, request.DevRoleId, a.ReworkDays),
            ("回歸測試工時", WbsPhase.QualityAssurance, request.QaRoleId, a.RegressionDays),
            ("專案管理工時", WbsPhase.Management, request.PmRoleId, a.PmDays)
        };

        var created = await quotes.CreateChangeRequestVersionAsync(caller, a.BaseQuoteVersionId, $"{a.AssessmentNo} {a.Title}", lines, ct).ConfigureAwait(false);
        if (created.Error is { } createError) return createError;

        a.CrQuoteVersionId = created.Value!.Id;
        a.Status = ChangeAssessmentStatus.Assessed;
        a.UpdatedBy = caller.Name;
        a.UpdatedAtUtc = clock.GetUtcNow();
        return await SaveAndGetAsync(caller, id, ct).ConfigureAwait(false);
    }

    /// <summary>客戶授權代表人書面簽署追加費用：CR 報價須已核准，轉為成交；評估單結案，可排入開發排程。</summary>
    public async Task<QtnResult<ChangeAssessmentDto>> CustomerSignAsync(QtnCaller caller, Guid id, CustomerSignChangeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Signatory) || request.Signatory.Length > 100) return QtnError.Validation("請填寫客戶授權代表人（100 字以內）。");

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var (a, o) = loaded.Value!;
        if (!QtnAccess.Has(o, caller.UserId, OpportunityRole.Sales, OpportunityRole.Pm)) return QtnError.Forbidden("僅商機業務或 PM 可登錄客戶簽署。");
        if (a.Status != ChangeAssessmentStatus.Assessed || a.CrQuoteVersionId is null) return QtnError.Conflict("QTN.INVALID_TRANSITION", "評估單尚未衍生 CR 報價。");
        if (request.SignedDate > Workdays.TaipeiToday(clock.GetUtcNow())) return QtnError.Validation("簽署日不可晚於今日。");
        if (!RowVersions.Matches(a.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(a).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        var signError = await quotes.MarkChangeRequestSignedAsync(caller, a.CrQuoteVersionId.Value, $"客戶 {request.Signatory.Trim()} 於 {request.SignedDate:yyyy-MM-dd} 書面簽署", ct)
            .ConfigureAwait(false);
        if (signError is not null) return signError;

        a.Status = ChangeAssessmentStatus.CustomerSigned;
        a.CustomerSignatory = request.Signatory.Trim();
        a.CustomerSignedDate = request.SignedDate;
        a.UpdatedBy = caller.Name;
        a.UpdatedAtUtc = clock.GetUtcNow();
        return await SaveAndGetAsync(caller, id, ct).ConfigureAwait(false);
    }

    public async Task<QtnResult<ChangeAssessmentDto>> CancelAsync(QtnCaller caller, Guid id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var (a, o) = loaded.Value!;
        if (!QtnAccess.Has(o, caller.UserId, OpportunityRole.Sales, OpportunityRole.Pm)) return QtnError.Forbidden("僅商機業務或 PM 可取消評估單。");
        if (a.Status is ChangeAssessmentStatus.CustomerSigned or ChangeAssessmentStatus.Cancelled) return QtnError.Conflict("QTN.INVALID_TRANSITION", "評估單已結案。");

        a.Status = ChangeAssessmentStatus.Cancelled;
        a.UpdatedBy = caller.Name;
        a.UpdatedAtUtc = clock.GetUtcNow();
        return await SaveAndGetAsync(caller, id, ct).ConfigureAwait(false);
    }

    /// <summary>排程：評估中且逾 3 個工作日期限者提醒商機 PM 與 Tech Lead（每日一次）。</summary>
    public async Task<ScanResultDto> ScanOverdueAsync(CancellationToken ct)
    {
        var today = Workdays.TaipeiToday(clock.GetUtcNow());
        var overdue = await db.ChangeAssessments.AsNoTracking().Where(a => a.Status == ChangeAssessmentStatus.Assessing && a.AssessmentDueDate < today)
            .ToListAsync(ct).ConfigureAwait(false);
        var notified = 0;
        foreach (var a in overdue)
        {
            var users = await db.OpportunityMembers.AsNoTracking()
                .Where(m => m.OpportunityId == a.OpportunityId && (m.Role == OpportunityRole.Pm || m.Role == OpportunityRole.TechLead))
                .Select(m => m.UserId).ToListAsync(ct).ConfigureAwait(false);
            if (await notifier.NotifyAsync(users, $"[CR 評估逾期] {a.AssessmentNo} {a.Title}",
                    [$"評估單：{a.AssessmentNo}　{a.Title}", $"收到日：{a.ReceivedDate:yyyy-MM-dd}；評估期限：{a.AssessmentDueDate:yyyy-MM-dd}（3 個工作日）", "請儘速完成《變更影響評估單》。"],
                    "QTN.CrOverdue", $"qtn:cr-overdue:{a.Id}:{today:yyyyMMdd}", ct).ConfigureAwait(false))
                notified++;
        }

        return new ScanResultDto(overdue.Count, notified, $"評估逾期 {overdue.Count} 件，已提醒 {notified} 件。");
    }

    private static bool CanEdit(ChangeAssessment a, Opportunity o, QtnCaller caller) =>
        a.Status == ChangeAssessmentStatus.Assessing && caller.CanEditEstimate && QtnAccess.Has(o, caller.UserId, OpportunityRole.Pm, OpportunityRole.TechLead);

    private static string? ValidateText(string title, string original, string changed)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) return "變更標題必填，最多 200 字。";
        if (string.IsNullOrWhiteSpace(original) || original.Length > 4000) return "原規格定義必填，最多 4000 字。";
        if (string.IsNullOrWhiteSpace(changed) || changed.Length > 4000) return "變更後需求必填，最多 4000 字。";
        return null;
    }

    private async Task<QtnResult<(ChangeAssessment Assessment, Opportunity Opportunity)>> LoadAsync(QtnCaller caller, Guid id, bool track, CancellationToken ct)
    {
        IQueryable<ChangeAssessment> query = db.ChangeAssessments;
        if (!track) query = query.AsNoTracking();
        var a = await query.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (a is null) return QtnError.NotFound("評估單不存在。");
        var o = await db.Opportunities.AsNoTracking().Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == a.OpportunityId, ct).ConfigureAwait(false);
        return o is null || !await access.CanViewAsync(o, caller.UserId, ct).ConfigureAwait(false)
            ? QtnError.NotFound("評估單不存在。")
            : QtnResult<(ChangeAssessment, Opportunity)>.Success((a, o));
    }

    private async Task<QtnResult<ChangeAssessmentDto>> SaveAndGetAsync(QtnCaller caller, Guid id, CancellationToken ct)
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

    private async Task<ChangeAssessmentDto> ToDtoAsync(ChangeAssessment a, QtnCaller caller, CancellationToken ct)
    {
        var o = await db.Opportunities.AsNoTracking().Include(x => x.Customer).Include(x => x.Members).FirstAsync(x => x.Id == a.OpportunityId, ct).ConfigureAwait(false);
        var versions = await db.QuoteVersions.AsNoTracking().Where(q => q.Id == a.BaseQuoteVersionId || q.Id == a.CrQuoteVersionId)
            .Select(q => new { q.Id, q.VersionLabel, q.Status, q.FinalPrice, q.FinalPriceWithTax }).ToListAsync(ct).ConfigureAwait(false);
        var b = versions.FirstOrDefault(v => v.Id == a.BaseQuoteVersionId);
        var cr = versions.FirstOrDefault(v => v.Id == a.CrQuoteVersionId);
        var today = Workdays.TaipeiToday(clock.GetUtcNow());

        return new ChangeAssessmentDto(
            a.Id, a.AssessmentNo, a.OpportunityId, o.OpportunityNo, o.Customer?.Name ?? string.Empty, a.BaseQuoteVersionId, b?.VersionLabel ?? string.Empty,
            a.CrQuoteVersionId, cr?.VersionLabel, cr?.Status, a.Requester, a.RequesterName, a.ReceivedDate, a.AssessmentDueDate,
            a.Status == ChangeAssessmentStatus.Assessing && today > a.AssessmentDueDate, a.Title, a.OriginalSpec, a.ChangedSpec,
            a.NewDevDays, a.ReworkDays, a.RegressionDays, a.PmDays, a.NewDevDays + a.ReworkDays + a.RegressionDays + a.PmDays,
            cr is null ? null : caller.Access.Price(cr.FinalPrice), cr is null ? null : caller.Access.Price(cr.FinalPriceWithTax),
            a.DelayWorkdays, a.Status, a.CustomerSignatory, a.CustomerSignedDate, CanEdit(a, o, caller), RowVersions.Format(a.RowVersion));
    }
}
