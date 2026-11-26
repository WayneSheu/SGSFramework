using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGSFramework.Core.Rules;

namespace SGSFramework.RuleEngine;

/// <summary>M1 內建的示範目標實體（僅供管理後台試跑與驗證，不綁真實資料表）。</summary>
public sealed class SampleRuleTarget
{
    public string CustomerType { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TotalAmount { get; set; }
    public int AccountAgeDays { get; set; }
    public string DepartmentPath { get; set; } = string.Empty;
}

public sealed class RuleTargetCatalog : IRuleTargetCatalog
{
    private readonly Dictionary<string, (Type Type, string Description)> _targets = new(StringComparer.OrdinalIgnoreCase);

    public RuleTargetCatalog(IEnumerable<IRuleTargetRegistration>? registrations = null)
    {
        foreach (var r in registrations ?? []) Register(r.Type, r.Description);
    }

    public RuleTargetCatalog Register(Type type, string? description = null)
    {
        lock (_targets) _targets[type.Name] = (type, description ?? type.Name);
        return this;
    }

    public IReadOnlyList<RuleTargetInfo> GetTargets()
    {
        lock (_targets)
            return _targets.Values.Select(v => new RuleTargetInfo(v.Type.Name, v.Description,
                v.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList())).ToList();
    }

    public Type? Resolve(string name)
    {
        lock (_targets) return _targets.TryGetValue(name, out var v) ? v.Type : null;
    }
}

public sealed class RuleTester(IRuleTargetCatalog catalog) : IRuleTester
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public RuleTestOutcome Test(RuleSnapshot rule, RuleUserContext user, string targetJson)
    {
        var type = catalog.Resolve(rule.TargetEntity);
        if (type is null) return new(false, false, null, $"目標實體未登錄：{rule.TargetEntity}");
        try
        {
            var target = JsonSerializer.Deserialize(string.IsNullOrWhiteSpace(targetJson) ? "{}" : targetJson, type, Json)!;
            var m = typeof(RuleTester).GetMethod(nameof(Run), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(type);
            return (RuleTestOutcome)m.Invoke(null, [rule, user, target])!;
        }
        catch (TargetInvocationException ex) { return new(false, false, null, ex.InnerException?.Message ?? ex.Message); }
        catch (Exception ex) { return new(false, false, null, ex.Message); }
    }

    private static RuleTestOutcome Run<T>(RuleSnapshot rule, RuleUserContext user, T target)
    {
        if (rule.Kind == RuleKind.Row)
        {
            // Row 規則以 EF 述詞評估：對單筆資料編譯後求值
            var pred = ExpressionCompiler.BuildRowPredicate<T>(rule.ConditionExpr, user).Compile();
            return new(true, pred(target), null, null);
        }
        var matched = ExpressionCompiler.CompileCondition<T>(rule.Kind, rule.ConditionExpr)(user, target);
        string? detail = null;
        if (matched && rule.Kind == RuleKind.Pricing && !string.IsNullOrWhiteSpace(rule.ActionExpr))
            detail = $"折扣倍率 = {ExpressionCompiler.CompileDecimal<T>(rule.ActionExpr)(user, target)}";
        return new(true, matched, detail, null);
    }
}

public static class RuleTargetServiceExtensions
{
    /// <summary>登錄可被規則引用的目標實體型別（白名單）。須在 AddDynamicRuleEngine 之後或之前呼叫皆可。</summary>
    public static IServiceCollection AddRuleTarget<T>(this IServiceCollection services, string? description = null)
    {
        services.AddSingleton<IRuleTargetRegistration>(new RuleTargetRegistration(typeof(T), description));
        return services;
    }
}

public interface IRuleTargetRegistration { Type Type { get; } string? Description { get; } }
internal sealed record RuleTargetRegistration(Type Type, string? Description) : IRuleTargetRegistration;
