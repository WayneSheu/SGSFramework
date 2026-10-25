using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Identities;

namespace PhysLIMS.API.Dbcontexts;

/// <summary>
/// ASP.NET Core Identity 內建資料表與繼承欄位的說明 (MS_Description)。
/// Identity 實體類別 (IdentityUserClaim&lt;TKey&gt; 等) 與 IdentityUser / IdentityRole 基底屬性
/// 位於框架組件中無法加 [Comment]，故以 Fluent API HasComment 集中設定。
/// </summary>
internal static class IdentityCommentConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.Id).HasComment("使用者識別主鍵");
            e.Property(x => x.UserName).HasComment("登入帳號");
            e.Property(x => x.NormalizedUserName).HasComment("正規化帳號 (大寫，供唯一性檢查與查詢比對)");
            e.Property(x => x.Email).HasComment("電子郵件");
            e.Property(x => x.NormalizedEmail).HasComment("正規化電子郵件 (大寫，供查詢比對)");
            e.Property(x => x.EmailConfirmed).HasComment("電子郵件是否已驗證");
            e.Property(x => x.PasswordHash).HasComment("密碼雜湊值 (不儲存明碼)");
            e.Property(x => x.SecurityStamp).HasComment("安全戳記；密碼或安全性設定變更時更新，使既有登入憑證失效");
            e.Property(x => x.ConcurrencyStamp).HasComment("並行控制戳記 (樂觀鎖定)");
            e.Property(x => x.PhoneNumber).HasComment("電話號碼");
            e.Property(x => x.PhoneNumberConfirmed).HasComment("電話號碼是否已驗證");
            e.Property(x => x.TwoFactorEnabled).HasComment("是否啟用雙因素驗證");
            e.Property(x => x.LockoutEnd).HasComment("帳號鎖定截止時間 (UTC)；null 表示未鎖定");
            e.Property(x => x.LockoutEnabled).HasComment("是否允許因登入失敗而鎖定帳號");
            e.Property(x => x.AccessFailedCount).HasComment("連續登入失敗次數 (登入成功後歸零)");
        });

        modelBuilder.Entity<ApplicationRole>(e =>
        {
            e.Property(x => x.Id).HasComment("角色識別主鍵");
            e.Property(x => x.Name).HasComment("角色名稱");
            e.Property(x => x.NormalizedName).HasComment("正規化角色名稱 (大寫，供唯一性檢查與查詢比對)");
            e.Property(x => x.ConcurrencyStamp).HasComment("並行控制戳記 (樂觀鎖定)");
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>(e =>
        {
            e.ToTable(t => t.HasComment("使用者宣告 (Claims)：附加於使用者的身分屬性，登入時寫入權杖"));
            e.Property(x => x.Id).HasComment("識別主鍵");
            e.Property(x => x.UserId).HasComment("使用者識別 (AspNetUsers.Id)");
            e.Property(x => x.ClaimType).HasComment("宣告類型");
            e.Property(x => x.ClaimValue).HasComment("宣告值");
        });

        modelBuilder.Entity<IdentityRoleClaim<Guid>>(e =>
        {
            e.ToTable(t => t.HasComment("角色宣告 (Claims)：附加於角色的身分屬性，該角色成員登入時一併取得"));
            e.Property(x => x.Id).HasComment("識別主鍵");
            e.Property(x => x.RoleId).HasComment("角色識別 (AspNetRoles.Id)");
            e.Property(x => x.ClaimType).HasComment("宣告類型");
            e.Property(x => x.ClaimValue).HasComment("宣告值");
        });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(e =>
        {
            e.ToTable(t => t.HasComment("使用者外部登入：使用者與外部驗證提供者 (如 Windows / SSO) 帳號的對應"));
            e.Property(x => x.LoginProvider).HasComment("外部驗證提供者名稱");
            e.Property(x => x.ProviderKey).HasComment("外部提供者中的使用者唯一識別");
            e.Property(x => x.ProviderDisplayName).HasComment("外部提供者顯示名稱");
            e.Property(x => x.UserId).HasComment("使用者識別 (AspNetUsers.Id)");
        });

        modelBuilder.Entity<IdentityUserRole<Guid>>(e =>
        {
            e.ToTable(t => t.HasComment("使用者角色指派：使用者與角色的多對多關聯"));
            e.Property(x => x.UserId).HasComment("使用者識別 (AspNetUsers.Id)");
            e.Property(x => x.RoleId).HasComment("角色識別 (AspNetRoles.Id)");
        });

        modelBuilder.Entity<IdentityUserToken<Guid>>(e =>
        {
            e.ToTable(t => t.HasComment("使用者權杖：外部驗證提供者發給使用者的權杖 (如驗證器金鑰、復原碼)"));
            e.Property(x => x.UserId).HasComment("使用者識別 (AspNetUsers.Id)");
            e.Property(x => x.LoginProvider).HasComment("權杖所屬驗證提供者");
            e.Property(x => x.Name).HasComment("權杖名稱");
            e.Property(x => x.Value).HasComment("權杖值");
        });
    }
}
