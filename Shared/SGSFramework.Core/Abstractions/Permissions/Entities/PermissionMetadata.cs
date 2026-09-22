
namespace SGSFramework.Core.Abstractions.Permissions.Entities
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using SGSFramework.Core.Abstractions.Entities.Hierarchical;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations.Schema;

    /// <summary>
    /// 多維度權限維度矩陣-功能權限維度
    /// 整合 IHierarchicalEntity 支援階層樹狀結構，並對應 ControllerMetadatas 的標題與描述。
    /// </summary>
    [Comment("權限中繼資料實體")]
    public class PermissionMetadata : IHierarchicalEntity
    {
        [DisplayName("主鍵")]
        [Comment("主鍵")]
        public int Id { get; set; }

        /// <summary>
        /// 模組名稱，對應ControllerMetadatas 的ControllerName。
        /// </summary>
        [DisplayName("模組名稱")]
        [Comment("模組名稱，對應ControllerMetadatas 的ModuleName，例如SGSFramework.System、SGSFramework.System。")] 
        public string ModuleName { get; set; } = string.Empty;


        /// <summary>
        /// 模組標題，對應ControllerMetadatas 的ControllerTitle。
        /// </summary>
        [DisplayName("模組標題")]
        [Comment("模組標題，對應ControllerMetadatas 的ModuleTitle，例如系統管理、組織管理。")]
        public string? ModuleTitle { get; set; }

        /// <summary>
        /// 功能名稱，對應ControllerMetadatas 的ControllerName。
        /// </summary>
        [DisplayName("功能名稱")]
        [Comment("功能名稱，對應ControllerMetadatas 的ControllerName。")]
        public string ControllerName { get; set; } = string.Empty;

        /// <summary>
        /// 功能標題，對應ControllerMetadatas 的ControllerTitle。
        /// </summary>
        [DisplayName("功能標題")]
        [Comment("功能標題，對應ControllerMetadatas 的ControllerTitle。")]
        public string? ControllerTitle { get; set; }

        /// <summary>
        /// 操作名稱，對應ControllerMetadatas 的ActionName。
        /// </summary>
        [DisplayName("操作名稱")]
        [Comment("操作名稱，對應ControllerMetadatas 的ActionName。")]
        public string ActionName { get; set; } = string.Empty;

        /// <summary>
        /// 操作標題，對應ControllerMetadatas 的ActionTitle。
        /// </summary>
        [DisplayName("操作標題")]
        [Comment("操作標題，對應ControllerMetadatas 的ActionTitle。")]
        public string ActionTitle { get; set; } = string.Empty;

        /// <summary>
        /// 操作說明，對應ControllerMetadatas 的Description。
        /// </summary>
        [DisplayName("操作說明")]
        [Comment("操作說明，對應ControllerMetadatas 的Description。")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 權限標題，對應ControllerMetadatas 的PermissionTitle。
        /// </summary>
        [DisplayName("權限標題")]
        [Comment("權限標題，對應ControllerMetadatas 的PermissionTitle。")]
        public string PermissionTitle { get; set; } = string.Empty;

        /// <summary>
        /// 權限代碼，對應ControllerMetadatas 的PermissionKey。
        /// </summary>
        [DisplayName("權限代碼")]
        [Comment("權限代碼，對應ControllerMetadatas 的PermissionKey。")]
        public string PermissionKey { get; set; } = string.Empty;

        /// <summary>
        /// 位址權限，對應ControllerMetadatas 的BitPosition。
        /// </summary>
        [DisplayName("位址權限")]
        [Comment("位址權限，對應ControllerMetadatas 的BitPosition。")]
        public int BitPosition { get; set; }

        /// <summary>
        /// 父節點ID
        /// </summary>
        [DisplayName("父節點ID")]
        [Comment("父節點ID")]
        public int? ParentId { get; set; }

        /// <summary>
        /// 父節點
        /// </summary>
        [DisplayName("父節點")]
        [Comment("父節點")]
        public PermissionMetadata? Parent { get; set; }

        /// <summary>
        /// 子節點集
        /// </summary>
        [DisplayName("子節點集")]
        [Comment("子節點集")]
        public ICollection<PermissionMetadata> Childrens { get; set; } = new List<PermissionMetadata>();

        /// <summary>
        /// 物化路徑，例如：1/2/3/4/5/
        /// </summary>
        [DisplayName("物化路徑")]
        [Comment("物化路徑，例如：1/2/3/4/5/")]
        public string NodePath { get; set; } = string.Empty;

        /// <summary>
        /// 階層深度
        /// </summary>
        [DisplayName("階層深度")]
        [Comment("階層深度")]
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
                  .WithMany(e => e.Childrens)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}