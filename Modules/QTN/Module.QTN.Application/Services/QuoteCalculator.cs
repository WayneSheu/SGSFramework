using System.Globalization;
using System.Text;
using SGS.Modules.QTN.Application.Rules;
using SGS.Modules.QTN.Infrastructure.Entities;

namespace SGS.Modules.QTN.Application.Services;

/// <summary>
/// 報價實體層級的重算：以 WBS 配置（費率快照）、直接費用、折扣與計價參數套用 <see cref="PricingCalculator"/> 與
/// <see cref="MarginLightPolicy"/>，回寫總額、毛利、燈號與付款里程碑金額。金額一律由後端計算，前端不可寫入。
/// </summary>
public static class QuoteCalculator
{
    /// <summary>期數：T&amp;M＝Sprint 數、Retainer＝月數；CR 追加版本一律為單期。</summary>
    public static int PeriodCount(QuoteVersion q)
    {
        ArgumentNullException.ThrowIfNull(q);
        if (q.IsChangeRequest) return 1;
        return q.PricingModel switch
        {
            PricingModel.TimeAndMaterials => Math.Max(1, q.SprintCount ?? 1),
            PricingModel.Retainer => Math.Max(1, q.RetainerMonths ?? 1),
            _ => 1
        };
    }

    public static IReadOnlyList<PricingLine> Lines(QuoteVersion q) =>
        q.WbsItems.SelectMany(w => w.Allocations.Select(a => new PricingLine(w.Phase, w.Stage, a.QuotedDays, a.SnapshotCostRate, a.SnapshotListRate))).ToList();

    public static decimal ExpectedDays(QuoteVersion q) =>
        PertCalculator.Round2(q.WbsItems.SelectMany(w => w.Allocations).Sum(a => a.ExpectedDays) * PeriodCount(q));

    public static PricingResult Recalculate(QuoteVersion q, bool ipBuyout)
    {
        ArgumentNullException.ThrowIfNull(q);

        var result = PricingCalculator.Calculate(new PricingInput(
            Lines(q), q.Expenses.Sum(e => e.Amount), PeriodCount(q), ipBuyout, q.IpLicenseFee, q.DiscountAmount, q.TaxRate));

        q.TotalQuotedDays = result.TotalQuotedDays;
        q.LaborCost = result.LaborCost;
        q.Overhead = result.Overhead;
        q.DirectExpense = result.DirectExpense;
        q.TotalCost = result.TotalCost;
        q.LaborListTotal = result.LaborListTotal;
        q.IpLicenseFee = result.IpLicenseFee;
        q.ListTotal = result.ListTotal;
        q.DiscountAmount = result.DiscountAmount;
        q.FinalPrice = result.FinalPrice;
        q.TaxAmount = result.TaxAmount;
        q.FinalPriceWithTax = result.FinalPriceWithTax;
        q.MarginRate = result.MarginRate;

        var evaluation = MarginLightPolicy.Evaluate(result.FinalPrice, result.MarginRate, ipBuyout, q.SpecialApproval);
        q.MarginLight = evaluation.Light;
        q.BonusFactor = evaluation.BonusFactor;
        q.RetainerMonthlyFee = q.PricingModel == PricingModel.Retainer && !q.IsChangeRequest ? result.PeriodListPrice : null;

        var ordered = q.Milestones.OrderBy(m => m.Seq).ToList();
        var amounts = MilestoneRules.Allocate(ordered.Select(m => new MilestoneSeed(m.Name, m.Percent, m.Trigger)).ToList(), q.FinalPrice, q.FinalPriceWithTax);
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Amount = amounts[i].Amount;
            ordered[i].AmountWithTax = amounts[i].AmountWithTax;
        }

