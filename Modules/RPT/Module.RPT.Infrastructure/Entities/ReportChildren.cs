using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using SGSFramework.Core.Abstractions.Permissions.Entities;

namespace SGS.Modules.RPT.Infrastructure.Entities;

/// <summary>檢驗數據來源</summary>
public enum ReportValueSource
{
    /// <summary>人工登錄</summary>
    Manual = 0,

    /// <summary>儀器拋轉（防篡改：人工修改須填寫原因）</summary>
    Instrument = 1
}

/// <summary>報告指派角色</summary>
public enum ReportAssignmentRole
{
    /// <summary>檢驗工程師 / 分析員（數據登錄）</summary>
    Analyst = 0,

    /// <summary>指定審核者</summary>
    Reviewer = 1,

    /// <summary>指定簽署人</summary>
    Signer = 2,

    /// <summary>專案成員（例：失效分析專案制權限）</summary>
    ProjectMember = 3,

    /// <summary>負責業務 / 客服</summary>
    SalesOwner = 4
}

/// <summary>
/// 檢驗結果數據列。修改由稽核攔截器保留原值 / 新值 / 修改人；儀器拋轉數據人工修改須填寫變更原因。
/// </summary>
[DisplayName("檢驗結果")]
[Comment("檢驗結果數據列；修改由稽核攔截器保留原值、新值與修改人，儀器拋轉數據人工修改須填寫變更原因")]
public class ReportResult : IAuditable
{
    [Comment("識別主鍵")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Comment("報告識別鍵")]
    public Guid ReportId { get; set; }

    [Comment("排序")]
    public int Sequence { get; set; }

    [Comment("檢驗項目代碼")]
    public string ItemCode { get; set; } = string.Empty;

    [Comment("檢驗項目名稱")]
    public string ItemName { get; set; } = string.Empty;

    [Comment("檢驗結果值")]
    public string? Value { get; set; }

    [Comment("單位")]
    public string? Unit { get; set; }

    [Comment("法規限值 / 規格")]
    public string? LimitText { get; set; }

    [Comment("檢驗方法")]
    public string? Method { get; set; }

    [Comment("數據來源（Manual=人工, Instrument=儀器拋轉）")]
    public ReportValueSource ValueSource { get; set; }

    [Comment("儀器拋轉之原始值（人工修改後仍保留）")]
    public string? InstrumentValue { get; set; }

    [Comment("變更原因（儀器數據人工修改時必填）")]
    public string? ChangeReason { get; set; }

    [Comment("備註")]
    public string? Remark { get; set; }

