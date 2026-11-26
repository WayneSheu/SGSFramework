using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Rules;

namespace SGS.Modules.RUL.Infrastructure.Entities;

/// <summary>規則生命週期：只有 Active 且在有效期間內的規則才會被引擎使用。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleStatus : byte
{
    Active = 0,
    Disabled = 1
}

/// <summary>變更類型。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleChangeType : byte
{
    Create = 0,
    Update = 1,
    Disable = 2,
    Enable = 3
}

/// <summary>變更申請狀態（Maker-Checker）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleChangeStatus : byte
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}

/// <summary>動態規則（已核准生效版本）。規則內容只能經變更申請核准後異動。</summary>
[DisplayName("動態規則")]
[Comment("動態規則主檔：僅存放已核准版本，異動須經 RuleChangeRequests 審批")]
public class RuleDefinition : IAuditable
{
    public long Id { get; set; }

    [Comment("規則名稱")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public RuleDomain Domain { get; set; }

    public RuleKind Kind { get; set; }

    [Comment("目標實體（須為已登錄的白名單實體）")]
    public string TargetEntity { get; set; } = string.Empty;

    [Comment("欄位名稱（Column / Validation 規則）")]
    public string? FieldName { get; set; }

    [Comment("適用角色（GlobalPolicy 代表所有人）")]
    public string RoleName { get; set; } = "GlobalPolicy";

    [Comment("條件運算式（System.Linq.Dynamic.Core，經白名單檢查）")]
    public string ConditionExpr { get; set; } = string.Empty;

    [Comment("計價折扣倍率運算式")]
    public string? ActionExpr { get; set; }

    [Comment("JSON 輸出（表單 visible/required/errorMessage；風控 decision/reason）")]
    public string? OutputPayload { get; set; }

    public FieldAccessLevel ReadAccess { get; set; }

    public FieldAccessLevel WriteAccess { get; set; }

    public RiskDecision? Risk { get; set; }

    public int Priority { get; set; }

    public DateTimeOffset? EffectiveFromUtc { get; set; }

    public DateTimeOffset? EffectiveToUtc { get; set; }

    public RuleStatus Status { get; set; } = RuleStatus.Active;

    [Comment("目前生效版本號")]
    public int Version { get; set; } = 1;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

/// <summary>規則變更申請：申請者與審核者必須為不同人（職責分離）。</summary>
[DisplayName("規則變更申請")]
[Comment("規則變更申請（Maker-Checker）")]
public class RuleChangeRequest : IAuditable
{
    public long Id { get; set; }

    [Comment("既有規則 Id；Create 於核准後回填")]
    public long? RuleId { get; set; }

    public RuleChangeType ChangeType { get; set; }

    [Comment("提議的規則內容（JSON；Disable/Enable 為 null）")]
    public string? ProposedJson { get; set; }

    [Comment("申請理由")]
    public string Reason { get; set; } = string.Empty;

    public RuleChangeStatus Status { get; set; } = RuleChangeStatus.Pending;

    public string RequestedBy { get; set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewComment { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

/// <summary>核准後的規則版本快照（可回溯與回滾）。</summary>
[DisplayName("規則版本")]
[Comment("核准後的規則版本快照")]
public class RuleVersion : IAuditable
{
    public long Id { get; set; }
    public long RuleId { get; set; }
    public int Version { get; set; }

    [Comment("完整規則快照（JSON）")]
    public string SnapshotJson { get; set; } = string.Empty;

    public long? ChangeRequestId { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

/// <summary>規則測試案例：以範例使用者與目標 JSON 驗證條件是否命中。</summary>
[DisplayName("規則測試案例")]
[Comment("規則測試案例")]
public class RuleTestCase : IAuditable
{
    public long Id { get; set; }
    public long RuleId { get; set; }
    public string Name { get; set; } = string.Empty;

    [Comment("RuleUserContext JSON")]
    public string UserJson { get; set; } = "{}";

    [Comment("目標實體 JSON")]
    public string TargetJson { get; set; } = "{}";

    [Comment("預期是否命中")]
    public bool ExpectedMatch { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
