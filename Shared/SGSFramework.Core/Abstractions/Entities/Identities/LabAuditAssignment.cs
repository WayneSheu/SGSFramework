#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using System;
    using System.ComponentModel;

    /// <summary>
    /// 稽核指派：指定使用者於起訖期間內對某區域實驗室執行稽核職責。
    /// 有效期間內，使用者所持有的稽核角色（<see cref="ApplicationRole.RequiresAuditAssignment"/>）權限於該實驗室生效，
    /// 且該實驗室會出現在使用者可切換的實驗室清單中；期間外或撤銷後自動失效。紀錄不刪除，保留完整稽核軌跡。
    /// </summary>
    [DisplayName("實驗室稽核指派")]
    [Comment("實驗室稽核指派：指定使用者於起訖期間內對某區域實驗室執行稽核職責。有效期間內，使用者所持有的稽核角色（ApplicationRole.RequiresAuditAssignment）權限於該實驗室生效，且該實驗室會出現在使用者可切換的實驗室清單中；期間外或撤銷後自動失效。紀錄不刪除，保留完整稽核軌跡。")]
    public class LabAuditAssignment
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]   
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("使用者識別鍵")]
        [Comment("使用者識別鍵")] 
        public Guid UserId { get; set; }

        /// <summary>受稽核的區域實驗室（ORG Organization.TenantLabId）</summary>
        [DisplayName("受稽核的區域實驗室")]
        [Comment("受稽核的區域實驗室（ORG Organization.TenantLabId）")]
        public Guid TenantLabId { get; set; }

        /// <summary>生效起始（含）</summary>
        [DisplayName("生效起始")]
        [Comment("生效起始（含）")]
        public DateTimeOffset EffectiveFrom { get; set; }

        /// <summary>生效迄止（含）</summary>
        [DisplayName("生效迄止")]
        [Comment("生效迄止（含）")]
        public DateTimeOffset EffectiveTo { get; set; }

        /// <summary>稽核原因 / 稽核案號</summary>
        [DisplayName("稽核原因 / 稽核案號")]
        [Comment("稽核原因 / 稽核案號")]
        public string Reason { get; set; } = string.Empty;

        [DisplayName("指派人員識別鍵")]
        [Comment("指派人員識別鍵")]
        public Guid? AssignedBy { get; set; }

        [DisplayName("指派人員名稱")]
        [Comment("指派人員名稱")]
        public string? AssignedByName { get; set; }

        [DisplayName("指派時間")]
        [Comment("指派時間")]   
        public DateTimeOffset CreatedAt { get; set; }

        [DisplayName("撤銷時間")]
        [Comment("撤銷時間")]
        public DateTimeOffset? RevokedAt { get; set; }

        [DisplayName("撤銷人員識別鍵")]
        [Comment("撤銷人員識別鍵")]    
        public string? RevokedByName { get; set; }

        [DisplayName("撤銷原因")]
        [Comment("撤銷原因")]   
        public string? RevokeReason { get; set; }

        /// <summary>指定時間點是否有效（未撤銷且於起訖期間內）</summary>
        [DisplayName("指定時間點是否有效（未撤銷且於起訖期間內）")]
        public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && EffectiveFrom <= now && now <= EffectiveTo;
    
    }

    public class LabAuditAssignmentConfiguration : IEntityTypeConfiguration<LabAuditAssignment>
    {
        public void Configure(EntityTypeBuilder<LabAuditAssignment> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("LabAuditAssignments", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            builder.Property(x => x.AssignedByName).HasMaxLength(256);
            builder.Property(x => x.RevokedByName).HasMaxLength(256);
            builder.Property(x => x.RevokeReason).HasMaxLength(500);
            builder.HasIndex(x => new { x.UserId, x.TenantLabId });
            builder.HasIndex(x => x.TenantLabId);
        }
    }
}
