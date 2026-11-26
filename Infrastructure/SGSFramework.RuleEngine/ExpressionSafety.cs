using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Linq.Expressions;
using System.Reflection;
using SGSFramework.Core.Rules;

namespace SGSFramework.RuleEngine;

/// <summary>不開放任何靜態型別（Math、Convert、Type…）給運算式使用。</summary>
internal sealed class EmptyTypeProvider : IDynamicLinqCustomTypeProvider
{
    public HashSet<Type> GetCustomTypes() => [];
    public Dictionary<Type, List<MethodInfo>> GetExtensionMethods() => [];
    public Type? ResolveType(string typeName) => null;
    public Type? ResolveTypeBySimpleName(string simpleName) => null;
}

/// <summary>運算式白名單策略與共用解析設定。</summary>
public static class ExpressionPolicy
{
    public const int MaxLength = 500;
    public const int MaxNodes = 200;

    public static readonly ParsingConfig Config = new()
    {
        CustomTypeProvider = new EmptyTypeProvider(),
        AllowNewToEvaluateAnyType = false,
        ResolveTypesBySimpleName = false
    };

    private static readonly HashSet<Type> ScalarTypes =
    [
        typeof(string), typeof(bool), typeof(byte), typeof(short), typeof(int), typeof(long),
        typeof(decimal), typeof(double), typeof(float), typeof(Guid), typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly),
        typeof(TimeSpan)
    ];

    // 允許的方法：(宣告型別, 名稱)
    private static readonly HashSet<string> AllowedStringMethods =
        ["StartsWith", "EndsWith", "Contains", "ToLower", "ToUpper", "Equals", "Trim", "IsNullOrEmpty"];

    private static readonly HashSet<string> AllowedEnumerableMethods =
        ["Any", "All", "Count", "Contains", "Select", "Where", "Sum", "Min", "Max", "Average"];

    internal static bool IsScalar(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t.IsEnum || ScalarTypes.Contains(t);
    }

    internal static bool IsMemberAllowed(MemberInfo member, HashSet<Assembly> trustedAssemblies, Type? targetType = null)
    {
        var declaring = member.DeclaringType!;
        if (declaring == typeof(object) || declaring == typeof(Type) || typeof(MemberInfo).IsAssignableFrom(declaring)) return false;
        if (member.Name is "GetType" or "GetHashCode") return false;
        // 機敏欄位不可被規則引用（避免以條件結果探測內容）
        if (member.Name is "PasswordHash" or "SecurityStamp" or "ConcurrencyStamp" or "NormalizedUserName" or "NormalizedEmail") return false;
        var d = Nullable.GetUnderlyingType(declaring) ?? declaring;
        if (declaring == typeof(RuleUserContext)) return true;
        if (IsScalar(d)) return true;               // string.Length、DateTime.Year、Nullable.Value…
        if (d.IsGenericType && d.GetGenericTypeDefinition() == typeof(List<>)) return member.Name is "Count" or "Item";
        if (targetType is not null && d.IsAssignableFrom(targetType)) return true; // 目標型別的基底類別成員（例：IdentityUser.Id）
        return trustedAssemblies.Contains(d.Assembly); // 使用者內容與註冊的實體型別
    }

    internal static bool IsMethodAllowed(MethodInfo m, HashSet<Assembly> trustedAssemblies)
    {
        var declaring = m.DeclaringType!;
        if (declaring == typeof(string)) return AllowedStringMethods.Contains(m.Name);
        if (declaring == typeof(Enumerable) || declaring == typeof(Queryable)) return AllowedEnumerableMethods.Contains(m.Name);
        if (declaring.IsGenericType && declaring.GetGenericTypeDefinition() == typeof(List<>)) return m.Name == "Contains";
        if (declaring == typeof(Convert)) return false;
        if (declaring.IsGenericType && declaring.GetGenericTypeDefinition() == typeof(HashSet<>)) return m.Name == "Contains";
        var d = Nullable.GetUnderlyingType(declaring) ?? declaring;
        if (IsScalar(d)) return m.Name is "Equals" or "CompareTo" or "ToString";
        return false;
    }
}

/// <summary>以 ExpressionVisitor 掃描運算式樹：僅允許白名單成員與方法。</summary>
internal sealed class SafetyVisitor(HashSet<Assembly> trusted, Type? targetType = null) : ExpressionVisitor
{
    private int _nodes;
    public string? Error { get; private set; }

    public override Expression? Visit(Expression? node)
    {
        if (node is null || Error is not null) return node;
        if (++_nodes > ExpressionPolicy.MaxNodes) { Error = "運算式過於複雜。"; return node; }
        return base.Visit(node);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (!ExpressionPolicy.IsMemberAllowed(node.Member, trusted, targetType)) Error = $"不允許存取成員：{node.Member.DeclaringType?.Name}.{node.Member.Name}";
        return Error is null ? base.VisitMember(node) : node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (!ExpressionPolicy.IsMethodAllowed(node.Method, trusted)) Error = $"不允許呼叫方法：{node.Method.DeclaringType?.Name}.{node.Method.Name}";
        return Error is null ? base.VisitMethodCall(node) : node;
    }

    protected override Expression VisitInvocation(InvocationExpression node) { Error = "不允許委派呼叫。"; return node; }
    protected override Expression VisitNew(NewExpression node)
    {
        if (!ExpressionPolicy.IsScalar(node.Type)) Error = $"不允許建立物件：{node.Type.Name}";
        return Error is null ? base.VisitNew(node) : node;
    }
    protected override Expression VisitConstant(ConstantExpression node)
    {
        // 允許純量常數與 null；集合常數僅限純量元素（in 清單）
        if (node.Value is null || ExpressionPolicy.IsScalar(node.Type)) return node;
        if (node.Value is System.Collections.IEnumerable e && node.Value is not string &&
            e.Cast<object?>().All(x => x is null || ExpressionPolicy.IsScalar(x.GetType()))) return node;
        Error = $"不允許常數型別：{node.Type.Name}";
        return node;
    }
}

public sealed class RuleExpressionValidator(IRuleTargetCatalog? catalog = null) : IRuleExpressionValidator
{
    public RuleValidationResult Validate(string targetEntity, RuleKind kind, string expression)
    {
        var t = catalog?.Resolve(targetEntity);
        return t is null ? RuleValidationResult.Fail($"目標實體未登錄：{targetEntity}") : Validate(t, kind, expression);
    }

    public RuleValidationResult Validate(Type targetType, RuleKind kind, string expression)
    {
        try
        {
            var lambda = ExpressionCompiler.Parse(targetType, kind, expression);
            return ExpressionCompiler.Check(lambda, targetType);
        }
        catch (Exception ex)
        {
            return RuleValidationResult.Fail($"語法錯誤：{ex.Message}");
        }
    }
}
