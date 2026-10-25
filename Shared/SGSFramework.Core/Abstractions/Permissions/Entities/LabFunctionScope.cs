using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.ComponentModel;

namespace SGSFramework.Core.Abstractions.Permissions.Entities
{
    /// <summary>
    /// 區域實驗室的功能開放政策。IsRestricted = false（或無資料）表示所有功能開放；
    /// IsRestricted = true 時，僅 <see cref="LabFunctionScope"/> 列出的權限於該實驗室生效（對所有使用者與角色的上限，系統管理員除外）。
    /// </summary>
    [DisplayName("區域實驗室功能開放政策")]
    [Comment("區域實驗室的功能開放政策。IsRestricted = false（或無資料）表示所有功能開放；IsRestricted = true 時，僅 LabFunctionScope 列出的權限於該實驗室生效（對所有使用者與角色的上限，系統管理員除外）。")]
    public class LabFunctionPolicy
    {
        /// <summary>區域實驗室識別碼（ORG Organization.TenantLabId）</summary>
        [DisplayName("區域實驗室識別碼（ORG Organization.TenantLabId）")]
        [Comment("區域實驗室識別碼（ORG Organization.TenantLabId）")]
        public Guid TenantLabId { get; set; }

        [DisplayName("是否限制功能")]
        [Comment("是否限制功能")]
        public bool IsRestricted { get; set; }

        [DisplayName("最後更新時間")]
        [Comment("最後更新時間")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [DisplayName("最後更新人員識別鍵")]
        [Comment("最後更新人員識別鍵")]  
        public Guid? UpdatedBy { get; set; }

        [DisplayName("最後更新人員名稱")]
        [Comment("最後更新人員名稱")]   
        public string? UpdatedByName { get; set; }
    }

    /// <summary>區域實驗室開放的權限（完整權限鍵，例：ORG.LABORATORY.READ）</summary>
    [DisplayName("區域實驗室開放的權限")]
    [Comment("區域實驗室開放的權限（完整權限鍵，例：ORG.LABORATORY.READ）")]
    public class LabFunctionScope
    {
        [DisplayName("區域實驗室識別碼（ORG Organization.TenantLabId）")]
        [Comment("區域實驗室識別碼（ORG Organization.TenantLabId）")]
        public Guid TenantLabId { get; set; }

        [DisplayName("權限鍵")]
        [Comment("權限鍵")]
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
