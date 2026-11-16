using Microsoft.EntityFrameworkCore;
using SGS.Modules.QTN.Application.Contracts;
using SGS.Modules.QTN.Application.Rules;
using SGS.Modules.QTN.Infrastructure.Dbcontexts;
using SGS.Modules.QTN.Infrastructure.Entities;
using SGSFramework.Core.Calendars;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.QTN.Application.Services;

/// <summary>
/// 結案復盤與數據閉環（SOP 第七章）：結案後 14 個工作日內由 PM 產出《預估與實際工時對比報告》；單一工作項偏差超過 ±15% 者須記錄原因，
/// 並於復盤後 7 日內校準《標準 WBS 工時庫》（以實際人天為新的最可能值，O／P 依比例縮放，基準版本 +1）。
/// </summary>
public sealed class VarianceReviewService(
    QTNDbContext db,
    QtnAccess access,
    QtnMailNotifier notifier,
    IWorkCalendarProvider calendar,
    TimeProvider clock)
{
    private const int MaxPageSize = 200;

    public async Task<PagedResult<VarianceReviewDto>> SearchAsync(QtnCaller caller, VarianceReviewSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var visible = (await access.VisibleOpportunitiesAsync(caller.UserId, ct).ConfigureAwait(false)).Select(o => o.Id);
        var quoteIds = db.QuoteVersions.Where(q => visible.Contains(q.OpportunityId)).Select(q => q.Id);
        var query = db.VarianceReviews.AsNoTracking().Where(r => quoteIds.Contains(r.QuoteVersionId));
        if (request.Filter?.Status is { Count: > 0 } statuses) query = query.Where(r => statuses.Contains(r.Status));

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var rows = await query.OrderBy(r => r.Status).ThenBy(r => r.ReportDueDate).ThenBy(r => r.Id).Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Include(r => r.Items).ToListAsync(ct).ConfigureAwait(false);
        var dtos = new List<VarianceReviewDto>(rows.Count);
        foreach (var r in rows) dtos.Add(await ToDtoAsync(r, caller, ct).ConfigureAwait(false));
        return new PagedResult<VarianceReviewDto>(dtos, total, pageIndex, pageSize);
    }

    public async Task<QtnResult<VarianceReviewDto>> GetAsync(QtnCaller caller, Guid id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var loaded = await LoadAsync(caller, id, track: false, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        return QtnResult<VarianceReviewDto>.Success(await ToDtoAsync(loaded.Value!.Review, caller, ct).ConfigureAwait(false));
    }

    /// <summary>建立復盤：以已成交主報價的 WBS 最下層工作項（含工時）為比對清單。</summary>
    public async Task<QtnResult<VarianceReviewDto>> CreateAsync(QtnCaller caller, CreateVarianceReviewRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var q = await db.QuoteVersions.AsNoTracking().Include(x => x.Opportunity!).ThenInclude(o => o.Members)
            .Include(x => x.WbsItems).ThenInclude(w => w.Allocations).AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == request.QuoteVersionId, ct).ConfigureAwait(false);
        if (q is null || !await access.CanViewAsync(q.Opportunity!, caller.UserId, ct).ConfigureAwait(false)) return QtnError.NotFound("報價不存在。");
        if (!QtnAccess.Has(q.Opportunity!, caller.UserId, OpportunityRole.Pm)) return QtnError.Forbidden("僅商機 PM 可建立結案復盤。");
        if (q.Status != QuoteStatus.Won) return QtnError.Conflict("QTN.INVALID_TRANSITION", "僅已成交的報價可進行結案復盤。");
        if (request.ClosedDate > Workdays.TaipeiToday(clock.GetUtcNow())) return QtnError.Validation("結案日不可晚於今日。");
        if (await db.VarianceReviews.AnyAsync(r => r.QuoteVersionId == q.Id, ct).ConfigureAwait(false))
            return QtnError.Conflict("QTN.DUPLICATE", "此報價已建立結案復盤。");

        var now = clock.GetUtcNow();
        var review = new VarianceReview
        {
            Id = Guid.NewGuid(), QuoteVersionId = q.Id, ClosedDate = request.ClosedDate,
            ReportDueDate = await Workdays.AddWorkdaysAsync(calendar, request.ClosedDate, VarianceRules.ReportWorkdays, ct).ConfigureAwait(false),
            CreatedBy = caller.Name, CreatedAtUtc = now
        };

        var periods = QuoteCalculator.PeriodCount(q);
        foreach (var w in q.WbsItems.Where(w => w.Allocations.Count > 0).OrderBy(w => w.WbsCode, QuoteService.WbsCodeComparer.Instance))
            review.Items.Add(new VarianceItem
            {
                Id = Guid.NewGuid(), WbsItemId = w.Id, TemplateItemId = w.TemplateItemId, WbsCode = w.WbsCode, Name = w.Name,
                QuotedDays = PertCalculator.Round2(w.Allocations.Sum(a => a.QuotedDays) * periods), CreatedBy = caller.Name, CreatedAtUtc = now
            });

        db.VarianceReviews.Add(review);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return await GetAsync(caller, review.Id, ct).ConfigureAwait(false);
    }

    public async Task<QtnResult<VarianceReviewDto>> UpdateAsync(QtnCaller caller, Guid id, UpdateVarianceReviewRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        if (request.RetrospectiveNotes is { Length: > 4000 }) return QtnError.Validation("復盤結論最多 4000 字。");

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var (r, o) = loaded.Value!;
        if (!CanEdit(r, o, caller)) return QtnError.Forbidden("僅商機 PM 或 Tech Lead 可於復盤結案前編修。");
        if (!RowVersions.Matches(r.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(r).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        var now = clock.GetUtcNow();
        foreach (var input in request.Items ?? [])
        {
            var item = r.Items.FirstOrDefault(i => i.Id == input.Id);
            if (item is null) return QtnError.Validation("復盤項目不存在。");
            if (input.ActualDays is < 0 or > 100_000) return QtnError.Validation("實際人天不正確。");
            if (input.RootCause is { Length: > 1000 }) return QtnError.Validation("偏差原因最多 1000 字。");
            item.ActualDays = input.ActualDays is { } d ? PertCalculator.Round2(d) : null;
            item.VarianceRate = VarianceRules.Rate(item.QuotedDays, item.ActualDays);
            item.ExceedsThreshold = VarianceRules.Exceeds(item.VarianceRate);
            item.RootCause = MasterDataService.Clean(input.RootCause);
            item.UpdatedBy = caller.Name;
            item.UpdatedAtUtc = now;
        }

        r.RetrospectiveNotes = MasterDataService.Clean(request.RetrospectiveNotes);
        var allFilled = r.Items.All(i => i.ActualDays is not null);
        if (allFilled)
        {
            var exceed = r.Items.Where(i => i.ExceedsThreshold).ToList();
            if (exceed.Count == 0) r.Status = VarianceReviewStatus.Closed;
            else
            {
                r.Status = VarianceReviewStatus.PendingRetrospective;
                r.CalibrationDueDate ??= Workdays.TaipeiToday(now).AddDays(VarianceRules.CalibrationDays);
                if (exceed.All(i => i.Calibrated || (i.TemplateItemId is null && i.RootCause is not null)) && r.RetrospectiveNotes is not null)
                    r.Status = VarianceReviewStatus.Closed;
            }
        }

        r.UpdatedBy = caller.Name;
        r.UpdatedAtUtc = now;
        return await SaveAndGetAsync(caller, id, ct).ConfigureAwait(false);
    }

    /// <summary>校準標準工時庫：超標且來自標準模組的項目，以實際人天更新範本三點工時（基準版本 +1）。</summary>
    public async Task<QtnResult<VarianceReviewDto>> CalibrateAsync(QtnCaller caller, Guid id, CalibrateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var loaded = await LoadAsync(caller, id, track: true, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var (r, o) = loaded.Value!;
        if (!QtnAccess.Has(o, caller.UserId, OpportunityRole.TechLead) || !caller.CanEditEstimate) return QtnError.Forbidden("僅商機 Tech Lead 可校準標準工時庫。");
        if (r.Status != VarianceReviewStatus.PendingRetrospective) return QtnError.Conflict("QTN.INVALID_TRANSITION", "須先填妥實際工時且有超標項目才能校準。");
        if (string.IsNullOrWhiteSpace(r.RetrospectiveNotes)) return QtnError.Validation("請先記錄復盤會議結論。");
        if (!RowVersions.Matches(r.RowVersion, request.RowVersion)) return QtnError.Concurrency();
        db.Entry(r).Property(x => x.RowVersion).OriginalValue = RowVersions.Parse(request.RowVersion);

        var targets = r.Items.Where(i => (request.ItemIds ?? []).Contains(i.Id)).ToList();
        if (targets.Count == 0) return QtnError.Validation("請選擇要校準的項目。");
        if (targets.Any(i => !i.ExceedsThreshold || i.TemplateItemId is null || i.ActualDays is null || string.IsNullOrWhiteSpace(i.RootCause)))
            return QtnError.Validation("僅偏差超過 ±15%、來自標準模組且已記錄原因的項目可校準。");

        var now = clock.GetUtcNow();
        var templateIds = targets.Select(i => i.TemplateItemId!.Value).Distinct().ToList();
        var templateItems = await db.WbsTemplateItems.Where(t => templateIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct).ConfigureAwait(false);
        foreach (var item in targets)
        {
            if (!templateItems.TryGetValue(item.TemplateItemId!.Value, out var t)) continue;
            var (no, nm, np) = VarianceRules.Calibrate(t.Optimistic, t.MostLikely, t.Pessimistic, item.ActualDays!.Value);
            t.Optimistic = no;
            t.MostLikely = nm;
            t.Pessimistic = np;
            t.BaselineVersion++;
            t.LastCalibratedAt = now;
            t.UpdatedBy = caller.Name;
            t.UpdatedAtUtc = now;
            item.Calibrated = true;
            item.UpdatedBy = caller.Name;
            item.UpdatedAtUtc = now;
        }

        if (r.Items.Where(i => i.ExceedsThreshold).All(i => i.Calibrated || (i.TemplateItemId is null && i.RootCause is not null)))
            r.Status = VarianceReviewStatus.Closed;
        r.UpdatedBy = caller.Name;
        r.UpdatedAtUtc = now;
        return await SaveAndGetAsync(caller, id, ct).ConfigureAwait(false);
    }

    /// <summary>排程：報告逾期（14 個工作日）或校準逾期（7 日）提醒 PM／Tech Lead（每日一次）。</summary>
    public async Task<ScanResultDto> ScanDueAsync(CancellationToken ct)
    {
        var today = Workdays.TaipeiToday(clock.GetUtcNow());
        var due = await db.VarianceReviews.AsNoTracking()
            .Where(r => (r.Status == VarianceReviewStatus.Open && r.ReportDueDate < today)
                        || (r.Status == VarianceReviewStatus.PendingRetrospective && r.CalibrationDueDate != null && r.CalibrationDueDate < today))
            .ToListAsync(ct).ConfigureAwait(false);

        var notified = 0;
        foreach (var r in due)
        {
            var q = await db.QuoteVersions.AsNoTracking().Include(x => x.Opportunity!).ThenInclude(o => o.Members).FirstOrDefaultAsync(x => x.Id == r.QuoteVersionId, ct)
                .ConfigureAwait(false);
            if (q is null) continue;
            var users = q.Opportunity!.Members.Where(m => m.Role is OpportunityRole.Pm or OpportunityRole.TechLead).Select(m => m.UserId);
            var what = r.Status == VarianceReviewStatus.Open ? $"《預估與實際工時對比報告》已逾期限 {r.ReportDueDate:yyyy-MM-dd}" : $"標準工時庫校準已逾期限 {r.CalibrationDueDate:yyyy-MM-dd}";
            if (await notifier.NotifyAsync(users, $"[結案復盤] {q.QuoteNo} {what}", [$"報價：{q.QuoteNo} {q.VersionLabel}　{q.Title}", what],
                    "QTN.VarianceDue", $"qtn:variance:{r.Id}:{r.Status}:{today:yyyyMMdd}", ct).ConfigureAwait(false))
                notified++;
        }

        return new ScanResultDto(due.Count, notified, $"復盤逾期 {due.Count} 件，已提醒 {notified} 件。");
    }

    private static bool CanEdit(VarianceReview r, Opportunity o, QtnCaller caller) =>
        r.Status != VarianceReviewStatus.Closed && QtnAccess.Has(o, caller.UserId, OpportunityRole.Pm, OpportunityRole.TechLead);

    private async Task<QtnResult<(VarianceReview Review, Opportunity Opportunity)>> LoadAsync(QtnCaller caller, Guid id, bool track, CancellationToken ct)
    {
        IQueryable<VarianceReview> query = db.VarianceReviews.Include(r => r.Items);
        if (!track) query = query.AsNoTracking();
        var r = await query.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (r is null) return QtnError.NotFound("復盤不存在。");
        var o = await db.QuoteVersions.AsNoTracking().Where(q => q.Id == r.QuoteVersionId).Select(q => q.Opportunity!).Include(x => x.Members)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return o is null || !await access.CanViewAsync(o, caller.UserId, ct).ConfigureAwait(false)
            ? QtnError.NotFound("復盤不存在。")
            : QtnResult<(VarianceReview, Opportunity)>.Success((r, o));
    }

    private async Task<QtnResult<VarianceReviewDto>> SaveAndGetAsync(QtnCaller caller, Guid id, CancellationToken ct)
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

    private async Task<VarianceReviewDto> ToDtoAsync(VarianceReview r, QtnCaller caller, CancellationToken ct)
    {
        var q = await db.QuoteVersions.AsNoTracking().Include(x => x.Opportunity!).ThenInclude(o => o.Customer).Include(x => x.Opportunity!).ThenInclude(o => o.Members)
            .FirstAsync(x => x.Id == r.QuoteVersionId, ct).ConfigureAwait(false);
        var today = Workdays.TaipeiToday(clock.GetUtcNow());
        var quoted = r.Items.Sum(i => i.QuotedDays);
        decimal? actual = r.Items.All(i => i.ActualDays is not null) && r.Items.Count > 0 ? r.Items.Sum(i => i.ActualDays!.Value) : null;

        return new VarianceReviewDto(
            r.Id, r.QuoteVersionId, q.QuoteNo, q.VersionLabel, q.Opportunity!.Title, q.Opportunity.Customer?.Name ?? string.Empty, r.ClosedDate, r.ReportDueDate,
            r.Status == VarianceReviewStatus.Open && today > r.ReportDueDate, r.Status, r.RetrospectiveNotes, r.CalibrationDueDate,
            quoted, actual, VarianceRules.Rate(quoted, actual), r.Items.Count(i => i.ExceedsThreshold),
            r.Items.OrderBy(i => i.WbsCode, QuoteService.WbsCodeComparer.Instance).Select(i => new VarianceItemDto(
                i.Id, i.WbsCode, i.Name, i.TemplateItemId, i.QuotedDays, i.ActualDays, i.VarianceRate, i.ExceedsThreshold, i.RootCause, i.Calibrated)).ToList(),
            CanEdit(r, q.Opportunity, caller), RowVersions.Format(r.RowVersion));
    }
}
