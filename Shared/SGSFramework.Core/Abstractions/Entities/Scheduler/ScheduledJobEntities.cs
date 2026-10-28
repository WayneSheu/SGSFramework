using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using System;
using System.ComponentModel;

namespace SGSFramework.Core.Abstractions.Entities.Scheduler
{
    /// <summary>
    /// 排程作業的執行期設定：由排程管理頁暫停 / 啟動，重啟後仍維持（組態 Scheduler:Jobs 決定作業是否存在，此表決定是否自動排程）。
    /// </summary>
    [DisplayName("排程作業設定")]
    [Comment("排程作業的執行期設定：由排程管理頁暫停 / 啟動，系統重啟後仍維持")]
    public class ScheduledJobSetting : IAuditable
    {
        [DisplayName("作業識別碼")]
        [Comment("週期作業 ID（例如 database-backup-full）")]
        public string JobId { get; set; } = string.Empty;

        [DisplayName("是否暫停")]
        [Comment("是否暫停自動排程（暫停時仍可手動立即執行）")]
        public bool IsPaused { get; set; }

        [DisplayName("暫停原因")]
        [Comment("暫停原因")]
        public string? PauseReason { get; set; }

        [DisplayName("自訂週期")]
        [Comment("排程管理頁設定的 Cron（5 碼）；null 表示沿用組態 / 程式預設")]
        public string? Cron { get; set; }

        [DisplayName("自訂時區")]
        [Comment("排程管理頁設定的時區（IANA 或 Windows ID）；null 表示沿用組態預設")]
        public string? TimeZone { get; set; }

        [DisplayName("建立人員")]
        [Comment("建立人員")]
        public string CreatedBy { get; set; } = string.Empty;

        [DisplayName("建立時間")]
        [Comment("建立時間")]
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        [DisplayName("最後異動人員")]
        [Comment("最後暫停 / 啟動的人員")]
        public string? UpdatedBy { get; set; }

        [DisplayName("最後異動時間")]
        [Comment("最後暫停 / 啟動的時間")]
        public DateTimeOffset? UpdatedAtUtc { get; set; }
    }

    /// <summary>
    /// 排程作業執行紀錄：每次嘗試一筆（重試為新的一筆），保留 Hangfire 作業 ID 供追查。
    /// </summary>
    [DisplayName("排程作業執行紀錄")]
    [Comment("排程作業執行紀錄：每次嘗試一筆（重試為新的一筆）")]
    public class ScheduledJobRun
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("作業識別碼")]
        [Comment("週期作業 ID（例如 database-backup-full）")]
        public string JobId { get; set; } = string.Empty;

        [DisplayName("作業名稱")]
        [Comment("執行當下的作業顯示名稱")]
        public string JobName { get; set; } = string.Empty;

        [DisplayName("Hangfire 作業 ID")]
        [Comment("Hangfire 背景作業 ID（同一作業的重試共用）")]
        public string HangfireJobId { get; set; } = string.Empty;

        [DisplayName("嘗試次數")]
        [Comment("第幾次嘗試（1 = 首次執行）")]
        public int Attempt { get; set; } = 1;

        [DisplayName("觸發方式")]
        [Comment("觸發方式：Schedule（排程）或 Manual（手動立即執行）")]
        public string Trigger { get; set; } = ScheduledJobTriggers.Schedule;

        [DisplayName("觸發人員")]
        [Comment("手動執行的人員帳號；排程觸發為 null")]
        public string? TriggeredBy { get; set; }

        [DisplayName("執行狀態")]
        [Comment("執行狀態：Running / Succeeded / Failed / Retrying")]
        public string Status { get; set; } = ScheduledJobRunStatuses.Running;

        [DisplayName("開始時間")]
        [Comment("開始時間")]
        public DateTimeOffset StartedAt { get; set; }

        [DisplayName("結束時間")]
        [Comment("結束時間")]
        public DateTimeOffset? FinishedAt { get; set; }

        [DisplayName("耗時（毫秒）")]
        [Comment("耗時（毫秒）")]
        public long? DurationMs { get; set; }

        [DisplayName("訊息")]
        [Comment("失敗原因或重試說明")]
        public string? Message { get; set; }

        [DisplayName("執行節點")]
        [Comment("執行作業的伺服器（多節點時追查用）")]
        public string? ServerName { get; set; }
    }

    public static class ScheduledJobTriggers
    {
        public const string Schedule = "Schedule";
        public const string Manual = "Manual";
    }

    public static class ScheduledJobRunStatuses
    {
        public const string Running = "Running";
        public const string Succeeded = "Succeeded";
        public const string Failed = "Failed";

        /// <summary>本次嘗試失敗，已排定重試</summary>
        public const string Retrying = "Retrying";
    }

    public class ScheduledJobSettingConfiguration : IEntityTypeConfiguration<ScheduledJobSetting>
    {
        public void Configure(EntityTypeBuilder<ScheduledJobSetting> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("ScheduledJobSettings", "core");
            builder.HasKey(x => x.JobId);
            builder.Property(x => x.JobId).HasMaxLength(100);
            builder.Property(x => x.PauseReason).HasMaxLength(500);
            builder.Property(x => x.Cron).HasMaxLength(100);
            builder.Property(x => x.TimeZone).HasMaxLength(100);
            builder.Property(x => x.CreatedBy).HasMaxLength(256);
            builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        }
    }

    public class ScheduledJobRunConfiguration : IEntityTypeConfiguration<ScheduledJobRun>
    {
        public void Configure(EntityTypeBuilder<ScheduledJobRun> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("ScheduledJobRuns", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.JobId).HasMaxLength(100).IsRequired();
            builder.Property(x => x.JobName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.HangfireJobId).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Trigger).HasMaxLength(20).IsRequired();
            builder.Property(x => x.TriggeredBy).HasMaxLength(256);
            builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
            builder.Property(x => x.Message).HasMaxLength(2000);
            builder.Property(x => x.ServerName).HasMaxLength(200);
            builder.HasIndex(x => new { x.JobId, x.StartedAt });
            builder.HasIndex(x => x.StartedAt);
            builder.HasIndex(x => new { x.HangfireJobId, x.Attempt }).IsUnique();
        }
    }
}
