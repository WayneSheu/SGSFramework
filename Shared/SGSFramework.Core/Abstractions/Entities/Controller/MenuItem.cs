using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Entities.Controller
{
    public class MenuItem
    {
        public Guid Id { get; set; }
        public Guid? ControllerId { get; set; }
        public Guid? ParentId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Route { get; set; }
        public int DisplayOrder { get; set; }
        public string? PermissionKey { get; set; }
        public bool IsActive { get; set; } = true;

        // 導覽屬性 (Navigation Property)
        public virtual MenuItem? Parent { get; set; }
        public virtual ICollection<MenuItem> Children { get; set; } = new List<MenuItem>();

    }

    public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
    {
        public void Configure(EntityTypeBuilder<MenuItem> builder)
        {
         
            builder.ToTable("MenuItems");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Route)
                .HasMaxLength(200);

            builder.Property(x => x.PermissionKey)
                .HasMaxLength(50);

            // 樹狀結構關聯：Self-referencing relationship
            builder.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict); // 防止刪除父節點時誤刪子節點

            // 索引優化：查詢選單時通常會依據 ParentId 與排序
            builder.HasIndex(x => x.ParentId);
            builder.HasIndex(x => x.DisplayOrder);
        }
    }
}
