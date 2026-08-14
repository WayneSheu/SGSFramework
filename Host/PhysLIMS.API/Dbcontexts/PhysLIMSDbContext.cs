using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Entities.Modules;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Identiies.Tenants;
using SGSFramework.Persistent.Abstractions.Dbcontexts;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Identities;

namespace PhysLIMS.API.Dbcontexts
{
    public class PhysLIMSDbContext : BaseIdentityDbContext<ApplicationUser, ApplicationRole, Guid, PhysLIMSDbContext>, ILogDbContext, ITokenDbContext
    {
        public PhysLIMSDbContext(DbContextOptions<PhysLIMSDbContext> options, ITenantService? tenantService = null)
            : base(options, tenantService)
        {

        }


        public DbSet<SGSFramework.Core.Abstractions.Logings.SystemLog> SystemLogs { get; set; }

        public DbSet<SGSFramework.Core.Abstractions.Logings.SecurityLog> SecurityLogs { get; set; }

        public DbSet<UserRefreshToken> UserRefreshTokens { get; set; }

        public DbSet<RemediationTicket> RemediationTickets { get; set; } = null!;

        public DbSet<ModuleMetadata> ModuleMetadatas { get; set; } = null!;

        public DbSet<ControllerMetadata> ControllerMetadata { get; set; }

        public DbSet<MenuItem> MenuItems { get; set; }=null!;

        public DbSet<PermissionGrant> PermissionGrants { get; set; } = null!;

        public DbSet<UserResourceGrant> UserResourceGrants { get; set; } = null!;

        public DbSet<Permission> Permissions     { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Identity 必須先呼叫 base

            // 1. 全域將預設 Schema 由 "dbo" 覆蓋為 "core"
            modelBuilder.HasDefaultSchema("core");

            // 顯式宣告 IdentityUserToken<Guid> 的複合主鍵與欄位轉型
            modelBuilder.Entity<IdentityUserToken<Guid>>(entity =>
            {
                entity.ToTable("AspNetUserTokens");

                // 設定複合主鍵 (UserId, LoginProvider, Name)
                entity.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });

                // 指定 UserId 屬性型態為 Guid/uniqueidentifier
                entity.Property(t => t.UserId)
                      .HasColumnType("uniqueidentifier")
                      .IsRequired();

                // 建立與 AspNetUsers 的外鍵關聯
                entity.HasOne<ApplicationUser>()
                      .WithMany()
                      .HasForeignKey(ut => ut.UserId)
                      .IsRequired()
                      .OnDelete(DeleteBehavior.Cascade);
            });


            //自動掃描同一個 Assembly 中所有實作 IEntityTypeConfiguration 的類別
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(PhysLIMSDbContext).Assembly);

            // 或者手動指定（如果你把配置放在 SGSFramework.Core 或 Persistent）
            modelBuilder.ApplyConfiguration(new SystemLogConfiguration());
        }
    }
}
