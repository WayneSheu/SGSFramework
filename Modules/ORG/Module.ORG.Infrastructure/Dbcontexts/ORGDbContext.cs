using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGS.Modules.ORG.Infrastructure.Migrations;
using SGSFramework.AuditLog.Interceptors;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Core.Identiies.Tenants;
using SGSFramework.Persistent.Abstractions.Dbcontexts;

namespace SGS.Modules.ORG.Infrastructure.Dbcontexts
{

    public class ORGDbContext : BaseDbContext<ORGDbContext>, IAuditDbContext
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AuditInterceptor _auditInterceptor;

        public ORGDbContext(DbContextOptions<ORGDbContext> options,
        IServiceProvider serviceProvider, AuditInterceptor auditInterceptor) : base(options)
        {
            _auditInterceptor = auditInterceptor ?? throw new ArgumentNullException(nameof(auditInterceptor));

            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// 當前租戶識別碼 (動態從 ServiceProvider 延遲解析，避免 DbContext 建構子強綁定引發 Scope Validation 異常)
        /// </summary>
        public string? CurrentTenantId => ResolveTenantId(_serviceProvider);

        private static string? ResolveTenantId(IServiceProvider serviceProvider)
        {
            try
            {
                // 優先嘗試直接解析
                var tenantService = serviceProvider.GetService<ITenantService>();
                if (tenantService != null)
                {
                    return tenantService.GetTenantId();
                }

                // 若目前 IServiceProvider 為 Root Provider，手動建立 Scope 安全解析
                using var scope = serviceProvider.CreateScope();
                return scope.ServiceProvider.GetService<ITenantService>()?.GetTenantId();
            }
            catch
            {
                return null;
            }
        }


        public DbSet<AuditLogEntity> AuditLogs { get; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<Organization> Organizations { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            optionsBuilder.AddInterceptors(_auditInterceptor);

            // 核心關鍵：替換 EF Core 內部的 IMigrationsAssembly 為外掛強化版
            optionsBuilder.ReplaceService<IMigrationsAssembly, PluginMigrationsAssembly>();

            if (!optionsBuilder.IsConfigured)
        {
                optionsBuilder.ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning)); 

            optionsBuilder.UseSqlServer(sqlOptions =>
            {
            sqlOptions.MigrationsAssembly(typeof(ORGDbContext).Assembly.FullName); 
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "org"); 
            });
        }
    }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            // base
            base.OnModelCreating(modelBuilder);

            // 1. 設定模組全域預設 Schema
            modelBuilder.HasDefaultSchema("org");

            // 2. 載入同 Assembly 中所有 IEntityTypeConfiguration
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ORGDbContext).Assembly);


            // 3. 在 Configuration 載入後，重新強制確保共用 Entity 與專用 Entity 的映射 Schema 為 "org"
            modelBuilder.Entity<AuditLogEntity>()
                .ToTable("AuditLogs", "org");

            modelBuilder.Entity<Organization>()
                .ToTable("Organization", "org");



        }
    }
}
