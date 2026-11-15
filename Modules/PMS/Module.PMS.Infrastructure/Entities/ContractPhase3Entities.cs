using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>非服務時間支援申請的狀態。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SupportStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>產業合規項目的處理狀態。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComplianceStatus
{
    /// <summary>尚未處理。</summary>
    Pending = 0,

    /// <summary>已納入合約（條款已寫入或附約已約定）。</summary>
    Included = 1,

    /// <summary>不適用本專案（須填原因）。</summary>
    NotApplicable = 2
}

/// <summary>
/// 非服務時間（平日 09:00–18:00 以外、假日）支援申請：須提前 5 個工作日申請，依平日加班 1.5 倍、深夜與例假日 2.0 倍、國定假日 2.5 倍計算加權時數。
/// 核決主管核准後才可動員；完成後記錄實際起訖以計算實際加權時數（請款依據）。
/// </summary>
[DisplayName("合約非服務時間支援申請")]
[Comment("非服務時間支援申請：窗口、加班加權時數、核准與實際執行")]
public class ContractSupportRequest : IAuditable
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }

    [Comment("合約內流水號")]
    public int Number { get; set; }

    [Comment("支援事由")]
    public string Title { get; set; } = string.Empty;

    [Comment("預計支援起（含時區）")]
    public DateTimeOffset WindowStart { get; set; }

    [Comment("預計支援迄（含時區）")]
    public DateTimeOffset WindowEnd { get; set; }

    public SupportStatus Status { get; set; } = SupportStatus.Pending;

    [Comment("預估總時數（含標準服務時間內）")]
    public decimal EstimatedHours { get; set; }

    [Comment("預估加班時數（標準服務時間以外）")]
    public decimal EstimatedOvertimeHours { get; set; }

    [Comment("預估加權時數（加班時數 × 倍率合計）")]
    public decimal EstimatedWeightedHours { get; set; }

    public DateTimeOffset? ActualStart { get; set; }
    public DateTimeOffset? ActualEnd { get; set; }

    [Comment("實際加班時數")]
    public decimal? ActualOvertimeHours { get; set; }

    [Comment("實際加權時數（請款依據）")]
    public decimal? ActualWeightedHours { get; set; }

    public Guid RequestedBy { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }

    [Comment("甲方提出此支援需求的日期（佐證：甲方須提前申請）")]
    public DateOnly? ClientRequestedDate { get; set; }

    [Comment("甲方提出方式（Email／電話／會議／即時通訊／其他）")]
    public string? ClientRequestChannel { get; set; }

    [Comment("甲方提出的佐證說明（信件主旨、聯絡人、單號等）")]
    public string? ClientRequestRef { get; set; }

    public Guid? DecidedBy { get; set; }
    public string? DecidedByName { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    [Comment("核准／駁回意見、完成或取消備註")]
    public string? Note { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
}

/// <summary>產業合規清單項目（金融、醫療、製造各有專屬條款）：審約時逐項確認已納入合約或不適用。</summary>
[DisplayName("合約產業合規項目")]
[Comment("產業合規清單：金融／醫療／製造專屬條款的處理狀態")]
public class ContractComplianceItem : IAuditable
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }

    [Comment("項目代碼（例 FIN_DR、MED_SAMD、MFG_DOWNTIME）")]
    public string Code { get; set; } = string.Empty;

    public ComplianceStatus Status { get; set; } = ComplianceStatus.Pending;

    [Comment("說明：納入的條款位置，或不適用的原因")]
    public string? Note { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
}
