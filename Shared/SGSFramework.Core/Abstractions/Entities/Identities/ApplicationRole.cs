using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.Base;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 代表應用程式中的角色實體，繼承自 IdentityRole<Guid>，使用 Guid 作為角色的唯一識別碼。
    /// </summary>
    /// <summary>
    /// 自訂應用程式角色實體
    /// </summary>
    public class ApplicationRole : IdentityRole<Guid>, IRoleEntity
    {
        public string? Code { get; set; }
        public string? Description { get; set; }

        /// <summary>
        /// 是否為系統內建角色。預設為 false（代表一般自訂角色）；系統初始化範本會明確設為 true。
        /// </summary>
        public bool IsSystemRole { get; set; } = false;
    }

    /// <summary>
    /// ApplicationRole 實體資料庫映射組態
    /// </summary>
    public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
    {
        public void Configure(EntityTypeBuilder<ApplicationRole> builder)
        {
            // 對應 ASP.NET Core Identity 預設的角色資料表名稱 (或依專案慣例調整)
            builder.ToTable("AspNetRoles");

            // 主鍵
            builder.HasKey(r => r.Id);

            // 欄位屬性限制
            builder.Property(r => r.Name)
                .HasMaxLength(256)
                .IsRequired();

            builder.Property(r => r.NormalizedName)
                .HasMaxLength(256)
                .IsRequired();

            builder.Property(r => r.Code)
                .HasMaxLength(64)
                .IsRequired(false);

            builder.Property(r => r.Description)
                .HasMaxLength(512)
                .IsRequired(false);

            builder.Property(r => r.IsSystemRole)
                .IsRequired()
                .HasDefaultValue(false);

            // 索引優化：針對 Code 建立唯一索引以確保系統角色代碼不重複
            builder.HasIndex(r => r.Code)
                .IsUnique()
                .HasFilter("[Code] IS NOT NULL");
        }
    }
}
