using SGSFramework.Core.Rules;

namespace SGSFramework.RuleEngine;

/// <summary>衝突合併策略（純函式，便於單元測試）。</summary>
public static class RuleCombiner
{
    /// <summary>讀取：任一 ForceDeny 即拒絕；否則取最寬鬆（Allow &gt; Mask &gt; Deny）；沒有命中為 Deny。</summary>
    public static FieldAccessLevel CombineRead(IEnumerable<FieldAccessLevel> matched)
    {
        var best = FieldAccessLevel.Deny;
        foreach (var m in matched)
        {
            if (m == FieldAccessLevel.ForceDeny) return FieldAccessLevel.Deny;
            if (m > best) best = m;
        }
        return best;
    }

    /// <summary>寫入：任一 ForceDeny 即拒絕；至少一筆 Allow 才允許。</summary>
    public static bool CombineWrite(IEnumerable<FieldAccessLevel> matched)
    {
        var allow = false;
        foreach (var m in matched)
        {
            if (m == FieldAccessLevel.ForceDeny) return false;
            if (m == FieldAccessLevel.Allow) allow = true;
        }
        return allow;
    }

    /// <summary>風控：Block 短路；否則 ManualReview &gt; Pass；沒有命中為 Pass。</summary>
    public static RiskDecision CombineRisk(IEnumerable<RiskDecision> matched)
    {
        var result = RiskDecision.Pass;
        foreach (var m in matched)
        {
            if (m == RiskDecision.Block) return RiskDecision.Block;
            if (m > result) result = m;
        }
        return result;
    }

    /// <summary>計價：BestPrice 取最小倍率；Stack 連乘。倍率下限為 0。沒有命中為 1。</summary>
    public static decimal CombinePricing(IReadOnlyList<decimal> multipliers, PricingMergeStrategy strategy)
    {
        if (multipliers.Count == 0) return 1m;
        var v = strategy == PricingMergeStrategy.BestPrice
            ? multipliers.Min()
            : multipliers.Aggregate(1m, (a, b) => a * b);
        return Math.Max(0m, v);
    }

    /// <summary>表單：顯示需全部規則同意；必填任一即成立；訊息彙整。</summary>
    public static FormFieldState CombineForm(string field, IEnumerable<(bool? Visible, bool? Required, string? Message)> matched)
    {
        bool visible = true, required = false;
        var messages = new List<string>();
        foreach (var (v, r, m) in matched)
        {
            if (v == false) visible = false;
            if (r == true) required = true;
            if (!string.IsNullOrWhiteSpace(m)) messages.Add(m!);
        }
        return new FormFieldState(field, visible, required, messages);
    }
}
