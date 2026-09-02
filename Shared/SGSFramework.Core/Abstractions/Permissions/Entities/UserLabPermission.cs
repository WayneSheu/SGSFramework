using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Permissions.Identities
{

    /// <summary>
    /// 使用者實驗室層級 64 位元遮罩權限實體
    /// </summary>
    public class UserLabPermission
    {
        /// <summary>
        /// 唯一識別碼 (Primary Key)
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 使用者識別碼 (對應 ASP.NET Core Identity UserId)
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 租戶實驗室識別碼 (Tenant Lab ID)
        /// </summary>
        public Guid TenantLabId { get; set; }

        /// <summary>
        /// 控制器或模組識別 Key (對應 ControllerMetadata 或 Module Key)
        /// </summary>
        public string ControllerOrModuleKey { get; set; } = string.Empty;

        /// <summary>
        /// 64 位元權限遮罩值 (Bitmask 0-63)
        /// </summary>
        public long Bitmask { get; set; }

        /// <summary>
        /// 建立時間
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 最後修改時間
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
    }

    public class UserLabPermissionConfiguration : IEntityTypeConfiguration<UserLabPermission>
    {
        public void Configure(EntityTypeBuilder<UserLabPermission> builder)
        {
            builder.ToTable("User_Lab_Permissions");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .HasColumnName("id")
                .IsRequired();

            builder.Property(x => x.UserId)
                .HasColumnName("user_id")
                .HasMaxLength(450)
                .IsRequired();

            builder.Property(x => x.TenantLabId)
                .HasColumnName("tenant_lab_id")
                .IsRequired();

            builder.Property(x => x.ControllerOrModuleKey)
                .HasColumnName("controller_or_module_key")
                .HasMaxLength(150)
                .IsRequired();

            builder.Property(x => x.Bitmask)
                .HasColumnName("bitmask")
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at");

            // 建立複合唯一索引，確保同一使用者在同一實驗室下的同一個模組/控制器權限記錄唯一
            builder.HasIndex(x => new { x.UserId, x.TenantLabId, x.ControllerOrModuleKey })
                .IsUnique()
                .HasDatabaseName("ix_user_lab_permissions_user_lab_controller");

            // 額外針對 TenantLabId 與 UserId 建立效能索引，加快多租戶權限過濾查詢
            builder.HasIndex(x => new { x.UserId, x.TenantLabId })
                .HasDatabaseName("ix_user_lab_permissions_user_tenant");
        }
    }

}
