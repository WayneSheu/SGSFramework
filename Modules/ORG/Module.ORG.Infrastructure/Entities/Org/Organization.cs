using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Polly;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Entities.Hierarchical;
using SGSFramework.Core.Abstractions.Entities.Ledgers;
using SGSFramework.Core.Abstractions.Entities.SoftDelet;
using System.ComponentModel.DataAnnotations;

namespace SGS.Modules.ORG.Infrastructure.Entities.Org
{
    /// <summary>
    /// 組織/實驗室樹狀領域實體（使用 HierarchyId 支援無限層級與快速資料隔離查詢）
    /// 實作IHierarchicalEntity，將寫入權限收攏至實體內部
    /// </summary>
    public class Organization : IHierarchicalEntity, IAuditable, ILedgerEntity, ISoftDeletable
    {

        public int Id { get; private set; }
        public int? ParentId { get; private set; }
        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;

        /// <summary>
        /// 跨模組多租戶繫結：僅限 Level = 1 之實體實驗室有值
        /// </summary>
        public Guid? TenantLabId { get; private set; }
        public string? Location { get; private set; }
        public string? Description { get; private set; }

        public string NodePath { get; set; } = string.Empty;
        public int Level { get; set; }

        /// EF Core 自關聯導覽屬性 (Self-Referencing Navigation Properties)
        /// <summary>
        /// 父組織節點
        /// </summary>
        public virtual Organization? Parent { get; private set; }

        /// <summary>
        /// 子組織節點集合 (唯讀公開介面，防止外部直接變更 List)
        /// </summary>
        public virtual IReadOnlyCollection<Organization> Children => _children.AsReadOnly();

        // 審計與軟刪除欄位
        public string CreatedBy { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTimeOffset? UpdatedAtUtc { get; set; }
        public bool IsDeleted { get; set; }
        public string? DeletedBy { get; set; }
        public DateTimeOffset? DeletedOnUtc { get; set; }


        private readonly List<Organization> _children = new();

        // EF Core 專用建構子
        protected Organization() { }

        public Organization(
            string name,
            string code,
            int level,
            int? parentId = null,
            Guid? tenantLabId = null,
            string? location = null,
            string? description = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
            ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));

            if (level < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level), "Level 不能小於 0。");
            }

            Name = name;
            Code = code;
            Level = level;
            ParentId = parentId;
            Location = location;
            Description = description;

            SetTenantLabId(tenantLabId);
        }

        /// <summary>
        /// 設定 TenantLabId 領域邏輯驗證
        /// </summary>
        public void SetTenantLabId(Guid? tenantLabId)
        {
            if (!tenantLabId.HasValue)
            {
                TenantLabId = null;
                return;
            }

            if (Level != 1)
            {
                throw new InvalidOperationException($"僅限 Level = 1 (實體實驗室) 可設定 TenantLabId，當前節點 Level 為 {Level}。");
            }

            TenantLabId = tenantLabId;
        }

        /// <summary>
        /// 新增子節點 (自動維護父子雙向導覽屬性)
        /// </summary>
        public void AddChild(Organization child)
        {
            ArgumentNullException.ThrowIfNull(child, nameof(child));

            if (child.Id != 0 && child.Id == Id)
            {
                throw new InvalidOperationException("無法將自身設定為子節點。");
            }

            _children.Add(child);
            child.Parent = this;
            child.ParentId = Id;
        }

        // 封裝專屬的領域方法，確保 ParentId、NodePath 與 Level 同步更新
        public void MoveToParent(Organization? newParent)
        {
            if (newParent != null)
            {
                if (newParent.Id == Id)
                {
                    throw new InvalidOperationException("無法將節點移至自身下方。");
                }

                // 驗證是否造成循環引用 (檢查新父節點的路徑是否包含自身)
                if (newParent.NodePath.StartsWith($"{NodePath}/"))
                {
                    throw new InvalidOperationException("無法將父節點移動至其子孫節點下方 (循環引用)。");
                }

                ParentId = newParent.Id;
                Level = newParent.Level + 1;
                NodePath = $"{newParent.NodePath}/{Id}";
            }
            else
            {
                // 移至根節點 (Level = 0 或 1)
                ParentId = null;
                Level = 0;
                NodePath = $"/{Id}";
            }
        }
    }
    public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
    {
        public void Configure(EntityTypeBuilder<Organization> builder)
        {
            //顯式將 Organization 鎖定在 "org" Schema 下
            builder.ToTable("Organization", "org", t =>
            {
                // 資料庫 Check Constraint：確保 [TenantLabId] 僅在 [Level] = 1 時才可以不為 NULL
                t.HasCheckConstraint(
                    "CK_Organization_TenantLabId_Level1Only",
                    "([Level] = 1 AND [TenantLabId] IS NOT NULL) OR ([Level] <> 1 AND [TenantLabId] IS NULL) OR ([TenantLabId] IS NULL)"
                );
            });


            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.Code).IsUnique();
            builder.Property(x => x.Id)
                   .ValueGeneratedOnAdd(); // Id 自動增量
            builder.Property(x => x.Name)
                   .IsRequired()
                   .HasMaxLength(50);
            builder.Property(x => x.Code).HasMaxLength(20);
            builder.Property(x => x.Description).HasMaxLength(200);

            //樹狀自關聯導覽屬性映射 (Self-Referencing Mapping)
            builder.HasOne(x => x.Parent)
                   .WithMany(x => x.Children)
                   .HasForeignKey(x => x.ParentId)
                   .OnDelete(DeleteBehavior.Restrict) // 避免 DB 自動瀑布刪除
                   .IsRequired(false);

            // 針對 _children 私有欄位備份進行 Field 存取配置
            builder.Metadata.FindNavigation(nameof(Organization.Children))?
                   .SetPropertyAccessMode(PropertyAccessMode.Field);

            // 軟刪除全域過濾器
            builder.HasQueryFilter(x => !x.IsDeleted);


            // 確保 NodePath 映射到 MSSQL 的 hierarchyid 欄位，並建立索引以達最佳搜尋效能
            builder.Property(x => x.NodePath)
                   .IsRequired();
            // TenantLabId 建立索引，優化多租戶反查效能
            builder.HasIndex(x => x.TenantLabId)
                   .HasDatabaseName("IX_Organization_TenantLabId")
                   .HasFilter("[TenantLabId] IS NOT NULL");

        }
    }
}
