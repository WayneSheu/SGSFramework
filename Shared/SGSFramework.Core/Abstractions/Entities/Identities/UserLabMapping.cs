using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 使用者與實驗室對應關係實體 (系統核心權限與多租戶 Context 劃分)
    /// </summary>
    public class UserLabMapping : IAuditable
    {
        public Guid UserId { get; private set; }
        public Guid LabId { get; private set; }
        public Guid TenantLabId { get; private set; }
        public bool IsPrimary { get; private set; }
        public string? JobTitle { get; private set; }
        public DateTime EffectiveDate { get; private set; }
        public DateTime? ExpiryDate { get; private set; }
        public bool IsActive { get; private set; }

        // --- IAuditable 實作 (統一使用帶時區的 DateTimeOffset) ---
        public DateTimeOffset CreatedAtUtc   { get;  set; }
        public string? CreatedBy { get;  set; }
        public DateTimeOffset? UpdatedAtUtc { get;  set; }
        public string? UpdatedBy { get;  set; }

        public virtual ApplicationUser User { get; set; } = null!;

        private UserLabMapping() { }

        public static UserLabMapping CreatePrimary(
            Guid userId,
            Guid labId,
            Guid tenantLabId,
            string? jobTitle = null,
            string? operatorId = null)
        {
            ValidateInputs(userId, labId, tenantLabId);

            var now = DateTimeOffset.UtcNow;
            return new UserLabMapping
            {
                UserId = userId,
                LabId = labId,
                TenantLabId = tenantLabId,
                IsPrimary = true,
                JobTitle = jobTitle?.Trim(),
                EffectiveDate = now.UtcDateTime,
                IsActive = true,
                CreatedAtUtc = now,
                CreatedBy = operatorId
            };
        }

        public void DemoteToSecondary(string? operatorId = null)
        {
            IsPrimary = false;
            SetUpdated(operatorId);
        }

        public void PromoteToPrimary(string? operatorId = null)
        {
            IsPrimary = true;
            SetUpdated(operatorId);
        }

        public void Deactivate(string? operatorId = null)
        {
            IsActive = false;
            SetUpdated(operatorId);
        }

        private void SetUpdated(string? operatorId)
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow;
            UpdatedBy = operatorId;
        }

        private static void ValidateInputs(Guid userId, Guid labId, Guid tenantLabId)
        {
            if (userId == Guid.Empty) throw new ArgumentException("UserId 不能為 Empty Guid。", nameof(userId));
            if (labId == Guid.Empty) throw new ArgumentException("LabId 不能為 Empty Guid。", nameof(labId));
            if (tenantLabId == Guid.Empty) throw new ArgumentException("TenantLabId 不能為 Empty Guid。", nameof(tenantLabId));
        }
    }

    /// <summary>
    /// UserLabMapping 實體的 EF Core Fluent API 映射組態 (核心與外掛解耦設計)
    /// 支援 IAuditable 稽核屬性與 MSSQL 2025 唯一性過濾索引
    /// </summary>
    public class UserLabMappingConfiguration : IEntityTypeConfiguration<UserLabMapping>
    {
        public void Configure(EntityTypeBuilder<UserLabMapping> builder)
        {
            // 1. 資料表與 Schema 定義
            builder.ToTable("UserLabMappings", "core");

            // 2. 複合主鍵設定 (UserId + LabId)
            builder.HasKey(x => new { x.UserId, x.LabId });

            // 3. 業務與狀態屬性設定
            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.LabId)
                .IsRequired();

            builder.Property(x => x.TenantLabId)
                .IsRequired();

            builder.Property(x => x.IsPrimary)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(x => x.JobTitle)
                .HasMaxLength(50)
                .IsRequired(false);

            builder.Property(x => x.EffectiveDate)
                .HasColumnType("datetime2(7)")
                .IsRequired();

            builder.Property(x => x.ExpiryDate)
                .HasColumnType("datetime2(7)")
                .IsRequired(false);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // 4. IAuditable 稽核欄位屬性設定 (帶時區之 DateTimeOffset 映射)
            builder.Property(x => x.CreatedAtUtc)
                .HasColumnType("datetimeoffset(7)")
                .HasDefaultValueSql("SYSDATETIMEOFFSET()")
                .IsRequired();

            builder.Property(x => x.CreatedBy)
                .HasMaxLength(100)
                .IsRequired(false);

            builder.Property(x => x.UpdatedAtUtc)
                .HasColumnType("datetimeoffset(7)")
                .IsRequired(false);

            builder.Property(x => x.UpdatedBy)
                .HasMaxLength(100)
                .IsRequired(false);

            // 5. 外鍵關係定義 (僅映射 Core 內部的 ApplicationUser 實體)
            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserLabMappings_Users_UserId");

            // 6. 索引設計 (針對外掛 LabId 與 TenantLabId 進行資料庫層級檢索優化)

            // MSSQL 2025 唯一性過濾索引：強制單一使用者在啟用狀態下僅能有一個 IsPrimary = 1 的主要實驗室
            builder.HasIndex(x => x.UserId)
                .IsUnique()
                .HasFilter("[IsPrimary] = 1 AND [IsActive] = 1")
                .HasDatabaseName("UX_UserLabMappings_OnePrimaryPerUser");

            // 多租戶（TenantLabId）快速查詢與資料隔離檢索索引
            builder.HasIndex(x => new { x.TenantLabId, x.UserId, x.IsActive })
                .HasDatabaseName("IX_UserLabMappings_TenantLabId_UserId");

            // 兼任與時效過濾檢索索引
            builder.HasIndex(x => new { x.UserId, x.IsActive, x.EffectiveDate, x.ExpiryDate })
                .HasDatabaseName("IX_UserLabMappings_EffectiveRange");
        }
    }
}
