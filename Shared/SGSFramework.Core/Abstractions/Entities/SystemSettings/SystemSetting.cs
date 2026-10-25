using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.ComponentModel;

namespace SGSFramework.Core.Abstractions.Entities.SystemSettings
{
    /// <summary>
    /// 系統配置（管理者可於執行期調整的設定，例如外觀識別、密碼規則）。
    /// 以「分類鍵 + JSON 值」儲存，新增設定分類時不需異動資料表結構。
    /// </summary>
    [DisplayName("系統配置")]
    [Comment("系統配置（管理者可於執行期調整的設定，例如外觀識別、密碼規則）。以「分類鍵 + JSON 值」儲存，新增設定分類時不需異動資料表結構。")]
    public class SystemSetting
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]
        /// <summary>設定分類鍵（例如 Branding、IdentityPolicy）</summary>
        public string Key { get; set; } = string.Empty;

        [DisplayName("設定內容")]
        [Comment("設定內容（JSON）")]
        /// <summary>設定內容（JSON）</summary>
        public string Value { get; set; } = string.Empty;

        [DisplayName("更新者用戶識別鍵")]
        [Comment("更新者用戶識別鍵")]
        public Guid? UpdatedByUserId { get; set; }

        [DisplayName("更新者名稱")]
        [Comment("更新者名稱")]
        public string? UpdatedByName { get; set; }

        [DisplayName("更新時間")]
        [Comment("更新時間")]
        public DateTimeOffset UpdatedAt { get; set; }
    }

    /// <summary>
    /// 系統配置異動歷程（稽核用，僅新增不修改）。
    /// Summary 為可讀的欄位差異，Logo 等大型內容僅記錄「已更新 / 已移除」。
    /// </summary>
    [DisplayName("系統配置異動歷程")]
    [Comment("系統配置異動歷程（稽核用，僅新增不修改）")]
    public class SystemSettingChangeLog
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("設定分類鍵")]
        [Comment("設定分類鍵")]
        public string SettingKey { get; set; } = string.Empty;

        [DisplayName("異動摘要")]
        [Comment("異動摘要")]
        public string Summary { get; set; } = string.Empty;

        [DisplayName("操作者用戶識別鍵")]
        [Comment("操作者用戶識別鍵")]
        public Guid? ActorUserId { get; set; }

        [DisplayName("操作者名稱")]
        [Comment("操作者名稱")]
        public string? ActorName { get; set; }

        [DisplayName("客戶端 IP")]
        [Comment("客戶端 IP")]
        public string? ClientIp { get; set; }

        [DisplayName("建立時間")]
        [Comment("建立時間")]
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
    {
        public void Configure(EntityTypeBuilder<SystemSetting> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("SystemSettings", "core");
            builder.HasKey(x => x.Key);
            builder.Property(x => x.Key).HasMaxLength(100);
            builder.Property(x => x.Value).IsRequired();
            builder.Property(x => x.UpdatedByName).HasMaxLength(256);
        }
    }

    public class SystemSettingChangeLogConfiguration : IEntityTypeConfiguration<SystemSettingChangeLog>
    {
        public void Configure(EntityTypeBuilder<SystemSettingChangeLog> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("SystemSettingChangeLogs", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.SettingKey).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
            builder.Property(x => x.ActorName).HasMaxLength(256);
            builder.Property(x => x.ClientIp).HasMaxLength(64);
            builder.HasIndex(x => x.CreatedAt);
        }
    }
}
