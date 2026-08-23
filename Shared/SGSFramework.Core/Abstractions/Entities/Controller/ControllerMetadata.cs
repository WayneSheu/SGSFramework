using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.Modules;
using System;
using System.Collections.Generic;
using System.Text;

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

        // 安全性欄位
        public string PermissionKey { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    public class ControllerMetadataConfig : IEntityTypeConfiguration<ControllerMetadata>
    {
        public void Configure(EntityTypeBuilder<ControllerMetadata> builder)
        {
            //指定資料表名稱
            builder.ToTable("ControllerMetadatas");

            //主鍵設定
            builder.HasKey(e => e.Id);

            //欄位長度限制與必要性 (防禦性設計)
            builder.Property(x => x.ModuleName).IsRequired().HasMaxLength(50);
            builder.Property(x => x.ModuleTitle).IsRequired().HasMaxLength(100);
            builder.Property(x => x.ControllerTitle).IsRequired().HasMaxLength(100);
            builder.Property(x => x.ControllerName).IsRequired().HasMaxLength(100);
            builder.Property(x => x.RouteTemplate).IsRequired().HasMaxLength(250);
            builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
            builder.Property(x => x.PermissionKey).IsRequired().HasMaxLength(50);
            // 5. 預設值與審計欄位
            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
            builder.Property(e => e.IsActive)
                .HasDefaultValue(true);

            //效能索引優化：快速進行權限查找與選單渲染
            builder.HasIndex(x => x.PermissionKey).HasDatabaseName("IX_Metadata_PermissionKey");
            builder.HasIndex(x => x.ModuleName).HasDatabaseName("IX_Metadata_ModuleName");

            // 修正後：提升唯一索引粒度至 Module + Controller + Action
            builder.HasIndex(x => new { x.ModuleName, x.ControllerName, x.ActionName })
                   .IsUnique()
                   .HasDatabaseName("UX_Module_Controller_Action");

        }
    }
}
