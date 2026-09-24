using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Core.Abstractions.Permissions.Entities
{
    /// <summary>
    /// 角色全域/模組級 64 位元遮罩權限實體
    /// </summary>
    public class RoleGlobalPermission : IAuditable
    {
        /// <summary>
        /// 唯一識別碼 (Primary Key)
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 角色識別碼 (對應 ASP.NET Core Identity RoleId)
        /// </summary>
        public string RoleId { get; set; } = string.Empty;

        /// <summary>
        /// 全域權限或模組識別 Key (例如: SYSTEM.ROLE.MANAGE)
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

        [AuditIgnore]
        [Editable(false)]
        public string CreatedBy { get; set; } = string.Empty;

        [AuditIgnore]
        [Editable(false)]
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        [AuditIgnore]
        public string? UpdatedBy { get; set; }

        [AuditIgnore]
        public DateTimeOffset? UpdatedAtUtc { get; set; }
    }
}

/// <summary>
/// RoleGlobalPermission 實體的 EF Core Fluent API 設定與效能優化配置
/// </summary>
public sealed class RoleGlobalPermissionConfiguration : IEntityTypeConfiguration<RoleGlobalPermission>
{
    public void Configure(EntityTypeBuilder<RoleGlobalPermission> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // 1. 資料表名稱設定
        builder.ToTable("Role_Global_Permissions");

        // 2. 主鍵設定
        builder.HasKey(x => x.Id);

        // 3. 欄位映射與型別/長度約束設定
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(x => x.RoleId)
            .HasColumnName("role_id")
            .HasMaxLength(450)
            .IsUnicode(false) // 設為 VARCHAR，節省儲存空間並提升 Index 搜尋效能
            .IsRequired();

        builder.Property(x => x.PermissionKey)
            .HasColumnName("permission_key")
            .HasMaxLength(150)
            .IsUnicode(false) // 設為 VARCHAR，用於英數字與點號組合之權限 Key
            .IsRequired();

        builder.Property(x => x.Bitmask)
            .HasColumnName("bitmask")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // 4. IAuditable 稽核欄位映射
        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256)
            .IsUnicode(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256)
            .IsUnicode(false);

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("datetimeoffset");

        // 5. 複合唯一索引優化 (Ensure Unique Bitmask per Role and Permission Key)
        builder.HasIndex(x => new { x.RoleId, x.PermissionKey })
            .IsUnique()
            .HasDatabaseName("ix_role_global_permissions_role_key");
    }
}
