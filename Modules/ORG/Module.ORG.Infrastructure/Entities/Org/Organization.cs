using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Entities.Hierarchical;
using SGSFramework.Core.Abstractions.Entities.Ledgers;
using SGSFramework.Core.Abstractions.Entities.SoftDelet;
using SGSFramework.Core.Helpers;
using System;
using System.Collections.Generic;

namespace SGS.Modules.ORG.Infrastructure.Entities.Org
{
    /// <summary>
    /// 組織/實驗室實體 (Hierarchical Entity)
    /// </summary>
    public class Organization : IHierarchicalEntity, IAuditable, ILedgerEntity, ISoftDeletable
    {
        public int Id { get; private set; }
        public int? ParentId { get; private set; }
        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;

        /// <summary>
        /// 僅 Level 2 區域實驗室實體可寫入 DB 的 TenantLabId
        /// </summary>
        public Guid? TenantLabId { get; private set; }

        public string? Location { get; private set; }
        public string? Description { get; private set; }
        public string NodePath { get; set; } = string.Empty;
        public int Level { get; private set; }

        /// <summary>
        /// 計算生效的 TenantLabId (如非 Level 2，則向父節點遞迴繼承)
        /// </summary>
        public Guid? EffectiveTenantLabId => TenantLabId ?? Parent?.EffectiveTenantLabId;

        public void UpdateLevel()
        {
            Level = NodePathHelper.CalculateLevel(NodePath);
        }

        public virtual Organization? Parent { get; private set; }
        public virtual IReadOnlyCollection<Organization> Children => _children.AsReadOnly();

        public string CreatedBy { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTimeOffset? UpdatedAtUtc { get; set; }
        public bool IsDeleted { get; set; }
        public string? DeletedBy { get; set; }
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