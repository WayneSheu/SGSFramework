using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using SGS.Modules.RPT.Infrastructure.Entities;
using SGS.Modules.RPT.Infrastructure.Migrations;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Persistent.Abstractions.Dbcontexts;

namespace SGS.Modules.RPT.Infrastructure.Dbcontexts
{
    /// <summary>
    /// RPT 模組專屬 DbContext（schema: rpt）：檢驗報告、結果數據、指派、狀態軌跡、作廢申請與存取紀錄。
    /// </summary>
    public class RPTDbContext : BaseDbContext<RPTDbContext>, IAuditDbContext
    {
        public RPTDbContext(DbContextOptions<RPTDbContext> options)
            : base(options)
        {
        }

        #region DbSets

        public DbSet<AuditLogEntity> AuditLogs { get; set; } = null!;

        public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

        public DbSet<Report> Reports { get; set; } = null!;

        public DbSet<ReportResult> ReportResults { get; set; } = null!;

        public DbSet<ReportAssignment> ReportAssignments { get; set; } = null!;

        public DbSet<ReportStatusHistory> ReportStatusHistories { get; set; } = null!;

        public DbSet<ReportVoidRequest> ReportVoidRequests { get; set; } = null!;

        public DbSet<ReportAccessLog> ReportAccessLogs { get; set; } = null!;

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

            modelBuilder.HasDefaultSchema("rpt");

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(RPTDbContext).Assembly);

            modelBuilder.Entity<AuditLogEntity>(b => b.ToTable("AuditLogs", "rpt"));
            modelBuilder.Entity<OutboxMessage>(b => b.ToTable("OutboxMessages", "rpt"));
        }
    }
}
