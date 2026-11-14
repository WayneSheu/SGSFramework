using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>RCA 觸發條件（SOP 7.3）：P1 異常、插件降級／回滾、處理時間超過合約 SLA。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RcaTrigger
{
    P1 = 1,
    Downgrade = 2,
    SlaBreach = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RcaStatus
{
    /// <summary>撰寫中。</summary>
    Draft = 0,

    /// <summary>Tech Lead 已提交，待 PM 潤飾並寄送客戶。</summary>
    Submitted = 1,

    /// <summary>PM 已寄送客戶，內容鎖定。</summary>
    Sent = 2
}

/// <summary>P1 重大異常分析報告（FORM-RCA-01，SOP 7.3）：每張問題單至多一份。一律以團隊為主詞，嚴禁點名個別工程師。</summary>
[DisplayName("RCA 報告")]
[Comment("P1 重大異常分析報告：事件摘要、時間軸、根因、短期止血與長期預防；系統恢復後 48 小時內提交")]
public class RcaReport : IAuditable
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid ProjectId { get; set; }

    public RcaTrigger Trigger { get; set; }
    public RcaStatus Status { get; set; } = RcaStatus.Draft;

    [Comment("事件摘要")]
    public string Summary { get; set; } = string.Empty;

    [Comment("系統恢復時間（提交期限自此起算 48 小時）")]
    public DateTimeOffset RecoveredAt { get; set; }

    [Comment("提交期限＝系統恢復後 48 小時")]
    public DateTimeOffset DueAt { get; set; }

    [Comment("精確時間軸（JSON：[{\"at\":…,\"event\":…}]）")]
    public string TimelineJson { get; set; } = "[]";

    [Comment("技術根因（敏感）")]
    public string RootCause { get; set; } = string.Empty;

    [Comment("短期止血措施")]
    public string ShortTermAction { get; set; } = string.Empty;

    [Comment("長期預防措施")]
    public string LongTermPrevention { get; set; } = string.Empty;

    public Guid? SubmittedBy { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? SentBy { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    [Comment("寄送客戶的備註（寄送對象、方式）")]
    public string? SentNote { get; set; }

    [Comment("系統代寄的收件人與副本摘要（僅 Email 模式；僅記錄模式為空）")]
    public string? SentTo { get; set; }

    [Comment("系統代寄時存檔的正式版 PDF（FMS 分類 pms-form，加密）；僅記錄模式為空")]
    public Guid? SentPdfFileId { get; set; }

    public string? SentPdfFileName { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Ticket? Ticket { get; set; }
}
