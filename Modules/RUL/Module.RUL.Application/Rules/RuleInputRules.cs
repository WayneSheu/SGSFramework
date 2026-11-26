using System.Text.Json;
using SGS.Modules.RUL.Application.Contracts;
using SGSFramework.Core.Rules;

namespace SGS.Modules.RUL.Application.Rules;

/// <summary>規則內容的結構檢查（純函式；運算式白名單另由 IRuleExpressionValidator 檢查）。</summary>
public static class RuleInputRules
{
    public const int MaxNameLength = 100;
    public const int MaxExpressionLength = 500;
    public const int MaxPayloadLength = 2000;

    /// <summary>領域與規則種類必須對應。</summary>
    public static bool DomainMatchesKind(RuleDomain domain, RuleKind kind) => (domain, kind) switch
    {
        (RuleDomain.Permission, RuleKind.Row or RuleKind.Column) => true,
        (RuleDomain.DynamicForm, RuleKind.Validation) => true,
        (RuleDomain.RiskControl, RuleKind.Risk) => true,
        (RuleDomain.Pricing, RuleKind.Pricing) => true,
        _ => false
    };

    /// <summary>回傳錯誤訊息；合法時為 null。</summary>
    public static string? Validate(RuleInput r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > MaxNameLength) return $"規則名稱必填且不可超過 {MaxNameLength} 字。";
        if (string.IsNullOrWhiteSpace(r.TargetEntity)) return "目標實體必填。";
        if (string.IsNullOrWhiteSpace(r.RoleName)) return "適用角色必填（全體請用 GlobalPolicy）。";
        if (!DomainMatchesKind(r.Domain, r.Kind)) return $"領域 {r.Domain} 不支援規則種類 {r.Kind}。";
        if (string.IsNullOrWhiteSpace(r.ConditionExpr)) return "條件運算式必填（恆真請寫 true）。";
        if (r.ConditionExpr.Length > MaxExpressionLength) return $"條件運算式不可超過 {MaxExpressionLength} 字元。";
        if (r.ActionExpr is { Length: > MaxExpressionLength }) return $"動作運算式不可超過 {MaxExpressionLength} 字元。";
        if (r.OutputPayload is { Length: > MaxPayloadLength }) return $"輸出酬載不可超過 {MaxPayloadLength} 字元。";
        if (r.EffectiveFromUtc is { } f && r.EffectiveToUtc is { } t && t <= f) return "有效迄日必須晚於有效起日。";

        if (!string.IsNullOrWhiteSpace(r.OutputPayload))
        {
            try { using var _ = JsonDocument.Parse(r.OutputPayload); }
            catch (JsonException) { return "輸出酬載不是合法的 JSON。"; }
        }

        switch (r.Kind)
        {
            case RuleKind.Row:
                if (r.ReadAccess is not (FieldAccessLevel.Allow or FieldAccessLevel.ForceDeny)) return "Row 規則的讀取等級只能是 Allow 或 ForceDeny。";
                break;
            case RuleKind.Column:
                if (string.IsNullOrWhiteSpace(r.FieldName)) return "Column 規則必須指定欄位。";
                if (r.WriteAccess is FieldAccessLevel.Mask) return "寫入等級不支援 Mask。";
                break;
            case RuleKind.Validation:
                if (string.IsNullOrWhiteSpace(r.FieldName)) return "表單驗證規則必須指定欄位。";
                break;
            case RuleKind.Risk:
                if (r.Risk is null && string.IsNullOrWhiteSpace(r.OutputPayload)) return "風控規則須指定決策（Risk 或酬載 decision）。";
                break;
            case RuleKind.Pricing:
                if (string.IsNullOrWhiteSpace(r.ActionExpr)) return "計價規則必須提供折扣倍率運算式。";
                break;
        }
        return null;
    }

    /// <summary>規則目前是否在有效期間內。</summary>
    public static bool IsEffective(DateTimeOffset? from, DateTimeOffset? to, DateTimeOffset now) =>
        (from is null || from <= now) && (to is null || to > now);
}
