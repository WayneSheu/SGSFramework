using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Entities.Ledgers;
using SGSFramework.Core.Abstractions.Entities.Modules;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Core.Abstractions.Permissions.Identities;
using SGSFramework.Core.Identiies.Tenants;
using SGSFramework.Core.Migrations;
using SGSFramework.Infrastructure.Persistence.Configurations;
using SGSFramework.Persistent.Abstractions.Dbcontexts;

namespace PhysLIMS.API.Dbcontexts;

public class PhysLIMSDbContext : BaseIdentityDbContext<ApplicationUser, ApplicationRole, Guid, PhysLIMSDbContext>, ILogDbContext, ITokenDbContext
{
    public PhysLIMSDbContext(DbContextOptions<PhysLIMSDbContext> options, ITenantService? tenantService = null)
        : base(options, tenantService)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<SystemLog> SystemLogs { get; set; } = null!;
    public DbSet<SecurityLog> SecurityLogs { get; set; } = null!;
    public DbSet<UserRefreshToken> UserRefreshTokens { get; set; } = null!;
    public DbSet<RemediationTicket> RemediationTickets { get; set; } = null!;
    public DbSet<ModuleMetadata> ModuleMetadatas { get; set; } = null!;
    public DbSet<ControllerMetadata> ControllerMetadata { get; set; } = null!;
    public DbSet<MenuItem> MenuItems { get; set; } = null!;
    public DbSet<PermissionGrant> PermissionGrants { get; set; } = null!;
    public DbSet<UserResourceGrant> UserResourceGrants { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {

        // 替換關聯式資料庫的 Annotation Provider
        optionsBuilder.ReplaceService<IRelationalAnnotationProvider, CustomSqlServerAnnotationProvider>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        // 1. 全域指定預設 Schema 為 "core"
        modelBuilder.HasDefaultSchema("core");

        // 2. 將原先的 OutboxMessageConfiguration 改為 CoreOutboxMessageConfiguration
        modelBuilder.ApplyConfiguration(new CoreOutboxMessageConfiguration());

        // 2. 自動掃描同 Assembly 下的所有 IEntityTypeConfiguration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PhysLIMSDbContext).Assembly);

        // 3. 針對所有實作 ILedgerEntity 的實體自動附加 MSSQL Ledger 標註
        var ledgerEntityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(ILedgerEntity).IsAssignableFrom(t.ClrType) && !t.ClrType.IsInterface)
            .ToList();

        foreach (var entityType in ledgerEntityTypes)
        {
            entityType.AddAnnotation("SqlServer:IsLedgerAppendOnly", true);
        }

        // 4. Identity 相關實體 Schema 顯式對齊 "core"
        modelBuilder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable("AspNetUserTokens", "core");
            entity.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });

            entity.Property(t => t.UserId)
                  .HasColumnType("uniqueidentifier")
                  .IsRequired();

            entity.HasOne<ApplicationUser>()
                  .WithMany()
                  .HasForeignKey(ut => ut.UserId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}