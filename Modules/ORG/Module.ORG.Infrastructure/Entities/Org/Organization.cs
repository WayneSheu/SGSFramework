using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Entities.Hierarchical;
using SGSFramework.Core.Abstractions.Entities.Ledgers;
using SGSFramework.Core.Abstractions.Entities.SoftDelet;
using SGSFramework.Core.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SGS.Modules.ORG.Infrastructure.Entities.Org
{
    /// <summary>
    /// 組織/實驗室實體 (Hierarchical Entity)
    /// </summary>
    [DisplayName("組織/實驗室實體 (Hierarchical Entity)")]
    [Comment("組織/實驗室實體 (Hierarchical Entity)")]
    public class Organization : IHierarchicalEntity, IAuditable, ILedgerEntity, ISoftDeletable
    {
        [DisplayName("組織/實驗室 ID")]
        [Comment("組織/實驗室 ID")]
        public int Id { get; private set; }
        
        [DisplayName("父節點 ID")]
        [Comment("父節點 ID")]
        public int? ParentId { get; private set; }
        
        [DisplayName("組織/實驗室代碼")]
        [Comment("組織/實驗室代碼")]
        public string Code { get; private set; } = string.Empty;
        
        [DisplayName("組織/實驗室名稱")]
        [Comment("組織/實驗室名稱")]
        public string Name { get; private set; } = string.Empty;

        /// <summary>
        /// 僅 Level 2 區域實驗室實體可寫入 DB 的 TenantLabId
        /// </summary>
        [DisplayName("僅 Level 2 區域實驗室實體可寫入 DB 的 TenantLabId")]
        [Comment("僅 Level 2 區域實驗室實體可寫入 DB 的 TenantLabId")]
        public Guid? TenantLabId { get; private set; }

        [DisplayName("組織/實驗室位置")]
        [Comment("組織/實驗室位置")]
        public string? Location { get; private set; }

        [DisplayName("組織/實驗室描述")]
        [Comment("組織/實驗室描述")]   
        public string? Description { get; private set; }

        [DisplayName("節點路徑")]
        [Comment("節點路徑")]   
        public string NodePath { get; set; } = string.Empty;

        [DisplayName("層級 (Level)")]
        [Comment("層級 (Level)")] 
        public int Level { get; private set; }

        [DisplayName("是否啟用")]
        [Comment("是否啟用")]   
        public bool IsActive { get; private set; } = true;

        /// <summary>
        /// 計算生效的 TenantLabId (如非 Level 2，則向父節點遞迴繼承)
        /// </summary>
        [DisplayName("計算生效的 TenantLabId (如非 Level 2，則向父節點遞迴繼承)")]
        [Comment("計算生效的 TenantLabId (如非 Level 2，則向父節點遞迴繼承)")]
        public Guid? EffectiveTenantLabId => TenantLabId ?? Parent?.EffectiveTenantLabId;

        public void UpdateLevel()
        {
            Level = NodePathHelper.CalculateLevel(NodePath);
        }

        public virtual Organization? Parent { get; private set; }
        public virtual IReadOnlyCollection<Organization> Children => _children.AsReadOnly();

        [DisplayName("建立者")]
        [Comment("建立者")]    
        public string CreatedBy { get; set; } = string.Empty;

        [DisplayName("建立時間")]
        [Comment("建立時間")]   
        public DateTimeOffset CreatedAtUtc { get; set; }

        [DisplayName("更新者")]
        [Comment("更新者")]    
        public string? UpdatedBy { get; set; }

        [DisplayName("更新時間")]
        [Comment("更新時間")]
        public DateTimeOffset? UpdatedAtUtc { get; set; }

        [DisplayName("是否已刪除")]
        [Comment("是否已刪除")]  
        public bool IsDeleted { get; set; }

        [DisplayName("刪除者")]
        [Comment("刪除者")]
        public string? DeletedBy { get; set; }

        [DisplayName("刪除時間")]
        [Comment("刪除時間")]
        public DateTimeOffset? DeletedOnUtc { get; set; }

        private readonly List<Organization> _children = new();

#pragma warning disable CS8618
        protected Organization() { }
#pragma warning restore CS8618

        /// <summary>
        /// 工廠方法：建立組織/實驗室實體
        /// </summary>
        public static Organization Create(OrganizationCreateContext context)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));
            ArgumentException.ThrowIfNullOrWhiteSpace(context.Name, nameof(context.Name));
            ArgumentException.ThrowIfNullOrWhiteSpace(context.Code, nameof(context.Code));

            // 1. 先計算預期的 Level (父節點斜線數 + 1)
            int expectedLevel = NodePathHelper.CalculateLevel(context.ParentNodePath) + 1;

            var entity = new Organization
            {
                Name = context.Name,
                Code = context.Code,
                ParentId = context.ParentId,
                Location = context.Location,
                Description = context.Description,
                Level = expectedLevel // 🔑 必須在 Initial SaveChanges 前指定正確的 Level
            };

            // 2. 設定 TenantLabId (傳入 expectedLevel 確保非 Level 2 被強制設為 null)
            entity.SetTenantLabId(context.TenantLabId, expectedLevel);
            return entity;
        }

        /// <summary>
        /// 設定或清除 TenantLabId（自動進行安全條件判斷，非 Level 2 一律抹除為 null）
        /// </summary>
        public void SetTenantLabId(Guid? tenantLabId, int? targetLevel = null)
        {
            int currentLevel = targetLevel ?? Level;

            // 防禦邏輯：非 Level 2 節點，TenantLabId 一律強制降為 null，防止 DB Check Constraint 衝突
            if (currentLevel != 2 || (tenantLabId.HasValue && tenantLabId.Value == Guid.Empty))
            {
                TenantLabId = null;
                return;
            }

            TenantLabId = tenantLabId;
        }

        /// <summary>
        /// 更新實驗室基本資訊
        /// </summary>
        public void UpdateDetails(string name, string? description, string? location)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("實驗室名稱不可為空。", nameof(name));
            }

            Name = name.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        }

        // 業務行為：停用組織
        public void Deactivate(string reason)
        {
            if (!IsActive) return;

            IsActive = false;
            // 可加載 Domain Event，如：UnregisterActiveUserSessionsEvent
        }

        // 業務行為：重新啟用組織
        public void Activate()
        {
            if (IsDeleted)
                throw new InvalidOperationException("無法啟用已刪除的組織。");

            IsActive = true;
        }
    }

    public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
    {
        public void Configure(EntityTypeBuilder<Organization> builder)
        {
            builder.ToTable("Organization", "org", t =>
            {
                // DB CHECK 約束比對 [Level] 欄位
                t.HasCheckConstraint(
                    "CK_Organization_TenantLabId_Level2Only",
                    "([TenantLabId] IS NULL) OR ([Level] = 2)"
                );
            });

            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.Code).IsUnique();
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.Name).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Code).HasMaxLength(20);
            builder.Property(x => x.Description).HasMaxLength(200);
            // 強制設定非空與預設值
            builder.Property(x => x.IsActive)
                   .IsRequired()
                   .HasDefaultValue(true);

            builder.Property(x => x.Level)
                   .IsRequired()
                   .HasDefaultValue(0);

            builder.Ignore(x => x.EffectiveTenantLabId);

            builder.HasOne(x => x.Parent)
                   .WithMany(x => x.Children)
                   .HasForeignKey(x => x.ParentId)
                   .OnDelete(DeleteBehavior.Restrict)
                   .IsRequired(false);

            builder.Metadata.FindNavigation(nameof(Organization.Children))?
                   .SetPropertyAccessMode(PropertyAccessMode.Field);

            builder.HasQueryFilter(x => !x.IsDeleted);

            builder.Property(x => x.NodePath).IsRequired();

            builder.HasIndex(x => x.TenantLabId)
                   .HasDatabaseName("IX_Organization_TenantLabId")
                   .HasFilter("[TenantLabId] IS NOT NULL");
        }
    }
}