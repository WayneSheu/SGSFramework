using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.Modules;
using System;

namespace SGSFramework.Core.Abstractions.Entities.Controller
{
    public class ControllerMetadata : IControllerMetadata
    {
        public Guid Id { get; set; }
        public string Version { get; set; } = "v1";
        public string ModuleName { get; set; } = string.Empty;
        public string ModuleTitle { get; set; } = string.Empty;
        public string ControllerTitle { get; set; } = string.Empty;
        public string ControllerName { get; set; } = string.Empty;
        public string ControllerTypeName { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public string RouteTemplate { get; set; } = string.Empty;
        public string? Description { get; set; }

        // 渲染欄位
        public string DisplayName { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public int DisplayOrder { get; set; }
        public string? ParentMenuName { get; set; }

        // === 新增：位元遮罩解耦與 Attributes 中繼資料 ===
        /// <summary>
        /// 該 Action 在模組內對應的位元位置 (0 ~ 63)，用於 Bitmask 快速運算
        /// </summary>
        public int? BitPosition { get; set; }

        /// <summary>
        /// 儲存該 Controller 或 Action 上掃描到的完整 Attributes 集合 (JSON 格式)
        /// </summary>
        public string? AttributesJson { get; set; }

        // 安全性欄位
        public string PermissionKey { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    public class ControllerMetadataConfig : IEntityTypeConfiguration<ControllerMetadata>
    {
        public void Configure(EntityTypeBuilder<ControllerMetadata> builder)
        {
            builder.ToTable("ControllerMetadatas");
            builder.HasKey(e => e.Id);

            builder.Property(x => x.ModuleName).IsRequired().HasMaxLength(50);
            builder.Property(x => x.ModuleTitle).IsRequired().HasMaxLength(100);
            builder.Property(x => x.ControllerTitle).IsRequired().HasMaxLength(100);
            builder.Property(x => x.ControllerName).IsRequired().HasMaxLength(100);
            builder.Property(x => x.RouteTemplate).IsRequired().HasMaxLength(250);
            builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
            builder.Property(x => x.PermissionKey).IsRequired().HasMaxLength(50);
            builder.Property(x => x.AttributesJson).HasColumnType("nvarchar(max)");

            builder.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(e => e.IsActive).HasDefaultValue(true);

            builder.HasIndex(x => x.PermissionKey).HasDatabaseName("IX_Metadata_PermissionKey");
            builder.HasIndex(x => x.ModuleName).HasDatabaseName("IX_Metadata_ModuleName");
            builder.HasIndex(x => new { x.ModuleName, x.ControllerName, x.ActionName })
                   .IsUnique()
                   .HasDatabaseName("UX_Module_Controller_Action");
        }
    }
}