#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using System;

    /// <summary>
    /// 實驗室專屬角色適用的區域實驗室（一個角色可指派多個實驗室）。
    /// ApplicationRole.TenantLabId 保留為第一個實驗室，供既有相容用途。
    /// </summary>
    public class RoleLabScope
    {
        public Guid RoleId { get; set; }

        /// <summary>區域實驗室的 TenantLabId（ORG Organization.TenantLabId）</summary>
        public Guid TenantLabId { get; set; }
    }

    public class RoleLabScopeConfiguration : IEntityTypeConfiguration<RoleLabScope>
    {
        public void Configure(EntityTypeBuilder<RoleLabScope> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("RoleLabScopes", "core");
            builder.HasKey(x => new { x.RoleId, x.TenantLabId });
            builder.HasIndex(x => x.TenantLabId);
        }
    }
}
