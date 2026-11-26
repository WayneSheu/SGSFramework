
using System.Text.Json.Serialization;

namespace SGSFramework.Core.Rules;

/// <summary>規則領域（對應不同的結果與合併策略）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleDomain
{
    /// <summary>資料權限（Row/Column ABAC）。</summary>
    Permission = 0,
    /// <summary>動態表單（欄位顯隱、必填、驗證訊息）。</summary>
    DynamicForm = 1,
    /// <summary>風險控管（Pass / ManualReview / Block）。</summary>
    RiskControl = 2,
    /// <summary>計價折扣。</summary>
    Pricing = 3
}

/// <summary>規則種類：Row 轉 SQL 過濾；其餘於記憶體評估。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleKind
{
    Row = 0,
    Column = 1,
    Validation = 2,
    Risk = 3,
    Pricing = 4
}

/// <summary>欄位存取等級。Allow &gt; Mask &gt; Deny；ForceDeny 為合規強制黑名單，短路覆蓋所有正向權限。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FieldAccessLevel
{
    Deny = 0,
    Mask = 1,
    Allow = 2,
    ForceDeny = 99
}

/// <summary>風控決策。Block &gt; ManualReview &gt; Pass，命中 Block 即短路。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RiskDecision
{
    Pass = 1,
    ManualReview = 2,
    Block = 99
}

/// <summary>計價規則合併策略。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PricingMergeStrategy
{
    /// <summary>取最優惠（最小折扣倍率）。</summary>
    BestPrice = 0,
    /// <summary>依優先序疊加（倍率連乘）。</summary>
    Stack = 1
}

/// <summary>規則可引用的使用者屬性（運算式中以 <c>user.</c> 存取；僅此型別的成員允許被引用）。</summary>
public sealed class RuleUserContext
{
    public string UserId { get; init; } = string.Empty;
    /// <summary>UserId 解析後的 Guid（無法解析為 Guid.Empty），供與實體 Id 比較（例：target.Id == user.UserGuid）。</summary>
    /// <summary>是否為系統管理員（供規則例外，例：!user.IsAdmin）。</summary>
    public bool IsAdmin { get; init; }
    public Guid UserGuid => Guid.TryParse(UserId, out var g) ? g : Guid.Empty;
    public string DepartmentPath { get; init; } = string.Empty;
    public int Level { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    /// <summary>「我的成員」使用者 Id（含自己）。由成員提供者計算，供 Row 規則以 user.MemberUserIds.Contains(row.Id) 過濾。</summary>
    public IReadOnlyList<Guid> MemberUserIds { get; init; } = [];
}

/// <summary>引擎使用的不可變規則快照（由規則儲存庫在核准生效後提供）。</summary>
public sealed record RuleSnapshot
{
    public long Id { get; init; }
    public string? Name { get; init; }
    public int Version { get; init; }
    public RuleDomain Domain { get; init; }
    public RuleKind Kind { get; init; }
    public string TargetEntity { get; init; } = string.Empty;
    public string? FieldName { get; init; }
    public string RoleName { get; init; } = string.Empty;
    public string ConditionExpr { get; init; } = string.Empty;
    /// <summary>計價規則的折扣倍率運算式（回傳 decimal）。</summary>
    public string? ActionExpr { get; init; }
    /// <summary>JSON 輸出（表單：visible/required/errorMessage；風控：decision/reason）。</summary>
    public string? OutputPayload { get; init; }
    public FieldAccessLevel ReadAccess { get; init; }
    public FieldAccessLevel WriteAccess { get; init; }
    public RiskDecision? Risk { get; init; }
    public int Priority { get; init; }
}

/// <summary>規則快照來源（由 RUL 外掛實作；未安裝時為空集合）。</summary>
public interface IRuleSnapshotProvider
{
    /// <summary>目前生效（已核准、啟用且在有效期間內）的規則。</summary>
    IReadOnlyList<RuleSnapshot> GetActiveRules();
}

public sealed class NullRuleSnapshotProvider : IRuleSnapshotProvider
{
    public IReadOnlyList<RuleSnapshot> GetActiveRules() => [];
}

/// <summary>運算式驗證結果。</summary>
public sealed record RuleValidationResult(bool IsValid, string? Error)
{
    public static RuleValidationResult Ok { get; } = new(true, null);
    public static RuleValidationResult Fail(string error) => new(false, error);
}

/// <summary>動態表單單一欄位的 UI 狀態。</summary>
public sealed record FormFieldState(string Field, bool Visible, bool Required, IReadOnlyList<string> Messages);

/// <summary>寫入被規則明確禁止的資訊；Message 取自規則酬載 errorMessage（可為 null）。</summary>
public sealed record RuleDenial(long RuleId, string? RuleName, string? Message);

/// <summary>風控評估結果。</summary>
public sealed record RiskVerdict(RiskDecision Decision, IReadOnlyList<string> Reasons);

/// <summary>計價評估結果：折扣倍率（1 = 無折扣）與命中的規則。</summary>
public sealed record PricingVerdict(decimal Multiplier, IReadOnlyList<long> AppliedRuleIds);

/// <summary>運算式安全驗證（白名單）。</summary>
public interface IRuleExpressionValidator
{
    RuleValidationResult Validate(Type targetType, RuleKind kind, string expression);

