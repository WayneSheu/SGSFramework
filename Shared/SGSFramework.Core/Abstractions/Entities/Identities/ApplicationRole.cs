#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using SGSFramework.Core.Abstractions.Entities.Base;
    using System;

    /// <summary>
    /// 企業級自訂應用程式角色實體 (支援 ARBAC 與多租戶實驗室隔離)
    /// </summary>
    public class ApplicationRole : IdentityRole<Guid>, IRoleEntity
    {
        public string? Code { get; set; }
        public string? Description { get; set; }

        /// <summary>
        /// 角色範圍類型 (SystemDefault, Global, LabScoped)
        /// </summary>
        public RoleScopeType ScopeType { get; set; } = RoleScopeType.LabScoped;

        /// <summary>
        /// 角色管理階層數值（數字越大位階越高。例: SA=100, MO=50, MA=10）
        /// </summary>
        public int HierarchyLevel { get; set; } = 10;

        /// <summary>
        /// 區域實驗室專屬角色的所屬租戶實驗室 ID（全域或系統角色則為 Null）
        /// </summary>
        public Guid? TenantLabId { get; set; }

        /// <summary>
        /// 是否為系統內建角色。預設為 false；系統初始化範本會明確設為 true。
        /// </summary>
        public bool IsSystemRole { get; set; } = false;

        public RoleStatus Status { get; set; } = RoleStatus.Active;

        /// <summary>
        /// 稽核角色：權限僅於使用者具有效「稽核指派」（LabAuditAssignment，起訖期間內）的區域實驗室生效；
        /// 其他實驗室（含稽核員自己的主要實驗室）與全域範圍一律不授予。
        /// </summary>
        public bool RequiresAuditAssignment { get; set; }

        /// <summary>生效起日（含）；null 表示立即生效。生效期間外角色不授予任何權限</summary>
        public DateTimeOffset? EffectiveFrom { get; set; }

        /// <summary>生效迄日（含）；null 表示無期限</summary>
        public DateTimeOffset? EffectiveTo { get; set; }

        public bool IsDeleted { get; set; } = false;
    }

    /// <summary>
    /// ApplicationRole 實體資料庫映射組態
    /// </summary>
    public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
    {
        public void Configure(EntityTypeBuilder<ApplicationRole> builder)
        {
            builder.ToTable("AspNetRoles");

            builder.HasKey(r => r.Id);

            builder.Property(r => r.Name)
                .HasMaxLength(256)
                .IsRequired();

            builder.Property(r => r.NormalizedName)
                .HasMaxLength(256)
                .IsRequired();

            builder.Property(r => r.Code)
                .HasMaxLength(64)
                .IsRequired(false);

            builder.Property(r => r.Description)
                .HasMaxLength(512)
                .IsRequired(false);

            builder.Property(r => r.ScopeType)
                .IsRequired()
                .HasDefaultValue(RoleScopeType.LabScoped);

            builder.Property(r => r.HierarchyLevel)
                .IsRequired()
                .HasDefaultValue(10);

            builder.Property(r => r.TenantLabId)
                .IsRequired(false);

            builder.Property(r => r.IsSystemRole)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(r => r.RequiresAuditAssignment)
                .IsRequired()
                .HasDefaultValue(false);

            // 索引優化：針對 Code 建立唯一索引以確保系統角色代碼不重複
            builder.HasIndex(r => r.Code)
                .IsUnique()
                .HasFilter("[Code] IS NOT NULL");

            // 效能索引：加速依據實驗室與階層的查詢
            builder.HasIndex(r => new { r.TenantLabId, r.HierarchyLevel });
        }
    }
}