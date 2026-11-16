using SGS.Modules.QTN.Infrastructure.Entities;

namespace SGS.Modules.QTN.Application.Rules;

/// <summary>
/// 風險加權工時（SOP 4.2）：PERT 三點估算 E＝(O＋4M＋P)／6，最終報價工時＝E × K技術 × K需求。人天四捨五入至小數 2 位。
/// </summary>
public static class PertCalculator
{
    /// <summary>技術新穎度係數：成熟技術棧 1.0；文件不全之第三方 API／舊系統改造 1.2；全新框架、AI 訓練、高併發微服務 1.5。</summary>
    public static readonly IReadOnlyList<decimal> TechFactors = [1.0m, 1.2m, 1.5m];

    /// <summary>需求明確度係數：完整 UI 與規格書 1.0；僅初步業務流程圖 1.3；僅概念性想法 1.6。</summary>
    public static readonly IReadOnlyList<decimal> RequirementFactors = [1.0m, 1.3m, 1.6m];

    public const decimal MaxDays = 10_000m;

    public static decimal Expected(decimal optimistic, decimal mostLikely, decimal pessimistic) =>
        Round2((optimistic + 4m * mostLikely + pessimistic) / 6m);

    /// <summary>以未四捨五入的 E 計算報價人天，避免二次進位誤差。</summary>
    public static decimal Quoted(decimal optimistic, decimal mostLikely, decimal pessimistic, decimal kTech, decimal kReq) =>
        Round2((optimistic + 4m * mostLikely + pessimistic) / 6m * kTech * kReq);

    public static bool IsValidTechFactor(decimal k) => TechFactors.Contains(k);

    public static bool IsValidRequirementFactor(decimal k) => RequirementFactors.Contains(k);

    /// <summary>三點工時檢核：皆不可為負、O ≤ M ≤ P、P 不超過上限、且不可全為 0。</summary>
    public static string? Validate(decimal optimistic, decimal mostLikely, decimal pessimistic)
    {
        if (optimistic < 0 || mostLikely < 0 || pessimistic < 0) return "三點工時不可為負數。";
        if (optimistic > mostLikely || mostLikely > pessimistic) return "三點工時須符合 最樂觀 ≤ 最可能 ≤ 最悲觀。";
        if (pessimistic > MaxDays) return $"單一配置工時不可超過 {MaxDays:N0} 人天。";
        if (pessimistic == 0) return "三點工時不可全為 0。";
        return null;
    }

    public static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

/// <param name="Min">比例下限（0~1）。</param>
/// <param name="Max">比例上限（0~1）。</param>
public sealed record PhaseRatioDto(WbsPhase Phase, decimal Days, decimal Ratio, decimal Min, decimal Max, bool InRange);

/// <summary>
/// 標準工時結構比例（SOP 4.1）：除核心開發外須強制計入支援性階段工時。固定總價與混合計價：分析、測試、管理任一階段為 0 即不得送審；
/// 比例超出基準區間僅提示（由 Tech Lead 於技術複核說明）。T&amp;M 產能包與月費維運以團隊產能計價，不套用比例檢核。
/// </summary>
public static class PhaseRatioRules
{
    public static readonly IReadOnlyDictionary<WbsPhase, (decimal Min, decimal Max)> Baseline = new Dictionary<WbsPhase, (decimal, decimal)>
    {
        [WbsPhase.Analysis] = (0.15m, 0.20m),
        [WbsPhase.Development] = (0.50m, 0.60m),
        [WbsPhase.QualityAssurance] = (0.15m, 0.20m),
        [WbsPhase.Management] = (0.10m, 0.15m)
    };

    public static bool Applies(PricingModel model) => model is PricingModel.FixedPrice or PricingModel.Hybrid;

    public static IReadOnlyList<PhaseRatioDto> Evaluate(IEnumerable<(WbsPhase Phase, decimal Days)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var byPhase = lines.GroupBy(l => l.Phase).ToDictionary(g => g.Key, g => g.Sum(x => x.Days));
        var total = byPhase.Values.Sum();

        return Baseline.Select(b =>
        {
            var days = byPhase.GetValueOrDefault(b.Key);
            var ratio = total == 0 ? 0 : Math.Round(days / total, 4, MidpointRounding.AwayFromZero);
            return new PhaseRatioDto(b.Key, days, ratio, b.Value.Min, b.Value.Max, ratio >= b.Value.Min && ratio <= b.Value.Max);
        }).ToList();
    }

