using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using SGS.Modules.FMS.Infrastructure.Entities;
using SGS.Modules.FMS.Infrastructure.Migrations;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Persistent.Abstractions.Dbcontexts;

namespace SGS.Modules.FMS.Infrastructure.Dbcontexts
{
    /// <summary>
    /// FMS 模組專屬 DbContext（schema: fms）：檔案中繼資料與其資料異動稽核。
    /// </summary>
    public class FMSDbContext : BaseDbContext<FMSDbContext>, IAuditDbContext
    {
        public FMSDbContext(DbContextOptions<FMSDbContext> options)
            : base(options)
        {
        }

        #region DbSets

        public DbSet<AuditLogEntity> AuditLogs { get; set; } = null!;

        public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

        public DbSet<FileMetadata> FileMetadatas { get; set; } = null!;

        #endregion

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);

            base.OnConfiguring(optionsBuilder);

            // 替換 EF Core 內部的 IMigrationsAssembly 為外掛動態載入強化版
            optionsBuilder.ReplaceService<IMigrationsAssembly, PluginMigrationsAssembly>();

            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("fms");

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(FMSDbContext).Assembly);

            modelBuilder.Entity<AuditLogEntity>(b => b.ToTable("AuditLogs", "fms"));
            modelBuilder.Entity<OutboxMessage>(b => b.ToTable("OutboxMessages", "fms"));
        }
    }
}
