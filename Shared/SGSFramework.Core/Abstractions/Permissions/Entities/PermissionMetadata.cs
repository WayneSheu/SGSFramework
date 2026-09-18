// ==========================================
// 檔案路徑: src/SGSFramework/Core/SGSFramework.Core.Abstractions/Permissions/Entities/PermissionMetadata.cs
// 架構層級: Domain / Abstractions Layer
// ==========================================

namespace SGSFramework.Core.Abstractions.Permissions.Entities
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using SGSFramework.Core.Abstractions.Entities.Hierarchical;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations.Schema;

    /// <summary>
    /// 多維度權限維度矩陣-功能權限維度
    /// 整合 IHierarchicalEntity 支援階層樹狀結構，並對應 ControllerMetadatas 的標題與描述。
    /// </summary>
    public class PermissionMetadata : IHierarchicalEntity
    {
        [Column(Order = 0)]
        public int Id { get; set; }

        [Column(Order = 1)]
        public string ModuleName { get; set; } = string.Empty;

        [Column(Order = 2)]
        public string? ModuleTitle { get; set; }

        [Column(Order = 3)]
        public string ControllerName { get; set; } = string.Empty;

        [Column(Order = 4)]
        public string? ControllerTitle { get; set; }

        [Column(Order = 5)]
        public string ActionName { get; set; } = string.Empty;

        [Column(Order = 6)]
        public string ActionTitle { get; set; } = string.Empty;

        [Column(Order = 7)]
        public string Description { get; set; } = string.Empty;

        [Column(Order = 8)]
        public string PermissionTitle { get; set; } = string.Empty;

        [Column(Order = 9)]
        public string PermissionKey { get; set; } = string.Empty;

        [Column(Order = 10)]
        public int BitPosition { get; set; }

        [Column(Order = 11)]
        public int? ParentId { get; set; }

        public PermissionMetadata? Parent { get; set; }

        public ICollection<PermissionMetadata> Children { get; set; } = new List<PermissionMetadata>();

        [Column(Order = 12)]
        public string NodePath { get; set; } = string.Empty;

        [Column(Order = 13)]
        public int Level { get; set; }

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
                Level = 0;
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

            // 調整索引：因為多個 Action 可以共用同一個 PermissionKey，改為複合唯一索引 (PermissionKey + ControllerName + ActionName)
            builder.HasIndex(x => new { x.PermissionKey, x.ControllerName, x.ActionName }).IsUnique();

            builder.Property(x => x.PermissionKey)
                .IsRequired()
                .HasMaxLength(128);

            // BitPosition 允許不同權限共用（例如同群組 Action），故移除全域 IsUnique()，改由程式邏輯控管衝突
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