    [Comment("建立時間")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [Comment("建立人員")]
    public string CreatedBy { get; set; } = string.Empty;

    [Comment("最後更新時間")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    [Comment("最後更新人員")]
    public string? UpdatedBy { get; set; }
}

/// <summary>報告指派：被指派者於「僅經手」範圍下可存取此報告；特檢報告僅限指派人員</summary>
[DisplayName("報告指派")]
[Comment("報告指派：被指派者於「僅經手」範圍下可存取此報告；特檢報告僅限指派人員")]
public class ReportAssignment : IAuditable
{
    [Comment("識別主鍵")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Comment("報告識別鍵")]
    public Guid ReportId { get; set; }

    [Comment("使用者識別鍵")]
    public Guid UserId { get; set; }

    [Comment("使用者名稱")]
    public string UserName { get; set; } = string.Empty;

    [Comment("指派角色（Analyst, Reviewer, Signer, ProjectMember, SalesOwner）")]
    public ReportAssignmentRole Role { get; set; }

    [Comment("建立時間")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [Comment("建立人員")]
    public string CreatedBy { get; set; } = string.Empty;

    [Comment("最後更新時間")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    [Comment("最後更新人員")]
    public string? UpdatedBy { get; set; }
}

/// <summary>報告狀態轉換軌跡（只增不改）</summary>
[DisplayName("報告狀態軌跡")]
[Comment("報告狀態轉換軌跡（只增不改）")]
public class ReportStatusHistory
{
    [Comment("識別主鍵")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Comment("報告識別鍵")]
    public Guid ReportId { get; set; }

    [Comment("原狀態（建立時為 null）")]
    public ReportLifecycleStatus? FromStatus { get; set; }

    [Comment("新狀態")]
    public ReportLifecycleStatus ToStatus { get; set; }

    [Comment("動作（Create, Submit, ApproveReview, RejectReview, Sign, RejectSign, Void, Reissue）")]
    public string Action { get; set; } = string.Empty;

    [Comment("操作者識別鍵")]
    public Guid? ActorId { get; set; }

    [Comment("操作者名稱")]
    public string ActorName { get; set; } = string.Empty;

    [Comment("原因 / 意見")]
    public string? Reason { get; set; }

    [Comment("發生時間")]
    public DateTimeOffset OccurredAt { get; set; }
}

/// <summary>報告作廢 / 重發申請（Maker-Checker：申請人不得核准）</summary>
[DisplayName("報告作廢申請")]
[Comment("報告作廢 / 重發申請（Maker-Checker：申請人不得核准）")]
public class ReportVoidRequest : IAuditable
{
    [Comment("識別主鍵")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Comment("報告識別鍵")]
    public Guid ReportId { get; set; }

    [Comment("作廢原因")]
    public string Reason { get; set; } = string.Empty;

    [Comment("核准後是否自動建立重發草稿")]
    public bool Reissue { get; set; }

    [Comment("申請狀態（Pending, Approved, Rejected, Cancelled）")]
    public RoleChangeRequestStatus Status { get; set; } = RoleChangeRequestStatus.Pending;

    [Comment("申請人識別鍵")]
    public Guid RequestedBy { get; set; }

    [Comment("申請人名稱")]
    public string RequestedByName { get; set; } = string.Empty;

    [Comment("申請時間")]
    public DateTimeOffset RequestedAt { get; set; }

    [Comment("審核人識別鍵")]
    public Guid? ReviewedBy { get; set; }

    [Comment("審核人名稱")]
    public string? ReviewedByName { get; set; }

    [Comment("審核時間")]
    public DateTimeOffset? ReviewedAt { get; set; }

    [Comment("審核意見")]
    public string? ReviewComment { get; set; }

    [Comment("重發之新版報告識別鍵")]
    public Guid? ReissuedReportId { get; set; }

    [Comment("建立時間")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [Comment("建立人員")]
    public string CreatedBy { get; set; } = string.Empty;

    [Comment("最後更新時間")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    [Comment("最後更新人員")]
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// 報告存取紀錄（檢視、下載、列印、真偽查驗）。資料表以 SQL Server Ledger (APPEND_ONLY) 建立，任何人（含 DBA）皆無法修改或刪除。
/// </summary>
[DisplayName("報告存取紀錄")]
[Comment("報告存取紀錄（檢視、下載、列印、真偽查驗）；Ledger APPEND_ONLY，無法修改或刪除")]
public class ReportAccessLog
{
    [Comment("識別主鍵")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Comment("報告識別鍵（非檢驗報告之一般報表為 null）")]
    public Guid? ReportId { get; set; }

    [Comment("報告編號")]
    public string? ReportNo { get; set; }

    [Comment("報表類型代碼（檢驗報告為類別代碼）")]
    public string ReportTypeCode { get; set; } = string.Empty;

    [Comment("動作（View, Download, Print, Verify）")]
    public string Action { get; set; } = string.Empty;

    [Comment("使用者識別鍵（匿名查驗為 null）")]
    public string? UserId { get; set; }

    [Comment("使用者名稱")]
    public string UserName { get; set; } = string.Empty;

    [Comment("來源 IP")]
    public string? RemoteIp { get; set; }

    [Comment("User-Agent")]
    public string? UserAgent { get; set; }

    [Comment("補充說明")]
    public string? Detail { get; set; }

    [Comment("發生時間")]
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class ReportResultConfiguration : IEntityTypeConfiguration<ReportResult>
{
    public void Configure(EntityTypeBuilder<ReportResult> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReportResults", "rpt");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ItemCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(100);
        builder.Property(x => x.Unit).HasMaxLength(30);
        builder.Property(x => x.LimitText).HasMaxLength(100);
        builder.Property(x => x.Method).HasMaxLength(200);
        builder.Property(x => x.ValueSource).HasMaxLength(20);
        builder.Property(x => x.InstrumentValue).HasMaxLength(100);
        builder.Property(x => x.ChangeReason).HasMaxLength(500);
        builder.Property(x => x.Remark).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.ReportId, x.Sequence });
    }
}

public sealed class ReportAssignmentConfiguration : IEntityTypeConfiguration<ReportAssignment>
{
    public void Configure(EntityTypeBuilder<ReportAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReportAssignments", "rpt");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Role).HasMaxLength(20);
        builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.ReportId, x.UserId, x.Role }).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

public sealed class ReportStatusHistoryConfiguration : IEntityTypeConfiguration<ReportStatusHistory>
{
    public void Configure(EntityTypeBuilder<ReportStatusHistory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReportStatusHistories", "rpt");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FromStatus).HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasMaxLength(20);
        builder.Property(x => x.Action).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ActorName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.ReportId, x.OccurredAt });
    }
}

public sealed class ReportVoidRequestConfiguration : IEntityTypeConfiguration<ReportVoidRequest>
{
    public void Configure(EntityTypeBuilder<ReportVoidRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReportVoidRequests", "rpt");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.RequestedByName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ReviewedByName).HasMaxLength(256);
        builder.Property(x => x.ReviewComment).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.ReportId, x.Status });
    }
}

public sealed class ReportAccessLogConfiguration : IEntityTypeConfiguration<ReportAccessLog>
{
    public void Configure(EntityTypeBuilder<ReportAccessLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // 實際資料表由 Migration 以 LEDGER = ON (APPEND_ONLY = ON) 原生 SQL 建立
        builder.ToTable("ReportAccessLogs", "rpt");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReportNo).HasMaxLength(40);
        builder.Property(x => x.ReportTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(20).IsRequired();
        builder.Property(x => x.UserId).HasMaxLength(64);
        builder.Property(x => x.UserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.RemoteIp).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.Detail).HasMaxLength(500);
        builder.HasIndex(x => new { x.ReportId, x.OccurredAt });
    }
}
