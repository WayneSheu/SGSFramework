using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities;
using System.Collections.Generic;
using SGSFramework.Core.Abstractions.Entities.Hierarchical;

namespace SGSFramework.Core.Abstractions.Permissions.Entities
{

    /// <summary>
    /// 多維度權限維度矩陣-功能權限維度
    /// 整合 IHierarchicalEntity 支援階層樹狀結構，並對應 ControllerMetadatas 的標題與描述。
    /// </summary>
    public class PermissionMetadata : IHierarchicalEntity
    {
        public int Id { get; set; }

        // 階層架構屬性 (IHierarchicalEntity<int>)
        public int? ParentId { get; set; }
        public PermissionMetadata? Parent { get; set; }
        public ICollection<PermissionMetadata> Children { get; set; } = new List<PermissionMetadata>();
        public string NodePath { get; set; } = string.Empty;
        public int Level { get; set; }

        // 權限代碼，例如: "ORG_LAB_READ"
        public string PermissionKey { get; set; } = string.Empty;

        // 位元位置，用於計算 1L << BitPosition
        public int BitPosition { get; set; }

        // 模組與控制器中繼資料 (對應 ControllerMetadatas)
        public string ModuleName { get; set; } = string.Empty;
        public string? ModuleTitle { get; set; }
        public string ControllerName { get; set; } = string.Empty;
        public string? ControllerTitle { get; set; }
        public string ActionName { get; set; } = string.Empty;

        // 描述，用於後台 UI 顯示
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 指派或變更父節點，並自動重新計算階層深度與物化路徑
        /// </summary>
        public void AssignParent(PermissionMetadata? parent)
        {
            Parent = parent;
            ParentId = parent?.Id;
            RecalculateHierarchy();
        }

        /// <summary>
        /// 內部核心：依據父節點狀態自動計算 Level 與 NodePath
        /// </summary>
        public void RecalculateHierarchy()
        {
            if (Parent == null)
            {
                Level = 0; // 根節點深度為 0 (或 1，視系統定義而定)
                NodePath = Id > 0 ? Id.ToString() : PermissionKey;
            }
            else
            {
                Level = Parent.Level + 1;
                NodePath = $"{Parent.NodePath}/{(Id > 0 ? Id.ToString() : PermissionKey)}";
            }
        }
    }

    public class PermissionConfiguration : IEntityTypeConfiguration<PermissionMetadata>
    {
        public void Configure(EntityTypeBuilder<PermissionMetadata> builder)
        {
            builder.ToTable("PermissionMetadata", "core");

            builder.HasKey(x => x.Id);

            // 確保權限代碼唯一
            builder.HasIndex(x => x.PermissionKey).IsUnique();
            builder.Property(x => x.PermissionKey)
                .IsRequired()
                .HasMaxLength(128);

            // 確保位元位置唯一
            builder.HasIndex(x => x.BitPosition).IsUnique();
            builder.Property(x => x.BitPosition)
                .IsRequired();

            builder.Property(x => x.ModuleName)
                .HasMaxLength(128);

            builder.Property(x => x.ModuleTitle)
                .HasMaxLength(128);

            builder.Property(x => x.ControllerName)
                .HasMaxLength(128);

            builder.Property(x => x.ControllerTitle)
                .HasMaxLength(128);

            builder.Property(x => x.Description)
                .HasMaxLength(256);

            // 自我參考階層架構設定 (IHierarchicalEntity)
            builder.HasOne(e => e.Parent)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}