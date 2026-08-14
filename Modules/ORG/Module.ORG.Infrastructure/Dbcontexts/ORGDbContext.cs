using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
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

            // 載入安全存取 Scope 的 AuditInterceptor
            optionsBuilder.AddInterceptors(_auditInterceptor);

            // 如果尚未配置，則使用 SQL Server 並指定 MigrationsHistoryTable 的 schema 為 "org"
            if (!optionsBuilder.IsConfigured)
            {
                // 忽略 EF Core 的 PendingModelChangesWarning 警告，避免在應用程式啟動時出現不必要的警告訊息
                optionsBuilder.ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

                // 設定 MigrationsHistoryTable 的 schema 為 "org"
                optionsBuilder.UseSqlServer( 
                    sqlOptions =>
                    {
                        // 確保這裡的字串與組件名稱完全對應
                        // 這裡的字串必須與組件名稱完全對應，否則會導致 MigrationsHistoryTable 無法正確建立
                        // 明確指向 Assembly 對象，而非僅字串名稱
                        sqlOptions.MigrationsAssembly(typeof(ORGDbContext).Assembly.FullName);
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "org");
                    }
                );
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
            modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

            // 3. 在 Configuration 載入後，重新強制確保共用 Entity 與專用 Entity 的映射 Schema 為 "org"
            modelBuilder.Entity<AuditLogEntity>()
                .ToTable("AuditLogs", "org");

            modelBuilder.Entity<Organization>()
                .ToTable("Organization", "org");



        }
    }
}
