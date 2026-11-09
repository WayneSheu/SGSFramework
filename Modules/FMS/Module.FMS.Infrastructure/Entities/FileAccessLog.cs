using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SGS.Modules.FMS.Infrastructure.Entities;

/// <summary>檔案存取動作</summary>
public static class FileAccessActions
{
    public const string Upload = "Upload";
    public const string Download = "Download";
    public const string View = "View";
    public const string Thumbnail = "Thumbnail";
    public const string Delete = "Delete";
    public const string Purge = "Purge";
    public const string Encrypt = "Encrypt";

    /// <summary>上傳 / 存取被拒絕（格式不符、超過上限、僅供檢視、權限不足等）</summary>
    public const string Denied = "Denied";

    /// <summary>解密或完整性驗證失敗（疑遭竄改或金鑰失效）</summary>
    public const string IntegrityFailure = "IntegrityFailure";
}

/// <summary>
/// 檔案存取稽核紀錄（fms.FileAccessLogs）：上傳、下載、檢視、縮圖、刪除、清除與失敗事件逐筆留存，
/// 明確區分人員工號與 M2M 系統帳號。資料表以 SQL Server Ledger（APPEND_ONLY）建立，任何人（含 DBA）皆無法修改或刪除。
/// 由背景服務非同步批次寫入，不阻塞檔案傳輸。
/// </summary>
[DisplayName("檔案存取稽核紀錄")]
[Comment("檔案存取稽核紀錄（Ledger APPEND_ONLY）：上傳 / 下載 / 檢視 / 刪除 / 清除與失敗事件，區分人員與系統帳號")]
public class FileAccessLog
{
    [Comment("識別主鍵")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Comment("檔案識別碼；上傳失敗等尚無檔案時為 null")]
    public Guid? FileId { get; set; }

    [Comment("檔案分類")]
    public string? Category { get; set; }

    [Comment("動作：Upload / Download / View / Thumbnail / Delete / Purge / Encrypt / Denied / IntegrityFailure")]
    public string Action { get; set; } = string.Empty;

    [Comment("是否成功")]
    public bool IsSuccess { get; set; }

    [Comment("使用者 / 系統帳號識別碼（NameIdentifier）；匿名為 null")]
    public string? UserId { get; set; }

    [Comment("使用者名稱或系統帳號顯示名稱")]
    public string UserName { get; set; } = string.Empty;

    [Comment("是否為 M2M 系統帳號（Client Credentials）")]
    public bool IsSystemAccount { get; set; }

    [Comment("系統帳號的 client_id；人員為 null")]
    public string? ClientId { get; set; }

    [Comment("連線來源 IP（RemoteIpAddress）")]
    public string? RemoteIp { get; set; }

    [Comment("X-Forwarded-For 原始值（僅記錄，不作為信任依據）")]
    public string? ForwardedFor { get; set; }

    [Comment("User-Agent")]
    public string? UserAgent { get; set; }

    [Comment("全鏈路追蹤碼（CorrelationId / TraceIdentifier）")]
    public string? TraceId { get; set; }

    [Comment("補充說明（失敗原因、縮圖尺寸等）")]
    public string? Detail { get; set; }

    [Comment("發生時間（UTC）")]
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class FileAccessLogConfiguration : IEntityTypeConfiguration<FileAccessLog>
{
    public void Configure(EntityTypeBuilder<FileAccessLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // 實際資料表由 migration 以原生 SQL 建立（LEDGER = ON (APPEND_ONLY = ON)），此處僅做映射
        builder.ToTable("FileAccessLogs", "fms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.Action).HasMaxLength(30).IsRequired();
        builder.Property(x => x.UserId).HasMaxLength(64);
        builder.Property(x => x.UserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ClientId).HasMaxLength(128);
        builder.Property(x => x.RemoteIp).HasMaxLength(64);
        builder.Property(x => x.ForwardedFor).HasMaxLength(256);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.TraceId).HasMaxLength(64);
        builder.Property(x => x.Detail).HasMaxLength(500);
        builder.HasIndex(x => new { x.FileId, x.OccurredAt });
        builder.HasIndex(x => x.OccurredAt);
    }
}
