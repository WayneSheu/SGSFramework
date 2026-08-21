using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Infrastructure.Entities.Org
{
    /// <summary>
    /// 使用者與區域實驗室對應實體 (多對多關聯)
    /// </summary>
    public class UserLaboratory : IAuditable
    {
        public string UserId { get; private set; } = string.Empty;
        public int OrganizationId { get; private set; }

        public virtual Organization Organization { get; private set; } = null!;

        public string CreatedBy { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTimeOffset? UpdatedAtUtc { get; set; }

        protected UserLaboratory() { }

        public static UserLaboratory Create(string userId, int organizationId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
            if (organizationId <= 0) throw new ArgumentOutOfRangeException(nameof(organizationId));

            return new UserLaboratory
            {
                UserId = userId,
                OrganizationId = organizationId
            };
        }
    }

    public class UserLaboratoryConfiguration : IEntityTypeConfiguration<UserLaboratory>
    {
        public void Configure(EntityTypeBuilder<UserLaboratory> builder)
        {
            builder.ToTable("UserLaboratory", "org");

            builder.HasKey(x => new { x.UserId, x.OrganizationId });

            builder.Property(x => x.UserId).HasMaxLength(450);

            builder.HasOne(x => x.Organization)
                   .WithMany()
                   .HasForeignKey(x => x.OrganizationId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