    /// <summary>送審阻擋：適用比例檢核的計價模式，支援性階段（分析、測試、管理）不可為 0 人天。</summary>
    public static string? BlockingError(PricingModel model, IReadOnlyList<PhaseRatioDto> ratios)
    {
        ArgumentNullException.ThrowIfNull(ratios);
        if (!Applies(model)) return null;

        var missing = ratios.Where(r => r.Phase != WbsPhase.Development && r.Days <= 0).Select(r => PhaseName(r.Phase)).ToList();
        return missing.Count == 0 ? null : $"固定總價／混合計價須強制計入支援性階段工時，缺少：{string.Join("、", missing)}。";
    }

    public static IReadOnlyList<string> Warnings(PricingModel model, IReadOnlyList<PhaseRatioDto> ratios)
    {
        ArgumentNullException.ThrowIfNull(ratios);
        if (!Applies(model)) return [];

        return ratios.Where(r => !r.InRange && r.Days > 0)
            .Select(r => $"{PhaseName(r.Phase)}佔 {r.Ratio:P1}，超出基準 {r.Min:P0}–{r.Max:P0}。")
            .ToList();
    }

    public static string PhaseName(WbsPhase phase) => phase switch
    {
        WbsPhase.Analysis => "需求訪談與架構設計",
        WbsPhase.Development => "核心功能開發",
        WbsPhase.QualityAssurance => "品質保證與測試",
        WbsPhase.Management => "專案管理與部署",
        _ => phase.ToString()
    };
}

/// <summary>
/// 專案類型報價策略矩陣（SOP 第三章）：以「報價人天 ÷ 期望人天」作為整體風險係數，與類型建議係數及目標毛利比對，僅提示不阻擋。
/// </summary>
public static class StrategyRules
{
    public sealed record Strategy(PricingModel SuggestedModel, decimal MinFactor, decimal MaxFactor, decimal MinMargin, decimal? MaxMargin, string Defense);

    public static readonly IReadOnlyDictionary<ProjectCategory, Strategy> Matrix = new Dictionary<ProjectCategory, Strategy>
    {
        [ProjectCategory.StandardCustom] = new(PricingModel.FixedPrice, 1.0m, 1.2m, 0.35m, 0.45m,
            "採「設計與開發兩階段簽約」，UI/UX 與規格書定稿後才鎖定開發總價，嚴格執行 CR 變更收費。"),
        [ProjectCategory.Innovative] = new(PricingModel.TimeAndMaterials, 1.3m, 1.6m, 0.40m, 0.50m,
            "銷售固定團隊的雙週開發產能，不承諾固定總功能清單，客戶可隨時調整優先級。"),
        [ProjectCategory.Refactor] = new(PricingModel.Hybrid, 1.4m, 1.8m, 0.45m, null,
            "前置付費技術盤點：先簽固定總價之架構健檢與 POC 合約，釐清技術債後再報主工程價格。"),
        [ProjectCategory.Maintenance] = new(PricingModel.Retainer, 1.0m, 1.0m, 0.30m, 0.38m,
            "明確定義 SLA 回應時間與每月標準維運人天上限，當月未用完不累計，超額按約定費率加收。")
    };

    public static decimal RiskFactor(decimal expectedDays, decimal quotedDays) =>
        expectedDays <= 0 ? 1m : Math.Round(quotedDays / expectedDays, 2, MidpointRounding.AwayFromZero);

    /// <param name="marginRate">null＝呼叫者無權看毛利（不比對毛利）。</param>
    public static IReadOnlyList<string> Warnings(ProjectCategory category, PricingModel model, decimal riskFactor, decimal? marginRate)
    {
        var s = Matrix[category];
        var warnings = new List<string>();
        if (model != s.SuggestedModel)
            warnings.Add($"此專案類型建議採「{ModelName(s.SuggestedModel)}」計價。");
        if (riskFactor < s.MinFactor || riskFactor > s.MaxFactor)
            warnings.Add($"整體風險係數 {riskFactor:0.00} 不在此類型建議範圍 {s.MinFactor:0.0}–{s.MaxFactor:0.0}。");
        if (marginRate is { } m && (m < s.MinMargin || (s.MaxMargin is { } max && m > max)))
            warnings.Add($"預估毛利率 {m:P1} 不在此類型目標毛利 {s.MinMargin:P0}{(s.MaxMargin is { } mx ? $"–{mx:P0}" : " 以上")}。");
        return warnings;
    }

    public static string ModelName(PricingModel model) => model switch
    {
        PricingModel.FixedPrice => "固定總價",
        PricingModel.TimeAndMaterials => "工時與材料費（敏捷衝刺產能包）",
        PricingModel.Hybrid => "混合計價",
        PricingModel.Retainer => "固定月費維運",
        _ => model.ToString()
    };
}
