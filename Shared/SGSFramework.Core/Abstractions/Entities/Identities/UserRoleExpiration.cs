using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.ComponentModel;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 過渡期角色（Transition Role）：角色指派的有效期限。
    /// 調職交接時保留舊角色一段時間，到期由排程自動回收，避免權限累積 (Privilege Creep)。
    /// 每位使用者在同一角色上最多一筆未處理的期限。
    /// </summary>
    [DisplayName("過渡期角色（Transition Role）")]
    [Comment("過渡期角色（Transition Role）：角色指派的有效期限。調職交接時保留舊角色一段時間，到期由排程自動回收，避免權限累積 (Privilege Creep)。每位使用者在同一角色上最多一筆未處理的期限。")]
    public class UserRoleExpiration
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]   
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("使用者識別鍵")]
        [Comment("使用者識別鍵")] 
        public Guid UserId { get; set; }

        [DisplayName("角色識別鍵")]
        [Comment("角色識別鍵")]  
        public Guid RoleId { get; set; }

        /// <summary>到期時間（UTC），到期後由排程自動解除角色指派</summary>
        [DisplayName("到期時間（UTC）")]
        [Comment("到期時間（UTC），到期後由排程自動解除角色指派")]
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>設定原因，例如「調任銷售部，業務交接」</summary>
        [DisplayName("設定原因")]
        [Comment("設定原因，例如「調任銷售部，業務交接」")]
        public string? Reason { get; set; }

        [DisplayName("建立者")]
        [Comment("建立者")]
        public string? CreatedBy { get; set; }

        [DisplayName("建立時間")]
        [Comment("建立時間")]   
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class UserRoleExpirationConfiguration : IEntityTypeConfiguration<UserRoleExpiration>
    {
        public void Configure(EntityTypeBuilder<UserRoleExpiration> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("UserRoleExpirations", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.Property(x => x.CreatedBy).HasMaxLength(256);
            builder.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
            builder.HasIndex(x => x.ExpiresAt);
        }
    }
}
