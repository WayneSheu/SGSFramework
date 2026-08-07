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

namespace PhysLIMS.API.Dbcontexts
{
    public class PhysLIMSDbContext : BaseIdentityDbContext<IdentityUser,IdentityRole,string, PhysLIMSDbContext>, ILogDbContext, ITokenDbContext
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

            // 關鍵：這行會自動掃描同一個 Assembly 中所有實作 IEntityTypeConfiguration 的類別
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(PhysLIMSDbContext).Assembly);

            // 或者手動指定（如果你把配置放在 SGSFramework.Core 或 Persistent）
            modelBuilder.ApplyConfiguration(new SystemLogConfiguration());
        }
    }
}
