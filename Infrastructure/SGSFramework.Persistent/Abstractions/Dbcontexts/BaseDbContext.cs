using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SGSFramework.Core.Abstractions.Entities.SoftDelet;
using SGSFramework.Core.Abstractions.Entities.Tenants;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Core.Identiies.Tenants;
using SGSFramework.Persistent.Configurations;
using SGSFramework.Persistent.Extensions;
using SGSFramework.Persistent.Helpers;
using System.Linq.Expressions;

namespace SGSFramework.Persistent.Abstractions.Dbcontexts
{
    /// <summary>
    /// 企業級泛型 DbContext 基底類別
    /// 整合了多租戶, 自動審計, 軟刪除, HierarchyId 與 OutboxRepository
    /// </summary>
    public abstract class BaseDbContext<TContext> : DbContext, IOutboxRepository
    where TContext : DbContext
    {

        private readonly string? _tenantId;
        //private readonly string? _schema;
        protected string _schema { get; }

        protected BaseDbContext(DbContextOptions<TContext> options, ITenantService? tenantService = null)
        : base(options)
        {
            // 核心整合：從 Options 中尋找透過 UseCustomSchema 傳入的擴充配置
            var extension = options.FindExtension<ToolkitOptionsExtension>();

            // 取得 Schema 設定，若無則預設為 "dbo"
            _schema = extension?.Schema ?? "core";

            // 取得目前租戶 ID (執行時由 DI 注入)
            _tenantId = tenantService?.GetTenantId();
        }

        // 這裡需要透過注入或是 ServiceLocator 方式取得
        // 簡單做法是直接在 ConfigureConventions 中 new 一個，
        // 或是在 AddDbContext 時透過 sp 傳遞。
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            //系統全域慣例與轉換政策
            configurationBuilder.ApplySESDefaultConventions();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // 執行時期：根據租戶動態切換資料庫
                //var connectionString = ITenantService.GetConnectionString();
                //optionsBuilder.UseSqlServer(connectionString, x => x.UseHierarchyId());
            }

            //只有本地開發環境才啟用
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.EnableDetailedErrors(); // 選擇性：開發時顯示更詳細的錯誤
            }

            // 注入自訂的 SQL 產生器（確保 Ledger 功能生效）
            //optionsBuilder.ReplaceService<IMigrationsSqlGenerator, LedgerMigrationsSqlGenerator>();
        }


        #region IOutboxRepository 實作

        /// <summary>
        /// 使用 SQL Server 特有的 UPDATE...OUTPUT 語法實現原子化抓取。
        /// 配合 ROWLOCK 與 READPAST 提示，確保多個 Background Worker 之間不會互相競爭同一筆資料。
        /// </summary>
        public async Task<List<OutboxMessage>> FetchOutboxMessagesAsync(int batchSize)
        {
            // 標記抓取時間，1900-01-01 作為「處理中」的暫時狀態
            var processingFetchTime = new DateTime(1900, 1, 1);

            // 動態獲取 OutboxMessage 在資料庫中的表名與 Schema
            var entityType = Model.FindEntityType(typeof(OutboxMessage));
            var tableName = entityType?.GetTableName() ?? "OutboxMessages";
            var schema = entityType?.GetSchema() ?? _schema;

            // 核心 SQL：更新的同時回傳受影響的行 (INSERTED.*)
            var sql = $@"
                UPDATE TOP (@batchSize) [{schema}].[{tableName}] WITH (ROWLOCK, READPAST)
                SET [{nameof(OutboxMessage.ProcessedOnUtc)}] = @fetchTime
                OUTPUT INSERTED.*
                WHERE [{nameof(OutboxMessage.ProcessedOnUtc)}] IS NULL
                AND ([{nameof(OutboxMessage.ScheduledAtUtc)}] IS NULL OR [{nameof(OutboxMessage.ScheduledAtUtc)}] <= GETUTCDATE())
                AND [{nameof(OutboxMessage.IsDead)}] = 0";

            var batchSizeParam = new Microsoft.Data.SqlClient.SqlParameter("@batchSize", batchSize);
            var fetchTimeParam = new Microsoft.Data.SqlClient.SqlParameter("@fetchTime", processingFetchTime);

            // 執行原始 SQL 並將結果對應回 OutboxMessage 實體
            return await Set<OutboxMessage>()
                .FromSqlRaw(sql, batchSizeParam, fetchTimeParam)
                .IgnoreQueryFilters()
                .ToListAsync();
        }

        #endregion

        /// <summary>
        /// 配置模型建立邏輯
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 自動將該 DbContext 內的所有資料表套用專屬的 Schema
            if (!string.IsNullOrWhiteSpace(_schema))
            {
                modelBuilder.HasDefaultSchema(_schema);
            }

            // 套用實體配置
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TContext).Assembly);

            // 透過反射自動套用 ISoftDeletable 實體的全域查詢過濾器
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entityType.GetViewName() != null) continue;

                // 檢查是否已被 SystemLogConfiguration 明確標註為 dbo
                var currentSchema = entityType.GetSchema();
                //if (currentSchema == "dbo") continue;

                var tableName = entityType.GetTableName();
                if (!string.IsNullOrEmpty(tableName))
                {
                    entityType.SetSchema(_schema);
                }
            }

            // 透過 Helper 統一套用 Outbox, Log 與多租戶配置
            DbConfigurationHelper.ApplyCommonConfigs(modelBuilder, _schema, typeof(TContext), _tenantId);
            // 使用擴充方法抽離並套用軟刪除過濾器
            modelBuilder.ApplySoftDeleteFilters();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // 自動為 IMultiTenant 實體注入 TenantID 陰影屬性值
            foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
            {
                // 檢查實體是否實作了 IMultiTenant
                if (entry.Entity is ITenantEntity)
                {
                    // 設定陰影屬性的值
                    entry.Property("TenantID").CurrentValue = _tenantId;
                }
            }
            return base.SaveChangesAsync(cancellationToken);
        }

        private static LambdaExpression GenerateSoftDeleteExpression(Type type)
        {
            var parameter = Expression.Parameter(type, "e");
            var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var body = Expression.Equal(property, Expression.Constant(false));
            return Expression.Lambda(body, parameter);
        }

    }

    internal interface ITenant
    {
    }
}
