using System.Collections.Concurrent;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using SGSFramework.Core.Rules;

namespace SGSFramework.RuleEngine;

/// <summary>
/// 運算式編譯與快取。Column/Validation/Risk/Pricing 以 user、target 兩個參數撰寫；
/// Row 以 row 代表實體（如 row.Department.Path）並以 user 引用使用者屬性。所有運算式皆先經白名單檢查。
/// </summary>
public static class ExpressionCompiler
{
    private const int MaxCacheEntries = 5000;
    private static readonly ConcurrentDictionary<string, Delegate> Compiled = new();
    private static readonly ConcurrentDictionary<string, LambdaExpression> RowParsed = new();

    /// <summary>規則異動後呼叫，清除所有編譯快取。</summary>
    public static void Invalidate() { Compiled.Clear(); RowParsed.Clear(); }

    internal static LambdaExpression Parse(Type targetType, RuleKind kind, string expression, Type? returnType = null)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentException("運算式不可為空。");
        if (expression.Length > ExpressionPolicy.MaxLength) throw new ArgumentException($"運算式長度不可超過 {ExpressionPolicy.MaxLength} 字元。");
        returnType ??= typeof(bool);
        ParameterExpression[] ps = kind == RuleKind.Row
            ? [Expression.Parameter(targetType, "row"), Expression.Parameter(typeof(RuleUserContext), "user")]
            : [Expression.Parameter(typeof(RuleUserContext), "user"), Expression.Parameter(targetType, "target")];
        return DynamicExpressionParser.ParseLambda(ExpressionPolicy.Config, false, ps, returnType, expression);
    }

    internal static RuleValidationResult Check(LambdaExpression lambda, Type targetType)
    {
        var trusted = new HashSet<System.Reflection.Assembly> { targetType.Assembly };
        var v = new SafetyVisitor(trusted, targetType);
        v.Visit(lambda.Body);
        return v.Error is null ? RuleValidationResult.Ok : RuleValidationResult.Fail(v.Error);
    }

    private static LambdaExpression ParseChecked(Type t, RuleKind kind, string expr, Type? ret = null)
    {
        var lambda = Parse(t, kind, expr, ret);
        var r = Check(lambda, t);
        if (!r.IsValid) throw new InvalidOperationException(r.Error);
        return lambda;
    }

    private static void Trim<T>(ConcurrentDictionary<string, T> d) { if (d.Count > MaxCacheEntries) d.Clear(); }

    /// <summary>編譯記憶體評估用的布林條件（空白條件視為恆真）。</summary>
    public static Func<RuleUserContext, TTarget, bool> CompileCondition<TTarget>(RuleKind kind, string expr)
    {
        if (string.IsNullOrWhiteSpace(expr)) return static (_, _) => true;
        var key = $"B|{kind}|{typeof(TTarget).FullName}|{expr}";
        return (Func<RuleUserContext, TTarget, bool>)Compiled.GetOrAdd(key, _ =>
        {
            Trim(Compiled);
            var l = ParseChecked(typeof(TTarget), kind, expr);
            return Expression.Lambda<Func<RuleUserContext, TTarget, bool>>(l.Body, l.Parameters).Compile();
        });
    }

    /// <summary>編譯計價折扣倍率運算式。</summary>
    public static Func<RuleUserContext, TTarget, decimal> CompileDecimal<TTarget>(string expr)
    {
        var key = $"D|{typeof(TTarget).FullName}|{expr}";
        return (Func<RuleUserContext, TTarget, decimal>)Compiled.GetOrAdd(key, _ =>
        {
            Trim(Compiled);
            var l = ParseChecked(typeof(TTarget), RuleKind.Pricing, expr, typeof(decimal));
            return Expression.Lambda<Func<RuleUserContext, TTarget, decimal>>(l.Body, l.Parameters).Compile();
        });
    }

    private sealed class UserHolder(RuleUserContext user) { public RuleUserContext User { get; } = user; }

    /// <summary>
    /// 建立 Row 規則的 EF 述詞：使用者以「閉包成員」帶入，EF 會參數化，因此不同使用者共用同一個查詢計畫。
    /// </summary>
    public static Expression<Func<T, bool>> BuildRowPredicate<T>(string expr, RuleUserContext user)
    {
        var key = $"R|{typeof(T).FullName}|{expr}";
        Trim(RowParsed);
        var lambda = RowParsed.GetOrAdd(key, _ => ParseChecked(typeof(T), RuleKind.Row, expr));
        var it = lambda.Parameters[0];
        var userAccess = Expression.Property(Expression.Constant(new UserHolder(user)), nameof(UserHolder.User));
        var body = new ParamReplacer(lambda.Parameters[1], userAccess).Visit(lambda.Body)!;
        return Expression.Lambda<Func<T, bool>>(body, it);
    }

    internal sealed class ParamReplacer(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
