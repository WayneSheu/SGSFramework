using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.Base;
using SGSFramework.Core.Abstractions.Entities.Ledgers;

namespace SGSFramework.Core.Abstractions.Entities.AuditLogs
{
    /// <summary>
    /// 審計日誌實體 (支援 SQL Server Ledger 防篡改鏈路與自癒機制)
    /// 使用 JSON 欄位來儲存變化，以便於後續的 NoSQL 存儲或靈活查詢。
    /// </summary>
    public class AuditLogEntity : IEntity<long>, ILedgerEntity
    {
        public long Id { get; set; }

        /// <summary>
        /// 全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)
        /// </summary>
        public string TraceId { get; set; } = string.Empty;

        public string? UserId { get; set; }
        public string? RemoteIp { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        public string? Schema { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;

        public string? KeyValues { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? ChangedColumns { get; set; }

        // 防篡改鏈路欄位
        public string PreviousHash { get; set; } = string.Empty;
        public string StoredHash { get; set; } = string.Empty;

        // 自癒相關欄位
        public bool IsRepaired { get; set; }
        public DateTimeOffset? RepairedAt { get; set; }
        public string? GapReason { get; set; }
        public string? OriginalStoredHash { get; set; }
    }



    public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntity>
    {
        public void Configure(EntityTypeBuilder<AuditLogEntity> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // 1. 資料表與複合主鍵設定 (搭配 CreatedAt 支援分區與 Ledger 機制)
            builder.ToTable("AuditLogs");
            builder.HasKey(e => new { e.Id, e.CreatedAt });

            builder.Property(e => e.Id)
                   .ValueGeneratedOnAdd();

            // 2. 身分與追蹤欄位 (固定 CHAR(32) ANSI 格式)
            builder.Property(e => e.TraceId)
                   .IsRequired()
                   .HasMaxLength(32)
                   .IsFixedLength()
                   .IsUnicode(false);

            builder.Property(e => e.UserId)
                   .HasMaxLength(128)
                   .IsRequired(false);

            builder.Property(e => e.RemoteIp)
                   .HasMaxLength(64)
                   .IsRequired(false);

            // 3. 時間戳記欄位
            builder.Property(e => e.CreatedAt)
                   .HasColumnType("datetimeoffset(7)")
                   .HasDefaultValueSql("SYSUTCDATETIME()")
                   .IsRequired();

            builder.Property(e => e.Timestamp)
                   .HasColumnType("datetimeoffset(7)")
                   .HasDefaultValueSql("SYSUTCDATETIME()")
                   .IsRequired();

            // 4. 行為與目標描述
            builder.Property(e => e.Schema)
                   .HasMaxLength(64)
                   .IsRequired(false);

            builder.Property(e => e.TableName)
                   .HasMaxLength(128)
                   .IsRequired();

            builder.Property(e => e.Action)
                   .HasMaxLength(50)
                   .IsRequired();

            // 5. JSON 異動內容
            builder.Property(e => e.KeyValues).HasColumnType("nvarchar(max)").IsRequired(false);
            builder.Property(e => e.OldValues).HasColumnType("nvarchar(max)").IsRequired(false);
            builder.Property(e => e.NewValues).HasColumnType("nvarchar(max)").IsRequired(false);
            builder.Property(e => e.ChangedColumns).HasColumnType("nvarchar(max)").IsRequired(false);

            // 6. 哈希鏈安全欄位
            builder.Property(e => e.PreviousHash)
                   .HasMaxLength(128)
                   .IsRequired();

            builder.Property(e => e.StoredHash)
                   .HasMaxLength(128)
                   .IsRequired();

            // 7. 自癒機制欄位
            builder.Property(e => e.IsRepaired)
                   .HasDefaultValue(false)
                   .IsRequired();

            builder.Property(e => e.RepairedAt)
                   .HasColumnType("datetimeoffset(7)")
                   .IsRequired(false);

            builder.Property(e => e.GapReason)
                   .HasMaxLength(500)
                   .IsRequired(false);

            builder.Property(e => e.OriginalStoredHash)
                   .HasMaxLength(128)
                   .IsRequired(false);

            // 8. 涵蓋索引與過濾索引
            builder.HasIndex(e => e.TraceId)
                   .HasDatabaseName("IX_AuditLog_TraceId_Covering")
                   .IncludeProperties(e => new { e.Action, e.CreatedAt, e.TableName });

            builder.HasIndex(e => e.IsRepaired)
                   .HasDatabaseName("IX_AuditLog_IsRepaired")
                   .HasFilter("[IsRepaired] = 0")
                   .IncludeProperties(e => new { e.TableName, e.TraceId, e.GapReason });
        }
    }

}
