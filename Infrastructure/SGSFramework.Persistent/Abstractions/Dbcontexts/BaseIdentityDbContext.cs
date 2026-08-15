using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Entities.Tenants;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Core.Identiies.Tenants;
using SGSFramework.Persistent.Configurations;
using SGSFramework.Persistent.Extensions;
using SGSFramework.Persistent.Helpers;

namespace SGSFramework.Persistent.Abstractions.Dbcontexts
{
    /// <summary>
    /// 企業級泛型 DbContext 基底類別
    /// 整合了 Identity, 多租戶, 自動審計, 軟刪除, OutboxRepository 與 AuthTokenBucket 聯防核心
    /// </summary>
    public abstract class BaseIdentityDbContext<TUser, TRole, TKey, TContext>
       : IdentityDbContext<TUser, TRole, TKey>, IOutboxRepository, ITokenDbContext // 🚀 完美合流：實作套件介面
       where TUser : IdentityUser<TKey>
       where TRole : IdentityRole<TKey>
       where TKey : IEquatable<TKey>
       where TContext : DbContext
    {
        private readonly string? _schema;
        private readonly string? _tenantId;

        protected BaseIdentityDbContext(
           DbContextOptions<TContext> options,
           ITenantService? tenantService = null) : base(options)
        {
            var extension = options.FindExtension<ToolkitOptionsExtension>();
            _schema = extension?.Schema ?? "dbo";
            _tenantId = tenantService?.GetTenantId();
        }

        #region 🚀 ITokenDbContext 實作 (AuthTokenBucket 核心快取載體)

        /// <summary>
        /// 泛型套件專用之多裝置安全換票與熔斷機制快取表
        /// </summary>
        public DbSet<UserRefreshToken> UserRefreshTokens { get; set; } = null!;
        public DbSet<RemediationTicket> RemediationTickets { get; set; } = null!;
        #endregion

        #region IOutboxRepository 實作

        public async Task<List<OutboxMessage>> FetchOutboxMessagesAsync(int batchSize)
        {
            var processingFetchTime = new DateTime(1900, 1, 1);
            var entityType = Model.FindEntityType(typeof(OutboxMessage));
            var tableName = entityType?.GetTableName() ?? "OutboxMessages";
            var schema = entityType?.GetSchema() ?? _schema;

            var sql = $@"
                UPDATE TOP (@batchSize) [{schema}].[{tableName}] WITH (ROWLOCK, READPAST)
                SET [{nameof(OutboxMessage.ProcessedOnUtc)}] = @fetchTime
                OUTPUT INSERTED.*
                WHERE [{nameof(OutboxMessage.ProcessedOnUtc)}] IS NULL
                AND ([{nameof(OutboxMessage.ScheduledAtUtc)}] IS NULL OR [{nameof(OutboxMessage.ScheduledAtUtc)}] <= GETUTCDATE())
                AND [{nameof(OutboxMessage.IsDead)}] = 0";

            var batchSizeParam = new Microsoft.Data.SqlClient.SqlParameter("@batchSize", batchSize);
            var fetchTimeParam = new Microsoft.Data.SqlClient.SqlParameter("@fetchTime", processingFetchTime);

            return await Set<OutboxMessage>()
                .FromSqlRaw(sql, batchSizeParam, fetchTimeParam)
                .IgnoreQueryFilters()
                .ToListAsync();
        }

        #endregion

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        
            // 🛡️ 只有本地開發環境才啟用
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.EnableDetailedErrors(); // 選擇性：開發時顯示更詳細的錯誤
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TContext).Assembly);
            modelBuilder.HasDefaultSchema(_schema);

            // 🚀 為 UserRefreshToken 建立高效複合索引，優化 MSSQL 2025 的 UPDLOCK 與悲觀鎖併發查詢
            modelBuilder.Entity<UserRefreshToken>(entity =>
            {
                entity.ToTable("UserRefreshTokens", "core");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.DeviceId }).IsUnique();
            });

            DbConfigurationHelper.ApplyCommonConfigs(modelBuilder, _schema, typeof(TContext), _tenantId);


        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
            {
                if (entry.Entity is ITenantEntity)
                {
                    entry.Property("TenantID").CurrentValue = _tenantId;
                }
            }
            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ApplySESDefaultConventions();
        }
    }
}