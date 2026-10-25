#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using System;
    using System.ComponentModel;

    /// <summary>
    /// 區域實驗室主管（以 TenantLabId 對應 ORG 區域實驗室，與 UserLabMapping 相同的鍵值維度）
    /// </summary>
    [DisplayName("區域實驗室主管")]
    [Comment("區域實驗室主管（以 TenantLabId 對應 ORG 區域實驗室，與 UserLabMapping 相同的鍵值維度）")]
    public class LabManager
    {
        [DisplayName("區域實驗室識別鍵")]
        [Comment("區域實驗室識別鍵")]   
        public Guid TenantLabId { get; set; }

        [DisplayName("主管使用者識別鍵")]
        [Comment("主管使用者識別鍵")]   
        public Guid ManagerUserId { get; set; }

        [DisplayName("最後更新時間")]
        [Comment("最後更新時間")] 
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [DisplayName("最後更新人員名稱")]
        [Comment("最後更新人員名稱")]   
        public string? UpdatedByName { get; set; }
    }

    public class LabManagerConfiguration : IEntityTypeConfiguration<LabManager>
    {
        public void Configure(EntityTypeBuilder<LabManager> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("LabManagers", "core");
            builder.HasKey(x => x.TenantLabId);
            builder.HasIndex(x => x.ManagerUserId);
            builder.Property(x => x.UpdatedByName).HasMaxLength(256);
        }
    }
}
