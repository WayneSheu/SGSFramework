using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.RUL.Infrastructure.Entities;

namespace SGS.Modules.RUL.Infrastructure.Configurations;

public sealed class RuleDefinitionConfiguration : IEntityTypeConfiguration<RuleDefinition>
{
    public void Configure(EntityTypeBuilder<RuleDefinition> b)
    {
        b.ToTable("Rules", "rul");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Domain).HasConversion<byte>();
        b.Property(x => x.Kind).HasConversion<byte>();
        b.Property(x => x.TargetEntity).HasMaxLength(100).IsRequired();
        b.Property(x => x.FieldName).HasMaxLength(100);
        b.Property(x => x.RoleName).HasMaxLength(100).IsRequired();
        b.Property(x => x.ConditionExpr).HasMaxLength(1000).IsRequired();
        b.Property(x => x.ActionExpr).HasMaxLength(1000);
        b.Property(x => x.OutputPayload).HasMaxLength(2000);
        b.Property(x => x.ReadAccess).HasConversion<int>();
        b.Property(x => x.WriteAccess).HasConversion<int>();
        b.Property(x => x.Risk).HasConversion<int?>();
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.CreatedBy).HasMaxLength(256);
        b.Property(x => x.UpdatedBy).HasMaxLength(256);
        b.HasIndex(x => new { x.Status, x.Domain, x.TargetEntity });
    }
}

public sealed class RuleChangeRequestConfiguration : IEntityTypeConfiguration<RuleChangeRequest>
{
    public void Configure(EntityTypeBuilder<RuleChangeRequest> b)
    {
        b.ToTable("ChangeRequests", "rul");
        b.HasKey(x => x.Id);
        b.Property(x => x.ChangeType).HasConversion<byte>();
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.ProposedJson).HasMaxLength(4000);
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.RequestedBy).HasMaxLength(256).IsRequired();
        b.Property(x => x.ReviewedBy).HasMaxLength(256);
        b.Property(x => x.ReviewComment).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(256);
        b.Property(x => x.UpdatedBy).HasMaxLength(256);
        b.HasIndex(x => new { x.Status, x.RequestedAtUtc });
        b.HasIndex(x => x.RuleId);
    }
}

public sealed class RuleVersionConfiguration : IEntityTypeConfiguration<RuleVersion>
{
    public void Configure(EntityTypeBuilder<RuleVersion> b)
    {
        b.ToTable("Versions", "rul");
        b.HasKey(x => x.Id);
        b.Property(x => x.SnapshotJson).HasMaxLength(4000).IsRequired();
        b.Property(x => x.ApprovedBy).HasMaxLength(256).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(256);
        b.Property(x => x.UpdatedBy).HasMaxLength(256);
        b.HasIndex(x => new { x.RuleId, x.Version }).IsUnique();
    }
}

public sealed class RuleTestCaseConfiguration : IEntityTypeConfiguration<RuleTestCase>
{
    public void Configure(EntityTypeBuilder<RuleTestCase> b)
    {
        b.ToTable("TestCases", "rul");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.UserJson).HasMaxLength(2000).IsRequired();
        b.Property(x => x.TargetJson).HasMaxLength(4000).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(256);
        b.Property(x => x.UpdatedBy).HasMaxLength(256);
        b.HasIndex(x => x.RuleId);
    }
}
