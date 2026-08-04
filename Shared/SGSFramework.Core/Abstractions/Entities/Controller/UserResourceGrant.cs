using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Entities.Controller
{
    /// <summary>
    /// 使用者資源授權實體，表示特定使用者對特定資源（如功能表項目）的存取權限。
    /// 「稽核人員」視為一個基於 Role 的起點，再透過 UserResourceGrants 或 Global Filter 進行例外擴充，確實是處理這類特殊權限最高效的方法。
    /// 
    /// </summary>
    public class UserResourceGrant
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid MenuItemId { get; set; }
        public Guid LabId { get; set; } // 多實驗室維度
        public string PermissionType { get; set; } = string.Empty; // e.g., "Read", "Write"
        public string EffectType { get; set; } = "Allow"; // "Allow" 或 "Deny"

        public Guid? DelegatorUserId { get; set; } // 被代理人 ID (誰委託我代理？)
        public DateTimeOffset? ValidFrom { get; set; } // 為空代表永久有效
        public DateTimeOffset? ValidTo { get; set; }   // 為空代表永久有效

        public virtual MenuItem MenuItem { get; set; } = null!;
    }

    public class UserResourceGrantConfiguration : IEntityTypeConfiguration<UserResourceGrant>
    {
        public void Configure(EntityTypeBuilder<UserResourceGrant> builder)
        {
            
            builder.ToTable("UserResourceGrants");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.PermissionType)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.EffectType)
                .IsRequired()
                .HasMaxLength(10); // "Allow" | "Deny"

            builder.Property(x => x.ValidFrom).HasColumnType("datetimeoffset");
            builder.Property(x => x.ValidTo).HasColumnType("datetimeoffset");


            // 複合索引：加速權限判斷查詢
            // 判斷順序：先找 User，再看針對哪個 Menu，最後過濾 Lab
            builder.HasIndex(x => new { x.UserId, x.MenuItemId, x.LabId })
                   .HasDatabaseName("IX_UserResource_Lookup");

            builder.HasOne(x => x.MenuItem)
                   .WithMany()
                   .HasForeignKey(x => x.MenuItemId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
