using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Entities.Controller
{
    /// <summary>
    /// 角色權限授權實體，表示特定角色對特定功能或資源的存取權限。
    /// </summary>
    public class PermissionGrant
    {
        public Guid Id { get; set; }
        public Guid RoleId { get; set; }
        public Guid MenuItemId { get; set; }
        public Guid LabId { get; set; } // 多實驗室隔離維度
        public string PermissionType { get; set; } = string.Empty; // e.g., "Read", "Write"

        // 導覽屬性
        public virtual MenuItem MenuItem { get; set; } = null!;
    }

    public class PermissionGrantConfiguration : IEntityTypeConfiguration<PermissionGrant>
    {
        public void Configure(EntityTypeBuilder<PermissionGrant> builder)
        {
            //
            builder.ToTable("PermissionGrants");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.PermissionType)
                .IsRequired()
                .HasMaxLength(20);

            // 複合唯一索引：確保同一角色在同一實驗室針對同一資源只有一筆權限設定
            builder.HasIndex(x => new { x.RoleId, x.MenuItemId, x.LabId })
                   .IsUnique()
                   .HasDatabaseName("IX_Permission_Role_Menu_Lab");

            // 外部鍵關聯
            builder.HasOne(x => x.MenuItem)
                   .WithMany()
                   .HasForeignKey(x => x.MenuItemId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
