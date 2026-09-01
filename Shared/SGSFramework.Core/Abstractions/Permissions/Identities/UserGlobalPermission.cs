using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Permissions.Identities
{
    /// <summary>
    /// 使用者全域/組織級 64 位元遮罩權限實體
    /// </summary>
    public class UserGlobalPermission
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
        /// 全域權限或模組識別 Key
        /// </summary>
        public string PermissionKey { get; set; } = string.Empty;

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

    public class UserGlobalPermissionConfiguration : IEntityTypeConfiguration<UserGlobalPermission>
    {
        public void Configure(EntityTypeBuilder<UserGlobalPermission> builder)
        {
            builder.ToTable("User_Global_Permissions");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .HasColumnName("id")
                .IsRequired();

            builder.Property(x => x.UserId)
                .HasColumnName("user_id")
                .HasMaxLength(450)
                .IsRequired();

            builder.Property(x => x.PermissionKey)
                .HasColumnName("permission_key")
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

            // 建立複合唯一索引，確保同一使用者不會重複擁有一致的全域權限 Key 紀錄
            builder.HasIndex(x => new { x.UserId, x.PermissionKey })
                .IsUnique()
                .HasDatabaseName("ix_user_global_permissions_user_key");
        }
    }
}
