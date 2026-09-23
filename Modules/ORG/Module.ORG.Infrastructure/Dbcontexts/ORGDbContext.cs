// ==========================================
// 檔案路徑: Infrastructure/SGS.Modules.ORG.Infrastructure/Dbcontexts/ORGDbContext.cs
// 架構層級: Infrastructure Layer (EF Core DbContext)
// 說明: ORG 模組獨立資料庫上下文，符合 Clean Architecture 與 .NET 10 企業級規範
// ==========================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGS.Modules.ORG.Infrastructure.Migrations;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Persistent.Abstractions.Dbcontexts;

namespace SGS.Modules.ORG.Infrastructure.Dbcontexts
{
    /// <summary>
    /// ORG 模組專屬 DbContext，負責處理組織架構、稽核紀錄與 Outbox 訊息持久化。
    /// </summary>
    public class ORGDbContext : BaseDbContext<ORGDbContext>, IAuditDbContext
    {
        /// <summary>
        /// 初始化 <see cref="ORGDbContext"/> 類別的新實體。
        /// 符合 DI 最佳實踐，僅接收標準 DbContextOptions，避免建構子強綁定及 Captive Dependency 隱患。
        /// </summary>
        /// <param name="options">DbContext 設定選項。</param>
        public ORGDbContext(DbContextOptions<ORGDbContext> options)
            : base(options)
        {
        }

        #region DbSets

        /// <summary>
        /// 稽核紀錄資料集 (實作 <see cref="IAuditDbContext"/>)
        /// </summary>
        public DbSet<AuditLogEntity> AuditLogs { get; set; } = null!;

        /// <summary>
        /// Outbox 發送訊息佇列資料集
        /// </summary>
        public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

        /// <summary>
        /// 組織主檔資料集
        /// </summary>
        public DbSet<Organization> Organizations { get; set; } = null!;

        /// <summary>
        /// 使用者可存取實驗室檢視表 Read Model
        /// </summary>
        public DbSet<UserAccessibleLabReadModel> UserAccessibleLabReadModels { get; set; } = null!;

        #endregion

        /// <summary>
        /// 配置 DbContext 基礎設施與元件替換。
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);

            base.OnConfiguring(optionsBuilder);

            // 替換 EF Core 內部的 IMigrationsAssembly 為外掛動態載入強化版
            optionsBuilder.ReplaceService<IMigrationsAssembly, PluginMigrationsAssembly>();

            // 忽略 Model 變更警告，避免 CI/CD 自動 Migration 時發出不必要的事件阻斷
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        /// <summary>
        /// 設定 Entity 映射模型、Schema 與實體組態。
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            base.OnModelCreating(modelBuilder);

            // 1. 設定 ORG 模組全域預設 Schema
            modelBuilder.HasDefaultSchema("org");

            // 2. 自動載入與套用同一 Assembly 中的所有 IEntityTypeConfiguration<T>
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ORGDbContext).Assembly);

            // 3. 強制確保核心與共用 Entity 映射至 org Schema
            modelBuilder.Entity<AuditLogEntity>(b =>
            {
                b.ToTable("AuditLogs", "org");
            });

            modelBuilder.Entity<OutboxMessage>(b =>
            {
                b.ToTable("OutboxMessages", "org");
            });

            modelBuilder.Entity<Organization>(b =>
            {
                b.ToTable("Organization", "org");
            });

            // 4. 設定 Keyless Read Model 檢視表映射
            modelBuilder.Entity<UserAccessibleLabReadModel>(eb =>
            {
                eb.HasNoKey();
                eb.ToView("vw_UserAccessibleLabs", "org");
            });
        }
    }
}