using SGS.Modules.QTN.Infrastructure.Entities;

namespace SGS.Modules.QTN.Application.Rules;

/// <param name="QuotedDays">單期報價人天（T&amp;M 為單一 Sprint、Retainer 為單月，其他為全案）。</param>
public sealed record PricingLine(WbsPhase Phase, int Stage, decimal QuotedDays, decimal CostRate, decimal ListRate);

/// <param name="PeriodCount">期數：T&amp;M＝Sprint 數、Retainer＝月數，其他為 1；人力與直接費用皆乘以期數。</param>
/// <param name="IpBuyout">涉及 IP 買斷：授權費不得低於牌價（人力＋直接費用）30%，不足時以下限帶入。</param>
public sealed record PricingInput(
    IReadOnlyList<PricingLine> Lines, decimal DirectExpensePerPeriod, int PeriodCount, bool IpBuyout, decimal IpLicenseFee, decimal DiscountAmount, decimal TaxRate);

public sealed record PricingResult(
    decimal TotalQuotedDays, decimal LaborCost, decimal Overhead, decimal DirectExpense, decimal TotalCost,
    decimal LaborListTotal, decimal IpLicenseFee, decimal ListTotal, decimal DiscountAmount, decimal FinalPrice,
    decimal TaxAmount, decimal FinalPriceWithTax, decimal MarginRate, decimal PeriodListPrice);

/// <summary>
/// 成本與毛利率（SOP 5.1）：
/// 專案總成本＝Σ(人天 × 成本單價)＋直接費用＋管銷分攤（直接人力成本 × 18%）；
/// 毛利率＝(成交總價未稅 − 專案總成本) ÷ 成交總價未稅。金額以新台幣元四捨五入。
/// </summary>
public static class PricingCalculator
{
    public const decimal OverheadRate = 0.18m;
    public const decimal IpFeeMinRate = 0.30m;
    public const decimal DefaultTaxRate = 0.05m;

    public static PricingResult Calculate(PricingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var periods = Math.Max(1, input.PeriodCount);
        var periodDays = input.Lines.Sum(l => l.QuotedDays);
        var periodLaborCost = input.Lines.Sum(l => Round0(l.QuotedDays * l.CostRate));
        var periodLaborList = input.Lines.Sum(l => Round0(l.QuotedDays * l.ListRate));
        var periodExpense = Round0(input.DirectExpensePerPeriod);

        var laborCost = periodLaborCost * periods;
        var overhead = Round0(laborCost * OverheadRate);
        var directExpense = periodExpense * periods;
        var totalCost = laborCost + overhead + directExpense;

        var laborList = periodLaborList * periods;
        var baseList = laborList + directExpense;
        var ipFee = input.IpBuyout ? Math.Max(Round0(input.IpLicenseFee), MinimumIpFee(baseList)) : 0m;
        var listTotal = baseList + ipFee;

        var discount = Math.Clamp(Round0(input.DiscountAmount), 0m, listTotal);
        var finalPrice = listTotal - discount;
        var taxRate = input.TaxRate < 0 ? DefaultTaxRate : input.TaxRate;
        var tax = Round0(finalPrice * taxRate);

        return new PricingResult(
            PertCalculator.Round2(periodDays * periods), laborCost, overhead, directExpense, totalCost,
            laborList, ipFee, listTotal, discount, finalPrice, tax, finalPrice + tax, MarginRate(finalPrice, totalCost),
            periodLaborList + periodExpense);
    }

    public static decimal MarginRate(decimal finalPrice, decimal totalCost) =>
        finalPrice <= 0 ? (totalCost > 0 ? -1m : 0m) : Math.Round((finalPrice - totalCost) / finalPrice, 6, MidpointRounding.AwayFromZero);

    public static decimal MinimumIpFee(decimal baseList) => Round0(baseList * IpFeeMinRate);

    public static decimal Round0(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);
}

public sealed record MilestoneSeed(string Name, decimal Percent, string Trigger);

public sealed record MilestoneAmount(int Seq, string Name, decimal Percent, decimal Amount, decimal AmountWithTax, string Trigger);

