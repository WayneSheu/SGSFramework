using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Infrastructure.Configurations;

public sealed class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProjectTasks", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Phase).HasConversion<byte?>();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.ProjectId, x.IsDeleted, x.ParentId, x.SortOrder });
        builder.HasIndex(x => x.AssigneeUserId);

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectTaskDependencyConfiguration : IEntityTypeConfiguration<ProjectTaskDependency>
{
    public void Configure(EntityTypeBuilder<ProjectTaskDependency> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProjectTaskDependencies", "pms");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PredecessorId, x.SuccessorId }).IsUnique();
        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.SuccessorId);

        // 前後任務皆為軟刪除，不對任務做串聯刪除（避免多重串聯路徑）；相依於任務刪除時由服務層移除
        builder.HasOne(x => x.Predecessor).WithMany().HasForeignKey(x => x.PredecessorId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Successor).WithMany().HasForeignKey(x => x.SuccessorId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ProjectBaselineConfiguration : IEntityTypeConfiguration<ProjectBaseline>
{
    public void Configure(EntityTypeBuilder<ProjectBaseline> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProjectBaselines", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.ProjectId, x.Number }).IsUnique();

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Tasks).WithOne(x => x.Baseline).HasForeignKey(x => x.BaselineId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectBaselineTaskConfiguration : IEntityTypeConfiguration<ProjectBaselineTask>
{
    public void Configure(EntityTypeBuilder<ProjectBaselineTask> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProjectBaselineTasks", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.BaselineId, x.TaskId }).IsUnique();
    }
}
