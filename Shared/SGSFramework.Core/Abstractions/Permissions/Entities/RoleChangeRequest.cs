using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.Identities;
using System;

namespace SGSFramework.Core.Abstractions.Permissions.Entities
{
    /// <summary>
    /// 角色權限變更申請（Maker-Checker 雙重審批）。
    /// 提出者 (Maker) 送出後不會立即生效，須由另一位具審核權限者 (Checker) 核准才套用。
    /// </summary>
    public class RoleChangeRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid RoleId { get; set; }

        public string RoleName { get; set; } = string.Empty;

        /// <summary>核准後要套用的完整權限鍵清單（JSON 陣列）</summary>
        public string TargetPermissionKeys { get; set; } = "[]";

        /// <summary>相對於送出當下的新增權限（JSON 陣列）</summary>
        public string AddedPermissionKeys { get; set; } = "[]";

        /// <summary>相對於送出當下的移除權限（JSON 陣列）</summary>
        public string RemovedPermissionKeys { get; set; } = "[]";

        /// <summary>送出當下的角色成員數（影響範圍）</summary>
        public int AffectedUserCount { get; set; }

        public RoleChangeRequestStatus Status { get; set; } = RoleChangeRequestStatus.Pending;

        /// <summary>提出者說明（變更原因）</summary>
        public string? RequestReason { get; set; }

        public Guid RequestedBy { get; set; }

        public string RequestedByName { get; set; } = string.Empty;

        public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

        public Guid? ReviewedBy { get; set; }

        public string? ReviewedByName { get; set; }

        public DateTimeOffset? ReviewedAt { get; set; }

        public string? ReviewComment { get; set; }

        /// <summary>
        /// 送出申請當下的角色狀態。送審期間角色標示為「待審核」，審核結束（核准 / 駁回 / 撤回）後據此恢復；
        /// 原為「啟用中」者送審期間仍維持既有權限，原為「待審核」（新建角色）者核准時一併啟用。
        /// </summary>
        public RoleStatus? PreviousRoleStatus { get; set; }
    }

    public enum RoleChangeRequestStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
        Cancelled = 3
    }

    public class RoleChangeRequestConfiguration : IEntityTypeConfiguration<RoleChangeRequest>
    {
        public void Configure(EntityTypeBuilder<RoleChangeRequest> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("RoleChangeRequests", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.RoleName).HasMaxLength(256).IsRequired();
            builder.Property(x => x.RequestedByName).HasMaxLength(256).IsRequired();
            builder.Property(x => x.ReviewedByName).HasMaxLength(256);
            builder.Property(x => x.RequestReason).HasMaxLength(1000);
            builder.Property(x => x.ReviewComment).HasMaxLength(1000);
            builder.HasIndex(x => new { x.Status, x.RequestedAt });
            builder.HasIndex(x => x.RoleId);
        }
    }

    /// <summary>
    /// 職能分立規則 (SoD, Separation of Duties)：兩項互斥權限不得同時存在於同一角色。
    /// 例如「建立付款單」與「核准付款單」。
    /// </summary>
    public class SodRule
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string PermissionKeyA { get; set; } = string.Empty;

        public string PermissionKeyB { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public string? CreatedBy { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class SodRuleConfiguration : IEntityTypeConfiguration<SodRule>
    {
        public void Configure(EntityTypeBuilder<SodRule> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("SodRules", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(1000);
            builder.Property(x => x.PermissionKeyA).HasMaxLength(256).IsRequired();
            builder.Property(x => x.PermissionKeyB).HasMaxLength(256).IsRequired();
            builder.Property(x => x.CreatedBy).HasMaxLength(256);
        }
    }
}
