using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.ComponentModel;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 角色異動歷程（稽核用，僅新增不修改）。
    /// 記錄角色狀態、設定、權限、成員、AD 群組等所有異動，含操作者、時間與影響人數。
    /// </summary>
    [DisplayName("角色異動歷程")]
    [Comment("角色異動歷程（稽核用，僅新增不修改）。記錄角色狀態、設定、權限、成員、AD 群組等所有異動，含操作者、時間與影響人數。")]
    public class RoleAuditLog
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]   
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("角色識別鍵")]
        [Comment("角色識別鍵")]  
        public Guid RoleId { get; set; }

        /// <summary>異動當下的角色名稱（角色刪除或更名後仍可辨識）</summary>
        [DisplayName("角色名稱")]
        [Comment("異動當下的角色名稱（角色刪除或更名後仍可辨識）")]
        public string RoleName { get; set; } = string.Empty;

        /// <summary>動作代碼，見 <see cref="RoleAuditActions"/></summary>
        [DisplayName("動作代碼")]
        [Comment("動作代碼，見 RoleAuditActions")]
        public string Action { get; set; } = string.Empty;

        /// <summary>人可讀的異動摘要</summary>
        [DisplayName("異動摘要")]
        [Comment("人可讀的異動摘要")]
        public string Summary { get; set; } = string.Empty;

        /// <summary>補充明細（JSON，例如權限差異、成員清單）</summary>
        [DisplayName("補充明細")]
        [Comment("補充明細（JSON，例如權限差異、成員清單）")]
        public string? Detail { get; set; }

        /// <summary>受影響的使用者人數</summary>
        [DisplayName("受影響的使用者人數")]
        [Comment("受影響的使用者人數")]
        public int AffectedUserCount { get; set; }

        [DisplayName("操作者用戶識別鍵")]
        [Comment("操作者用戶識別鍵")]
        public Guid? ActorUserId { get; set; }

        [DisplayName("操作者名稱")]
        [Comment("操作者名稱")]
        public string? ActorName { get; set; }

        [DisplayName("建立時間")]
        [Comment("建立時間")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public static class RoleAuditActions
    {
        public const string Created = "Created";
        public const string Updated = "Updated";
        public const string StatusChanged = "StatusChanged";
        public const string Deleted = "Deleted";
        public const string PermissionsUpdated = "PermissionsUpdated";
        public const string MembersAdded = "MembersAdded";
        public const string MembersRemoved = "MembersRemoved";
        public const string MemberExpired = "MemberExpired";
        public const string AdGroupMapped = "AdGroupMapped";
        public const string AdGroupRemoved = "AdGroupRemoved";
        public const string ChangeRequested = "ChangeRequested";
        public const string ChangeApproved = "ChangeApproved";
        public const string ChangeRejected = "ChangeRejected";
        public const string AccessReviewed = "AccessReviewed";
    }

    public class RoleAuditLogConfiguration : IEntityTypeConfiguration<RoleAuditLog>
    {
        public void Configure(EntityTypeBuilder<RoleAuditLog> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("RoleAuditLogs", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.RoleName).HasMaxLength(256).IsRequired();
            builder.Property(x => x.Action).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Summary).HasMaxLength(1000).IsRequired();
            builder.Property(x => x.ActorName).HasMaxLength(256);
            builder.HasIndex(x => new { x.RoleId, x.CreatedAt });
        }
    }
}
