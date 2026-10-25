#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using SGSFramework.Core.Abstractions.Entities.Base;
    using System;
    using System.ComponentModel;

    /// <summary>
    /// 企業級自訂應用程式角色實體 (支援 ARBAC 與多租戶實驗室隔離)
    /// </summary>
    [DisplayName("企業級自訂應用程式角色實體 (支援 ARBAC 與多實驗室隔離)")]
    [Comment("企業級自訂應用程式角色實體 (支援 ARBAC 與多實驗室隔離)")]
    public class ApplicationRole : IdentityRole<Guid>, IRoleEntity
    {
        [DisplayName("角色代碼")]
        [Comment("角色代碼")]
        public string? Code { get; set; }

        [DisplayName("角色描述")]
        [Comment("角色描述")]   
        public string? Description { get; set; }

        /// <summary>
        /// 角色範圍類型 (SystemDefault, Global, LabScoped)
        /// </summary>
        [DisplayName("角色範圍類型 (SystemDefault, Global, LabScoped)")]
        [Comment("角色範圍類型 (SystemDefault, Global, LabScoped)")]
        public RoleScopeType ScopeType { get; set; } = RoleScopeType.LabScoped;

        /// <summary>
        /// 角色管理階層數值（數字越大位階越高。例: SA=100, MO=50, MA=10）
        /// </summary>
        [DisplayName("角色管理階層數值（數字越大位階越高。例: SA=100, MO=50, MA=10）")]
        [Comment("角色管理階層數值（數字越大位階越高。例: SA=100, MO=50, MA=10）")]
        public int HierarchyLevel { get; set; } = 10;

        /// <summary>
        /// 區域實驗室專屬角色的所屬租戶實驗室 ID（全域或系統角色則為 Null）
        /// </summary>
        [DisplayName("區域實驗室專屬角色的所屬租戶實驗室 ID（全域或系統角色則為 Null）")]
        [Comment("區域實驗室專屬角色的所屬租戶實驗室 ID（全域或系統角色則為 Null）")]
        public Guid? TenantLabId { get; set; }

        /// <summary>
        /// 是否為系統內建角色。預設為 false；系統初始化範本會明確設為 true。
        /// </summary>
        [DisplayName("是否為系統內建角色")]
        [Comment("是否為系統內建角色。預設為 false；系統初始化範本會明確設為 true。")]
        public bool IsSystemRole { get; set; } = false;

        [DisplayName("角色狀態")]
        [Comment("角色狀態(0 = 停用, 1 = 啟用中,2=已鎖定 ,3=待審核)")]  
        public RoleStatus Status { get; set; } = RoleStatus.Active;

        /// <summary>
        /// 稽核角色：權限僅於使用者具有效「稽核指派」（LabAuditAssignment，起訖期間內）的區域實驗室生效；
        /// 其他實驗室（含稽核員自己的主要實驗室）與全域範圍一律不授予。
        /// </summary>\
        [DisplayName("稽核角色")]
        [Comment("是否為稽核角色。預設為 false；僅在使用者具有效「稽核指派」的區域實驗室中生效。")]
        public bool RequiresAuditAssignment { get; set; }

        /// <summary>生效起日（含）；null 表示立即生效。生效期間外角色不授予任何權限</summary>
        [DisplayName("生效起日（含）；null 表示立即生效。生效期間外角色不授予任何權限")]
        [Comment("生效起日（含）；null 表示立即生效。生效期間外角色不授予任何權限")] 
        public DateTimeOffset? EffectiveFrom { get; set; }

        /// <summary>生效迄日（含）；null 表示無期限</summary>
        [DisplayName("生效迄日（含）；null 表示無期限")]
        [Comment("生效迄日（含）；null 表示無期限")]
        public DateTimeOffset? EffectiveTo { get; set; }

        [DisplayName("是否已刪除")]
        [Comment("是否已刪除")]  
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