// ==========================================
// 檔案路徑: src/SGSFramework.Core/Entities/Permissions/UserLabPermission.cs
// 架構層級: Core Domain / Entities
// ==========================================

#nullable enable

namespace SGSFramework.Core.Abstractions.Permissions.Identities
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using SGSFramework.Core.Abstractions.Entities.AuditLogs;
    using System;

    /// <summary>
    /// 優化後的使用者實驗室專屬 64 位元遮罩權限實體 (對齊 UserLabMapping 型別與稽核標準)
    /// </summary>
    public class UserLabPermission : IAuditable
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid UserId { get; private set; }
        public int LabId { get; private set; }
        public Guid TenantLabId { get; private set; }
        public string ControllerOrModuleKey { get; private set; } = string.Empty;
        public long Bitmask { get; private set; }

        // --- IAuditable 實作 ---
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset? UpdatedAtUtc { get; set; }
        public string? UpdatedBy { get; set; }

        private UserLabPermission() { }

        public static UserLabPermission Create(
            Guid userId,
            int labId,
            Guid tenantLabId,
            string controllerOrModuleKey,
            long bitmask,
            string? operatorId = null)
        {
            if (userId == Guid.Empty) throw new ArgumentException("UserId 不能為 Empty Guid。", nameof(userId));
            if (labId <= 0) throw new ArgumentOutOfRangeException(nameof(labId), "LabId 必須大於 0。");
            if (tenantLabId == Guid.Empty) throw new ArgumentException("TenantLabId 不能為 Empty Guid。", nameof(tenantLabId));
            ArgumentException.ThrowIfNullOrWhiteSpace(controllerOrModuleKey);

            var nowUtc = DateTimeOffset.UtcNow;
            return new UserLabPermission
            {
                UserId = userId,
                LabId = labId,
                TenantLabId = tenantLabId,
                ControllerOrModuleKey = controllerOrModuleKey.Trim().ToUpperInvariant(),
                Bitmask = bitmask,
                CreatedAtUtc = nowUtc,
                CreatedBy = operatorId
            };
        }

        public void UpdateBitmask(long newBitmask, string? operatorId = null)
        {
            Bitmask = newBitmask;
            UpdatedAtUtc = DateTimeOffset.UtcNow;
            UpdatedBy = operatorId;
        }
    }

    public class UserLabPermissionConfiguration : IEntityTypeConfiguration<UserLabPermission>
    {
        public void Configure(EntityTypeBuilder<UserLabPermission> builder)
        {
            builder.ToTable("UserLabPermissions", "core");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.LabId)
                .IsRequired();

            builder.Property(x => x.TenantLabId)
                .IsRequired();

            builder.Property(x => x.ControllerOrModuleKey)
                .HasMaxLength(150)
                .IsRequired();

            builder.Property(x => x.Bitmask)
                .IsRequired();

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

            // 複合唯一索引：確保同一使用者在同一實驗室下的同一個模組/控制器權限記錄唯一
            builder.HasIndex(x => new { x.UserId, x.LabId, x.ControllerOrModuleKey })
                .IsUnique()
                .HasDatabaseName("UX_UserLabPermissions_User_Lab_Module");

            //高效能關聯索引
            builder.HasIndex(x => new { x.UserId, x.TenantLabId })
                .HasDatabaseName("IX_UserLabPermissions_UserId_TenantLabId");
        }
    }
}