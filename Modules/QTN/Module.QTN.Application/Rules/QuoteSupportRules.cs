using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SGS.Modules.QTN.Infrastructure.Entities;
using SGSFramework.Core.Calendars;

namespace SGS.Modules.QTN.Application.Rules;

/// <summary>編號規則：商機 OP-YYYY-NNN；報價 QT-{FP|AG|HB|RT}-YYYYMMDD-NN；CR 評估單 {報價單號}-CR{NN}。</summary>
public static class QuoteNumbering
{
    public static string ModelCode(PricingModel model) => model switch
    {
        PricingModel.FixedPrice => "FP",
        PricingModel.TimeAndMaterials => "AG",
        PricingModel.Hybrid => "HB",
        PricingModel.Retainer => "RT",
        _ => "QT"
    };

    public static string QuotePrefix(PricingModel model, DateOnly date) => $"QT-{ModelCode(model)}-{date:yyyyMMdd}-";

    public static string NextQuoteNo(PricingModel model, DateOnly date, IEnumerable<string> existing) =>
        Next(QuotePrefix(model, date), existing, 2);

    public static string NextOpportunityNo(int year, IEnumerable<string> existing) => Next($"OP-{year}-", existing, 3);

    public static string NextAssessmentNo(string quoteNo, IEnumerable<string> existing) => Next($"{quoteNo}-CR", existing, 2);

    private static string Next(string prefix, IEnumerable<string> existing, int width)
    {
        ArgumentNullException.ThrowIfNull(existing);
        var max = existing
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
            .Select(n => int.TryParse(n.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
        return prefix + (max + 1).ToString(new string('0', width), CultureInfo.InvariantCulture);
    }
}

/// <summary>結案工時偏差（SOP 第七章）：偏差率＝(實際－預估)÷預估，超過 ±15% 須復盤並於 7 日內校準標準工時庫。</summary>
public static class VarianceRules
{
    public const decimal Threshold = 0.15m;
    public const int ReportWorkdays = 14;
    public const int CalibrationDays = 7;

    public static decimal? Rate(decimal quotedDays, decimal? actualDays) =>
        actualDays is not { } a || quotedDays <= 0 ? null : Math.Round((a - quotedDays) / quotedDays, 6, MidpointRounding.AwayFromZero);

    public static bool Exceeds(decimal? rate) => rate is { } r && Math.Abs(r) > Threshold;

    /// <summary>以實際平均消耗校準標準工時：最可能值＝實際人天，O／P 依原比例縮放。</summary>
    public static (decimal O, decimal M, decimal P) Calibrate(decimal o, decimal m, decimal p, decimal actualMostLikely)
    {
        if (m <= 0) return (actualMostLikely, actualMostLikely, actualMostLikely);
        var scale = actualMostLikely / m;
        return (PertCalculator.Round2(o * scale), PertCalculator.Round2(actualMostLikely), PertCalculator.Round2(p * scale));
    }
}

/// <summary>工作日運算（以 Core IWorkCalendarProvider 取得假日與補班例外；未列者週一至週五為工作日）。</summary>
public static class Workdays
{
    public static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);

    public static DateOnly TaipeiToday(DateTimeOffset utcNow) => DateOnly.FromDateTime(utcNow.ToOffset(TaipeiOffset).DateTime);

    public static bool IsWorkday(DateOnly date, IReadOnlyDictionary<DateOnly, bool> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        if (overrides.TryGetValue(date, out var isWorkday)) return isWorkday;
        return date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    /// <summary>自 <paramref name="start"/> 次日起算第 <paramref name="workdays"/> 個工作日。</summary>
    public static DateOnly AddWorkdays(DateOnly start, int workdays, IReadOnlyDictionary<DateOnly, bool> overrides)
    {
        var d = start;
        var n = 0;
        while (n < workdays)
        {
            d = d.AddDays(1);
            if (IsWorkday(d, overrides)) n++;
        }

        return d;
    }

    public static async Task<DateOnly> AddWorkdaysAsync(IWorkCalendarProvider calendar, DateOnly start, int workdays, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        var overrides = (await calendar.GetOverridesAsync(start, start.AddDays(workdays * 3 + 30), ct).ConfigureAwait(false))
            .GroupBy(o => o.Date).ToDictionary(g => g.Key, g => g.Last().IsWorkday);
        return AddWorkdays(start, workdays, overrides);
    }
}

/// <summary>送審內容雜湊：將報價可決定金額與範圍的內容正規化後計算 SHA-256（核准時比對，防止送審後被竄改）。</summary>
public static class QuoteHash
{
    public static string Compute(string canonical) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical ?? string.Empty)));

    public static string Num(decimal v) => v.ToString("0.######", CultureInfo.InvariantCulture);
}

/// <summary>欄位級權限（SOP 6.2）：後端依呼叫者權限投影 DTO，成本與毛利不出後端。</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum QuoteFieldAccess
{
    /// <summary>技術主管／PM：僅工時與係數，所有財務金額、毛利率與燈號遮罩。</summary>
    EffortOnly = 0,

    /// <summary>業務：牌價與成交價、毛利燈號；成本單價、總成本、毛利率為 null。</summary>
    PriceOnly = 1,

    /// <summary>財務／決策層：全部欄位。</summary>
    Full = 2
}

public static class FieldSecurityPolicy
{
    public const string CostPermission = "QTN.COST.READ";
    public const string PricePermission = "QTN.PRICE.READ";

    public static QuoteFieldAccess Resolve(bool canReadCost, bool canReadPrice) =>
        canReadCost ? QuoteFieldAccess.Full : canReadPrice ? QuoteFieldAccess.PriceOnly : QuoteFieldAccess.EffortOnly;

    public static bool SeesPrice(this QuoteFieldAccess access) => access >= QuoteFieldAccess.PriceOnly;

    public static bool SeesCost(this QuoteFieldAccess access) => access == QuoteFieldAccess.Full;

    public static decimal? Price(this QuoteFieldAccess access, decimal v) => access.SeesPrice() ? v : null;

    public static decimal? Cost(this QuoteFieldAccess access, decimal v) => access.SeesCost() ? v : null;
}
