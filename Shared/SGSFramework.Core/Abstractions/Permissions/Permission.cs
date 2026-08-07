using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Permissions
{
    public class Permission
    {

            // 主鍵
            public int Id { get; set; }
            // 權限代碼，例如: "ORG_LAB_READ" (對應 RequiresPermissionAttribute 的 PermissionKey)
            public string PermissionKey { get; set; } = string.Empty;

            // 位元位置 (0 到 62)，用於計算 1L << BitPosition
            public int BitPosition { get; set; }
            // 對應的控制器名稱和方法名稱，用於動態生成權限列表
            public string ModuleName { get; set; } = string.Empty; 
            public string ControllerName { get; set; } = string.Empty;
            public string ActionName { get; set; } = string.Empty;

            // 描述，用於後台 UI 顯示
            public string Description { get; set; } = string.Empty;
        
    }


    public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
    {
        public void Configure(EntityTypeBuilder<Permission> builder)
        {
            builder.ToTable("Auth_Permissions"); // 建議加上命名空間或模組前綴

            builder.HasKey(x => x.Id);

            // 確保權限代碼唯一，這是動態權限系統的關鍵
            builder.HasIndex(x => x.PermissionKey).IsUnique();
            builder.Property(x => x.PermissionKey)
                .IsRequired()
                .HasMaxLength(128);

            // 確保位元位置唯一，防止多個 Key 指向同一個 Bit
            builder.HasIndex(x => x.BitPosition).IsUnique();
            builder.Property(x => x.BitPosition)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(256);
        }
    }

}
