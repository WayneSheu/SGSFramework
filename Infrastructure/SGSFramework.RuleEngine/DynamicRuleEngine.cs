using System.Linq.Expressions;
using System.Text.Json;
using SGSFramework.Core.Rules;

namespace SGSFramework.RuleEngine;

/// <summary>通用動態規則引擎：從 <see cref="IRuleSnapshotProvider"/> 取規則，依角色篩選後評估並合併。</summary>
public sealed class DynamicRuleEngine(IRuleSnapshotProvider provider) : IRuleEngine
{
    public const string GlobalRole = "GlobalPolicy";

    private IEnumerable<RuleSnapshot> Candidates<T>(RuleUserContext user, RuleKind kind, string? field = null)
    {
        var roles = new HashSet<string>(user.Roles, StringComparer.OrdinalIgnoreCase) { GlobalRole };
        var entity = typeof(T).Name;
        return provider.GetActiveRules().Where(r =>
            r.Kind == kind &&
            r.TargetEntity.Equals(entity, StringComparison.OrdinalIgnoreCase) &&
            roles.Contains(r.RoleName) &&
            (field is null || string.Equals(r.FieldName, field, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool Matches<T>(RuleSnapshot r, RuleUserContext user, T target) =>
        ExpressionCompiler.CompileCondition<T>(r.Kind, r.ConditionExpr)(user, target);

    public FieldAccessLevel EvaluateRead<TTarget>(RuleUserContext user, TTarget target, string fieldName) =>
        RuleCombiner.CombineRead(Candidates<TTarget>(user, RuleKind.Column, fieldName)
            .Where(r => Matches(r, user, target)).Select(r => r.ReadAccess));

    public bool EvaluateWrite<TTarget>(RuleUserContext user, TTarget target, string fieldName) =>
        RuleCombiner.CombineWrite(Candidates<TTarget>(user, RuleKind.Column, fieldName)
            .Where(r => Matches(r, user, target)).Select(r => r.WriteAccess));

    public bool HasRowRules<TTarget>(RuleUserContext user) => Candidates<TTarget>(user, RuleKind.Row).Any();

    public IQueryable<TTarget> ApplyRowSecurity<TTarget>(IQueryable<TTarget> query, RuleUserContext user)
    {
        var rules = Candidates<TTarget>(user, RuleKind.Row).ToList();
        var allow = rules.Where(r => r.ReadAccess == FieldAccessLevel.Allow).ToList();
        var deny = rules.Where(r => r.ReadAccess == FieldAccessLevel.ForceDeny).ToList();
        if (allow.Count == 0) return query.Where(_ => false);

        var it = Expression.Parameter(typeof(TTarget), "e");
        Expression Merge(IEnumerable<RuleSnapshot> rs) => rs
            .Select(r => ExpressionCompiler.BuildRowPredicate<TTarget>(r.ConditionExpr, user))
            .Select(p => new ExpressionCompiler.ParamReplacer(p.Parameters[0], it).Visit(p.Body)!)
            .Aggregate(Expression.OrElse);

        Expression body = Merge(allow);
        if (deny.Count > 0) body = Expression.AndAlso(body, Expression.Not(Merge(deny)));
        return query.Where(Expression.Lambda<Func<TTarget, bool>>(body, it));
    }

    public IReadOnlyList<FormFieldState> EvaluateForm<TTarget>(RuleUserContext user, TTarget target) =>
        Candidates<TTarget>(user, RuleKind.Validation)
            .Where(r => !string.IsNullOrEmpty(r.FieldName) && Matches(r, user, target))
            .GroupBy(r => r.FieldName!, StringComparer.OrdinalIgnoreCase)
            .Select(g => RuleCombiner.CombineForm(g.Key, g.OrderByDescending(r => r.Priority).Select(r =>
            {
                var p = Payload(r);
                return (Bool(p, "visible"), Bool(p, "required"), Str(p, "errorMessage"));
            })))
            .ToList();

    public RiskVerdict EvaluateRisk<TTarget>(RuleUserContext user, TTarget target)
    {
        var hits = Candidates<TTarget>(user, RuleKind.Risk).Where(r => Matches(r, user, target))
            .Select(r => (Rule: r, Decision: r.Risk ?? ParseRisk(Str(Payload(r), "decision")))).ToList();
        var decision = RuleCombiner.CombineRisk(hits.Select(h => h.Decision));
        var reasons = hits.Where(h => h.Decision == decision)
            .Select(h => Str(Payload(h.Rule), "reason")).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToList();
        return new RiskVerdict(decision, reasons);
    }

    public PricingVerdict EvaluatePricing<TTarget>(RuleUserContext user, TTarget target, PricingMergeStrategy strategy)
    {
        var hits = Candidates<TTarget>(user, RuleKind.Pricing)
            .Where(r => !string.IsNullOrWhiteSpace(r.ActionExpr) && Matches(r, user, target))
            .OrderByDescending(r => r.Priority).ToList();
        var mult = hits.Select(r => ExpressionCompiler.CompileDecimal<TTarget>(r.ActionExpr!)(user, target)).ToList();
        return new PricingVerdict(RuleCombiner.CombinePricing(mult, strategy), hits.Select(r => r.Id).ToList());
    }

    private static RiskDecision ParseRisk(string? s) => s?.ToLowerInvariant() switch
    {
        "block" => RiskDecision.Block,
        "manualreview" => RiskDecision.ManualReview,
        _ => RiskDecision.Pass
    };

    private static JsonElement? Payload(RuleSnapshot r)
    {
        if (string.IsNullOrWhiteSpace(r.OutputPayload)) return null;
        try { return JsonDocument.Parse(r.OutputPayload).RootElement.Clone(); } catch (JsonException) { return null; }
    }

    private static bool? Bool(JsonElement? e, string name)
    {
        if (e is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(name, out var v) &&
            v.ValueKind is JsonValueKind.True or JsonValueKind.False) return v.GetBoolean();
        return null;
    }

    private static string? Str(JsonElement? e, string name)
    {
        if (e is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(name, out var v) &&
            v.ValueKind == JsonValueKind.String) return v.GetString();
        return null;
    }
}
