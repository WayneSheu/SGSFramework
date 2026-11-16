using Microsoft.EntityFrameworkCore;
using SGS.Modules.QTN.Application.Contracts;
using SGS.Modules.QTN.Application.Rules;
using SGS.Modules.QTN.Infrastructure.Dbcontexts;
using SGS.Modules.QTN.Infrastructure.Entities;

namespace SGS.Modules.QTN.Application.Services;

/// <summary>報價儀表板：漏斗、燈號分佈、得標率、待辦（待我審批、CR 評估逾期、復盤待辦、7 日內到期）。金額與毛利依欄位級權限投影。</summary>
public sealed class QuoteDashboardService(QTNDbContext db, QtnAccess access, QuoteService quotes, TimeProvider clock)
{
    public async Task<QuoteDashboardDto> GetAsync(QtnCaller caller, DashboardRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var now = clock.GetUtcNow();
        var today = Workdays.TaipeiToday(now);
        var year = request?.Year is { } y and >= 2000 and <= 2100 ? y : today.Year;
        var from = new DateTimeOffset(year, 1, 1, 0, 0, 0, Workdays.TaipeiOffset);
        var to = from.AddYears(1);

        var visible = await access.VisibleOpportunitiesAsync(caller.UserId, ct).ConfigureAwait(false);
        var visibleIds = visible.Select(o => o.Id);
        var openOpportunities = await visible.CountAsync(o => o.Status == OpportunityStatus.Open, ct).ConfigureAwait(false);

        // 各商機（不含 CR）最新版本
        var latest = await db.QuoteVersions.AsNoTracking()
            .Where(q => visibleIds.Contains(q.OpportunityId) && !q.IsChangeRequest && q.CreatedAtUtc >= from && q.CreatedAtUtc < to)
            .Where(q => !db.QuoteVersions.Any(o => o.OpportunityId == q.OpportunityId && !o.IsChangeRequest && o.Minor > q.Minor))
            .Select(q => new { q.Status, q.MarginLight, q.PricingModel, q.FinalPrice, q.MarginRate, q.ValidUntil })
            .ToListAsync(ct).ConfigureAwait(false);

        var price = caller.Access.SeesPrice();
        var won = latest.Count(q => q.Status == QuoteStatus.Won);
        var lost = latest.Count(q => q.Status == QuoteStatus.Lost);
        var pending = await quotes.SearchAsync(caller, new QuoteSearchRequest { PageSize = 1, Filter = new QuoteFilter { PendingMyApproval = true } }, ct).ConfigureAwait(false);

        var assessments = await db.ChangeAssessments.AsNoTracking()
            .CountAsync(a => visibleIds.Contains(a.OpportunityId) && a.Status == ChangeAssessmentStatus.Assessing && a.AssessmentDueDate < today, ct).ConfigureAwait(false);
        var quoteIds = db.QuoteVersions.Where(q => visibleIds.Contains(q.OpportunityId)).Select(q => q.Id);
        var reviews = await db.VarianceReviews.AsNoTracking().CountAsync(r => quoteIds.Contains(r.QuoteVersionId) && r.Status != VarianceReviewStatus.Closed, ct)
            .ConfigureAwait(false);

        return new QuoteDashboardDto(
            year, openOpportunities, pending.TotalCount,
            latest.Count(q => q.Status == QuoteStatus.PendingApproval),
            latest.Count(q => q.Status == QuoteStatus.Issued),
            won, lost,
            won + lost == 0 ? null : Math.Round((decimal)won / (won + lost), 4),
            price ? latest.Where(q => q.Status is QuoteStatus.Approved or QuoteStatus.Issued or QuoteStatus.PendingApproval).Sum(q => q.FinalPrice) : null,
            price ? latest.Where(q => q.Status == QuoteStatus.Won).Sum(q => q.FinalPrice) : null,
            caller.Access.SeesCost() && latest.Any(q => q.FinalPrice > 0)
                ? Math.Round(latest.Where(q => q.FinalPrice > 0).Sum(q => q.MarginRate * q.FinalPrice) / latest.Where(q => q.FinalPrice > 0).Sum(q => q.FinalPrice), 4)
                : null,
            latest.GroupBy(q => q.Status).Select(g => new CountByDto(g.Key.ToString(), g.Count(), price ? g.Sum(x => x.FinalPrice) : null)).ToList(),
            price ? latest.GroupBy(q => q.MarginLight).Select(g => new CountByDto(g.Key.ToString(), g.Count(), g.Sum(x => x.FinalPrice))).ToList() : [],
            latest.GroupBy(q => q.PricingModel).Select(g => new CountByDto(g.Key.ToString(), g.Count(), price ? g.Sum(x => x.FinalPrice) : null)).ToList(),
            assessments, reviews,
            latest.Count(q => q.Status == QuoteStatus.Issued && q.ValidUntil is { } v && v >= today && v <= today.AddDays(7)));
    }
}
