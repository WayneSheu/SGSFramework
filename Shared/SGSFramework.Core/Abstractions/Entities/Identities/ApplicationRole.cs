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
        /// <summary>
        /// 角色描述（選填，允許為 Null）
        /// </summary>
        public string? Description { get; set; }
    }

    /// <summary>
    /// ApplicationRole 實體資料庫映射組態
    /// </summary>
    public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
    {
        public void Configure(EntityTypeBuilder<ApplicationRole> builder)
        {
            // 於資料庫層級強制設定 Name 為 NOT NULL
            builder.Property(r => r.Name)
                   .IsRequired()
                   .HasMaxLength(256);

            // 設定 Description 為 NULL
            builder.Property(r => r.Description)
                   .IsRequired(false)
                   .HasMaxLength(500);
        }
    }
}
