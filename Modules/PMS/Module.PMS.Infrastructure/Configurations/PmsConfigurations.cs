using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Infrastructure.Persistence.Extensions;

namespace SGS.Modules.PMS.Infrastructure.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Projects", "pms", t =>
            // 專案代號格式資料層防線（服務層另有 ProjectCodePolicy）
            t.HasCheckConstraint("CK_Projects_Code_Format", "LEN([Code]) BETWEEN 2 AND 10 AND [Code] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z0-9]%' AND [Code] COLLATE Latin1_General_100_BIN2 LIKE '[A-Z]%'"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(10).IsUnicode(false).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ContractSlaNote).HasMaxLength(500);
        builder.Property(x => x.Phase).HasConversion<byte>();
        builder.Property(x => x.Stage).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.Status, x.Stage });

        builder.HasMany(x => x.Members).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Modules).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Gates).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Deliverables).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProjectMembers", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RaciRole).HasConversion<byte>();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.ProjectId, x.UserId, x.RaciRole }).IsUnique();
        // 資料列授權：我的專案
        builder.HasIndex(x => x.UserId);
    }
}

public sealed class PmsModuleConfiguration : IEntityTypeConfiguration<PmsModule>
{
    public void Configure(EntityTypeBuilder<PmsModule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PmsModules", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(10).IsUnicode(false).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.ProjectId, x.Code }).IsUnique();
    }
}

public sealed class ProjectGateConfiguration : IEntityTypeConfiguration<ProjectGate>
{
    public void Configure(EntityTypeBuilder<ProjectGate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProjectGates", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Gate).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.EvidenceNote).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.ProjectId, x.Gate }).IsUnique();
    }
}

public sealed class DeliverableConfiguration : IEntityTypeConfiguration<Deliverable>
{
    public void Configure(EntityTypeBuilder<Deliverable> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Deliverables", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Stage).HasConversion<byte>();
        builder.Property(x => x.DocType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.DocName).HasMaxLength(100);
        builder.Property(x => x.OriginalFileName).HasMaxLength(260);
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);
        builder.Ignore(x => x.HasFile);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.ProjectId, x.Stage, x.DocType });
        // 同一文件（專案＋模組＋類型）同版本未刪除者唯一（SQL Server 唯一索引視 NULL 為相等，ModuleId 為空亦受限）
        builder.HasIndex(x => new { x.ProjectId, x.ModuleId, x.DocType, x.Version })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.FileId);
        builder.HasOne(x => x.Module).WithMany().HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ProjectCodeReservationConfiguration : IEntityTypeConfiguration<ProjectCodeReservation>
{
    public void Configure(EntityTypeBuilder<ProjectCodeReservation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProjectCodeReservations", "pms");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }
}

public sealed class PmsOutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ConfigureOutboxTable("pms");
    }
}
