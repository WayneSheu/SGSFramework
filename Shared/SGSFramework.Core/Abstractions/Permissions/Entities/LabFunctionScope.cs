using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace SGSFramework.Core.Abstractions.Permissions.Entities
{
    /// <summary>
    /// 區域實驗室的功能開放政策。IsRestricted = false（或無資料）表示所有功能開放；
    /// IsRestricted = true 時，僅 <see cref="LabFunctionScope"/> 列出的權限於該實驗室生效（對所有使用者與角色的上限，系統管理員除外）。
    /// </summary>
    public class LabFunctionPolicy
    {
        /// <summary>區域實驗室識別碼（ORG Organization.TenantLabId）</summary>
        public Guid TenantLabId { get; set; }

        public bool IsRestricted { get; set; }

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public Guid? UpdatedBy { get; set; }

        public string? UpdatedByName { get; set; }
    }

    /// <summary>區域實驗室開放的權限（完整權限鍵，例：ORG.LABORATORY.READ）</summary>
    public class LabFunctionScope
    {
        public Guid TenantLabId { get; set; }

        public string PermissionKey { get; set; } = string.Empty;
    }

    public class LabFunctionPolicyConfiguration : IEntityTypeConfiguration<LabFunctionPolicy>
    {
        public void Configure(EntityTypeBuilder<LabFunctionPolicy> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("LabFunctionPolicies", "core");
            builder.HasKey(x => x.TenantLabId);
            builder.Property(x => x.UpdatedByName).HasMaxLength(256);
        }
    }

    public class LabFunctionScopeConfiguration : IEntityTypeConfiguration<LabFunctionScope>
    {
        public void Configure(EntityTypeBuilder<LabFunctionScope> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("LabFunctionScopes", "core");
            builder.HasKey(x => new { x.TenantLabId, x.PermissionKey });
            builder.Property(x => x.PermissionKey).HasMaxLength(256);
        }
    }
}