/// <summary>付款里程碑（SOP 5.3）：依比例分配未稅與含稅金額，尾差歸末期，確保合計等於成交總價。</summary>
public static class MilestoneRules
{
    public static readonly IReadOnlyList<MilestoneSeed> FixedPriceDefault =
    [
        new("第一期（簽約啟動款）", 30m, "合約簽署完成後 5 日內支付（收到款項後始得啟動專案與建置環境）"),
        new("第二期（設計規格定稿）", 20m, "《系統規格書》與《UI 設計稿》經客戶書面簽核確認"),
        new("第三期（UAT 測試交付）", 30m, "系統開發完成部署至 UAT 測試環境並交付《測試報告》"),
        new("第四期（正式上線驗收）", 20m, "正式機上線運作無誤並完成驗收（保固期自驗收日起算）")
    ];

    /// <summary>依計價模式產生預設里程碑。</summary>
    /// <param name="stageOneShare">混合計價第一階段金額佔總價比例（0~1）。</param>
    public static IReadOnlyList<MilestoneSeed> Defaults(PricingModel model, int periodCount, decimal stageOneShare = 0m)
    {
        switch (model)
        {
            case PricingModel.FixedPrice:
                return FixedPriceDefault;

            case PricingModel.TimeAndMaterials:
                return EqualSplit(Math.Max(1, periodCount), i => $"第 {i} 個 Sprint",
                    i => i == 1 ? "簽約時預付首個 Sprint 費用" : "Sprint Review 完成後 5 個工作日內結算");

            case PricingModel.Retainer:
                return EqualSplit(Math.Max(1, periodCount), i => $"第 {i} 個月維運費", _ => "按月開立發票請款");

            case PricingModel.Hybrid:
                var p1 = Math.Round(Math.Clamp(stageOneShare, 0m, 1m) * 100m, 2, MidpointRounding.AwayFromZero);
                return
                [
                    new("第一階段（架構健檢與遷移藍圖，固定總價）", p1, "簽約時一次付清"),
                    new("第二階段（核心重構與資料遷移，預估 T&M）", 100m - p1, "依雙週工時報表實報實銷")
                ];

            default:
                return FixedPriceDefault;
        }
    }

    public static string? Validate(IReadOnlyList<MilestoneSeed> seeds)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        if (seeds.Count == 0) return "至少需一期付款里程碑。";
        if (seeds.Count > 36) return "付款里程碑最多 36 期。";
        if (seeds.Any(s => s.Percent < 0 || s.Percent > 100)) return "各期比例須介於 0–100%。";
        if (seeds.Any(s => string.IsNullOrWhiteSpace(s.Name))) return "各期須有名稱。";
        return seeds.Sum(s => s.Percent) == 100m ? null : $"付款比例合計須為 100%（目前 {seeds.Sum(s => s.Percent):0.##}%）。";
    }

    public static IReadOnlyList<MilestoneAmount> Allocate(IReadOnlyList<MilestoneSeed> seeds, decimal finalPrice, decimal finalPriceWithTax)
    {
        ArgumentNullException.ThrowIfNull(seeds);

        var result = new List<MilestoneAmount>(seeds.Count);
        decimal allocated = 0, allocatedTax = 0;
        for (var i = 0; i < seeds.Count; i++)
        {
            var s = seeds[i];
            var last = i == seeds.Count - 1;
            var amount = last ? finalPrice - allocated : PricingCalculator.Round0(finalPrice * s.Percent / 100m);
            var withTax = last ? finalPriceWithTax - allocatedTax : PricingCalculator.Round0(finalPriceWithTax * s.Percent / 100m);
            allocated += amount;
            allocatedTax += withTax;
            result.Add(new MilestoneAmount(i + 1, s.Name.Trim(), s.Percent, amount, withTax, s.Trigger?.Trim() ?? string.Empty));
        }

        return result;
    }

    private static List<MilestoneSeed> EqualSplit(int n, Func<int, string> name, Func<int, string> trigger)
    {
        var each = Math.Floor(100m / n * 100m) / 100m;
        var list = new List<MilestoneSeed>(n);
        for (var i = 1; i <= n; i++)
            list.Add(new MilestoneSeed(name(i), i == n ? 100m - each * (n - 1) : each, trigger(i)));
        return list;
    }
}
