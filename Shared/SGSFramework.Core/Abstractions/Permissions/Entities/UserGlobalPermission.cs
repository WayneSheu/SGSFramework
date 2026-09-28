// ==========================================
// 檔案路徑: src/SGSFramework.Core/Entities/Permissions/UserGlobalPermission.cs
// 架構層級: Core Domain / Entities
// ==========================================

#nullable enable

namespace SGSFramework.Core.Abstractions.Permissions.Identities
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using SGSFramework.Core.Abstractions.Attributes;
    using SGSFramework.Core.Abstractions.Entities.AuditLogs;
    using System;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// 使用者全域/組織級 64 位元遮罩權限實體 (統一 UserId 為 Guid 型別)
    /// </summary>
    public class UserGlobalPermission : IAuditable
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid UserId { get; private set; }
        public string PermissionKey { get; private set; } = string.Empty;
        public long Bitmask { get; private set; }

        private UserGlobalPermission() { }

        public static UserGlobalPermission Create(
            Guid userId,
            string permissionKey,
            long bitmask,
            string? operatorId = null)
        {
            if (userId == Guid.Empty) throw new ArgumentException("UserId 不能為 Empty Guid。", nameof(userId));
            ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

            var nowUtc = DateTimeOffset.UtcNow;
            return new UserGlobalPermission
            {
                UserId = userId,
                PermissionKey = permissionKey.Trim().ToUpperInvariant(),
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

        // --- IAuditable 實作 ---
        [AuditIgnore]
        [Editable(false)]
        public DateTimeOffset CreatedAtUtc { get; set; }

        [AuditIgnore]
        [Editable(false)]
        public string? CreatedBy { get; set; }

        [AuditIgnore]
        public DateTimeOffset? UpdatedAtUtc { get; set; }

        [AuditIgnore]
        public string? UpdatedBy { get; set; }
    }

    public class UserGlobalPermissionConfiguration : IEntityTypeConfiguration<UserGlobalPermission>
    {
        public void Configure(EntityTypeBuilder<UserGlobalPermission> builder)
        {
            builder.ToTable("UserGlobalPermissions", "core");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.PermissionKey)
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

            // 建立複合唯一索引，確保同一使用者不會重複擁有一致的全域權限 Key 紀錄
            builder.HasIndex(x => new { x.UserId, x.PermissionKey })
                .IsUnique()
                .HasDatabaseName("UX_UserGlobalPermissions_UserId_PermissionKey");
        }
    }
}