    /// <summary>以已登錄的目標實體名稱驗證；名稱未登錄視為失敗。</summary>
    RuleValidationResult Validate(string targetEntity, RuleKind kind, string expression);
}

/// <summary>可被規則引用的目標實體（白名單）。</summary>
public sealed record RuleTargetInfo(string Name, string Description, IReadOnlyList<string> Fields);

/// <summary>目標實體登錄表：管理後台下拉、運算式驗證與試跑共用。</summary>
public interface IRuleTargetCatalog
{
    IReadOnlyList<RuleTargetInfo> GetTargets();
    Type? Resolve(string name);
}

/// <summary>規則試跑結果。</summary>
public sealed record RuleTestOutcome(bool Success, bool Matched, string? Detail, string? Error);

/// <summary>規則試跑：以 JSON 範例資料評估單一規則（不影響線上規則）。</summary>
public interface IRuleTester
{
    RuleTestOutcome Test(RuleSnapshot rule, RuleUserContext user, string targetJson);
}

/// <summary>通用動態規則引擎（Core 契約，其他模組只依賴此介面）。</summary>
public interface IRuleEngine
{
    FieldAccessLevel EvaluateRead<TTarget>(RuleUserContext user, TTarget target, string fieldName);
    bool EvaluateWrite<TTarget>(RuleUserContext user, TTarget target, string fieldName);
    /// <summary>
    /// 是否有命中的 Column 規則將該欄位的寫入設為 ForceDeny（只看明確禁止，不要求有 Allow 規則）。
    /// 命中時回傳禁止資訊（訊息取自規則酬載 errorMessage，未設定時為 null）；未命中回傳 null。
    /// </summary>
    RuleDenial? FindWriteDenial<TTarget>(RuleUserContext user, TTarget target, string fieldName);
    IQueryable<TTarget> ApplyRowSecurity<TTarget>(IQueryable<TTarget> query, RuleUserContext user);
    /// <summary>此使用者（依其角色）對該實體是否有任何 Row 規則；沒有代表不受 Row 限制。</summary>
    bool HasRowRules<TTarget>(RuleUserContext user);
    IReadOnlyList<FormFieldState> EvaluateForm<TTarget>(RuleUserContext user, TTarget target);
    RiskVerdict EvaluateRisk<TTarget>(RuleUserContext user, TTarget target);
    PricingVerdict EvaluatePricing<TTarget>(RuleUserContext user, TTarget target, PricingMergeStrategy strategy);
}

/// <summary>未載入規則來源時的預設引擎：權限一律拒絕，其餘維持中性結果。</summary>
public sealed class NullRuleEngine : IRuleEngine
{
    public FieldAccessLevel EvaluateRead<TTarget>(RuleUserContext user, TTarget target, string fieldName) => FieldAccessLevel.Deny;
    public bool EvaluateWrite<TTarget>(RuleUserContext user, TTarget target, string fieldName) => false;
    public RuleDenial? FindWriteDenial<TTarget>(RuleUserContext user, TTarget target, string fieldName) => null;
    public IQueryable<TTarget> ApplyRowSecurity<TTarget>(IQueryable<TTarget> query, RuleUserContext user) => query.Where(_ => false);
    public bool HasRowRules<TTarget>(RuleUserContext user) => false;
    public IReadOnlyList<FormFieldState> EvaluateForm<TTarget>(RuleUserContext user, TTarget target) => [];
    public RiskVerdict EvaluateRisk<TTarget>(RuleUserContext user, TTarget target) => new(RiskDecision.Pass, []);
    public PricingVerdict EvaluatePricing<TTarget>(RuleUserContext user, TTarget target, PricingMergeStrategy strategy) => new(1m, []);
}