        return result;
    }

    /// <summary>混合計價第一階段（前置健檢）牌價佔比，用於預設付款里程碑。</summary>
    public static decimal StageOneShare(QuoteVersion q)
    {
        var total = q.WbsItems.SelectMany(w => w.Allocations).Sum(a => a.QuotedDays * a.SnapshotListRate);
        if (total <= 0) return 0m;
        var stageOne = q.WbsItems.Where(w => w.Stage == 1).SelectMany(w => w.Allocations).Sum(a => a.QuotedDays * a.SnapshotListRate);
        return stageOne / total;
    }

    /// <summary>送審凍結內容的正規化字串（WBS、配置與快照費率、直接費用、折扣、計價參數、里程碑、條款）。</summary>
    public static string Canonical(QuoteVersion q)
    {
        ArgumentNullException.ThrowIfNull(q);
        var sb = new StringBuilder();
        sb.Append(q.Id).Append('|').Append(q.VersionLabel).Append('|').Append(q.PricingModel).Append('|').Append(q.Title).Append('\n');
        sb.Append("T|").Append(q.SprintCount).Append('|').Append(q.RetainerMonths).Append('|').Append(q.RetainerStart?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Append('|').Append(N(q.RetainerMonthlyDaysCap)).Append('|').Append(N(q.OverageDailyRate)).Append('|').Append(N(q.OffHoursMultiplier))
            .Append('|').Append(q.DurationWeeks).Append('|').Append(q.ValidDays).Append('|').Append(QuoteHash.Num(q.TaxRate)).Append('\n');
        sb.Append("P|").Append(QuoteHash.Num(q.DiscountAmount)).Append('|').Append(QuoteHash.Num(q.IpLicenseFee)).Append('|').Append(q.SpecialApproval).Append('|')
            .Append(QuoteHash.Num(q.FinalPrice)).Append('\n');

        foreach (var w in q.WbsItems.OrderBy(w => w.WbsCode, StringComparer.Ordinal))
        {
            sb.Append("W|").Append(w.WbsCode).Append('|').Append(w.Name).Append('|').Append(w.Deliverable).Append('|').Append(w.Phase).Append('|').Append(w.Stage)
                .Append('|').Append(QuoteHash.Num(w.KTech)).Append('|').Append(QuoteHash.Num(w.KReq)).Append('\n');
            foreach (var a in w.Allocations.OrderBy(a => a.RateRoleId))
                sb.Append("A|").Append(a.RateRoleId).Append('|').Append(QuoteHash.Num(a.Optimistic)).Append('|').Append(QuoteHash.Num(a.MostLikely)).Append('|')
                    .Append(QuoteHash.Num(a.Pessimistic)).Append('|').Append(QuoteHash.Num(a.QuotedDays)).Append('|').Append(QuoteHash.Num(a.SnapshotCostRate))
                    .Append('|').Append(QuoteHash.Num(a.SnapshotListRate)).Append('\n');
        }

        foreach (var e in q.Expenses.OrderBy(e => e.Category).ThenBy(e => e.Description, StringComparer.Ordinal).ThenBy(e => e.Amount))
            sb.Append("E|").Append(e.Category).Append('|').Append(e.Description).Append('|').Append(QuoteHash.Num(e.Amount)).Append('|').Append(e.IsCapped).Append('\n');
        foreach (var m in q.Milestones.OrderBy(m => m.Seq))
            sb.Append("M|").Append(m.Seq).Append('|').Append(m.Name).Append('|').Append(QuoteHash.Num(m.Percent)).Append('|').Append(m.Trigger).Append('\n');
        foreach (var c in q.Clauses.OrderBy(c => c.SortOrder))
            sb.Append("C|").Append(c.Code).Append('|').Append(c.Title).Append('|').Append(c.Body).Append('\n');
        foreach (var s in q.SlaLevels.OrderBy(s => s.Level, StringComparer.Ordinal))
            sb.Append("S|").Append(s.Level).Append('|').Append(s.Definition).Append('|').Append(s.ResponseTime).Append('|').Append(s.ResolveTarget).Append('\n');

        return sb.ToString();

        static string N(decimal? v) => v is { } d ? QuoteHash.Num(d) : string.Empty;
    }

    public static string ComputeHash(QuoteVersion q) => QuoteHash.Compute(Canonical(q));

    /// <summary>依樹狀位置產生 WBS 編號：第一層 1.0、2.0；第二層 1.1、1.2；更深層 1.1.1。</summary>
    public static void AssignWbsCodes(IReadOnlyList<QuoteWbsItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var children = items.ToLookup(i => i.ParentId);

        void Walk(Guid? parentId, string? parentPath)
        {
            var n = 0;
            foreach (var item in children[parentId].OrderBy(i => i.SortOrder))
            {
                n++;
                var path = parentPath is null ? n.ToString(CultureInfo.InvariantCulture) : $"{parentPath}.{n}";
                item.WbsCode = parentPath is null ? $"{path}.0" : path;
                Walk(item.Id, path);
            }
        }

        Walk(null, null);
    }

    public static void ApplyPert(QuoteWbsItem item, WbsRoleAllocation a)
    {
        a.ExpectedDays = PertCalculator.Expected(a.Optimistic, a.MostLikely, a.Pessimistic);
        a.QuotedDays = PertCalculator.Quoted(a.Optimistic, a.MostLikely, a.Pessimistic, item.KTech, item.KReq);
    }

    public static string FormatMoney(decimal v) => v.ToString("N0", CultureInfo.InvariantCulture);
}
