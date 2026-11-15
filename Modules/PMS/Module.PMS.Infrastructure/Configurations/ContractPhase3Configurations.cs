using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Infrastructure.Configurations;

public sealed class ContractSupportRequestConfiguration : IEntityTypeConfiguration<ContractSupportRequest>
{
    public void Configure(EntityTypeBuilder<ContractSupportRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractSupportRequests", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.EstimatedHours).HasPrecision(8, 2);
        builder.Property(x => x.EstimatedOvertimeHours).HasPrecision(8, 2);
        builder.Property(x => x.EstimatedWeightedHours).HasPrecision(8, 2);
        builder.Property(x => x.ActualOvertimeHours).HasPrecision(8, 2);
        builder.Property(x => x.ActualWeightedHours).HasPrecision(8, 2);
        builder.Property(x => x.RequestedByName).HasMaxLength(256);
        builder.Property(x => x.DecidedByName).HasMaxLength(256);
        builder.Property(x => x.ClientRequestChannel).HasMaxLength(50);
        builder.Property(x => x.ClientRequestRef).HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.ContractId, x.Number }).IsUnique();
        builder.HasIndex(x => new { x.ContractId, x.Status });

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContractComplianceItemConfiguration : IEntityTypeConfiguration<ContractComplianceItem>
{
    public void Configure(EntityTypeBuilder<ContractComplianceItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractComplianceItems", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.ContractId, x.Code }).IsUnique();

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
    }
}
