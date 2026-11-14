using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGS.Modules.PMS.Infrastructure.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UatStatus
{
    /// <summary>待客戶驗收簽核。</summary>
    PendingSign = 0,

    /// <summary>客戶驗收通過。</summary>
    Accepted = 1,

    /// <summary>客戶驗收不通過（修復後以新版本重新驗收）。</summary>
    Rejected = 2,

    Cancelled = 3
}

/// <summary>UAT 驗收簽核單（FORM-UAT-01，SOP 第 5 章）：PM 建立、客戶驗收簽核（目前由 PM 代錄）。UAT 期規格外需求不納入本次驗收，應轉 CR。</summary>
[DisplayName("UAT 驗收簽核單")]
[Comment("UAT 驗收簽核單：版本、驗收範圍、內容雜湊與客戶驗收結果（目前由 PM 代錄）")]
public class UatSignoff : IAuditable
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    [Comment("受驗收的版本號（vX.Y.Z）")]
    public string Version { get; set; } = string.Empty;

    [Comment("驗收範圍說明（納入驗收的功能與測試情境）")]
    public string ScopeNote { get; set; } = string.Empty;

    public UatStatus Status { get; set; } = UatStatus.PendingSign;

    [Comment("驗收內容雜湊（專案代號、版本、驗收範圍的 SHA-256）；簽核須對同一雜湊")]
    public string ContentHash { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    [Comment("驗收結果記錄時間")]
    public DateTimeOffset? ResultAt { get; set; }

    [Comment("記錄結果的人（客戶外部帳號開放前為代錄的 PM）")]
    public Guid? ResultByUserId { get; set; }

    [Comment("是否由 PM 代錄客戶驗收結果")]
    public bool ResultIsProxy { get; set; }

    [Comment("代錄原因與依據（例如客戶 Email 回覆確認）")]
    public string? ProxyReason { get; set; }

    [Comment("客戶驗收意見／不通過原因／取消原因")]
    public string? Comment { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Project? Project { get; set; }
    public List<UatAttachment> Attachments { get; set; } = [];
}

/// <summary>UAT 驗收簽核單附件：客戶回覆信件、簽署掃描檔、驗收截圖等；實體檔案在 FMS（分類 pms-issue），僅存索引。</summary>
[DisplayName("UAT 驗收附件")]
[Comment("UAT 驗收簽核單附件索引：實體檔案儲存於 FMS，僅記錄 FileId")]
public class UatAttachment : IAuditable
{
    public Guid Id { get; set; }
    public Guid UatSignoffId { get; set; }

    [Comment("FMS 檔案識別碼")]
    public Guid FileId { get; set; }

    [Comment("檔名（系統產生：YYYYMMDD_專案代號_UAT_版本_檔案說明）")]
    public string FileName { get; set; } = string.Empty;

    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public string? Sha256 { get; set; }

    [Comment("已確認遮罩個資／密碼／金鑰（SOP 8-4）")]
    public bool MaskConfirmed { get; set; }

    public bool IsDeleted { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public UatSignoff? UatSignoff { get; set; }
}
