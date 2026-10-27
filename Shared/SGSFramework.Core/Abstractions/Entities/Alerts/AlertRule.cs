using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SGSFramework.Core.Abstractions.Entities.Alerts
{
    /// <summary>告警規則的 LogType 取值</summary>
    public static class AlertRuleLogTypes
    {
        public const string Any = "Any";
        public const string System = "System";
        public const string Security = "Security";

        public static readonly string[] All = [Any, System, Security];
    }

    /// <summary>告警規則的動作取值</summary>
    public static class AlertRuleActions
    {
        /// <summary>符合時發送告警</summary>
        public const string Alert = "Alert";

        /// <summary>符合時不告警（用於排除已知雜訊，需排在較高優先序）</summary>
        public const string Suppress = "Suppress";

        public static readonly string[] All = [Alert, Suppress];
    }

    /// <summary>
    /// 告警規則：管理者可於執行期調整的日誌告警條件。
    /// 依 <see cref="Priority"/> 由小到大逐條比對，第一條符合者決定結果（Alert 發送 / Suppress 抑制）；
    /// 沒有任何規則符合則不告警。條件欄位皆為「且」關係，未填的條件視為不限制。
    /// </summary>
    [DisplayName("告警規則")]
    [Comment("告警規則：管理者可於執行期調整的日誌告警條件。依優先序由小到大比對，第一條符合者決定結果。")]
    public class AlertRule : IAuditable
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("規則名稱")]
        [Comment("規則名稱（唯一）")]
        public string Name { get; set; } = string.Empty;

        [DisplayName("說明")]
        [Comment("規則說明")]
        public string? Description { get; set; }

        [DisplayName("是否啟用")]
        [Comment("是否啟用；停用的規則不參與比對")]
        public bool IsEnabled { get; set; } = true;

        [DisplayName("優先序")]
        [Comment("優先序；數字越小越先比對")]
        public int Priority { get; set; } = 100;

        [DisplayName("日誌類型")]
        [Comment("套用的日誌類型：Any / System / Security")]
        public string LogType { get; set; } = AlertRuleLogTypes.Any;

        [DisplayName("最低層級")]
        [Comment("最低日誌層級（Verbose / Debug / Information / Warning / Error / Fatal），含此層級以上才符合")]
        public string MinLevel { get; set; } = "Error";

        [DisplayName("訊息關鍵字")]
        [Comment("訊息包含的關鍵字（不分大小寫），可填事件碼如 SEC-911")]
        public string? MessageContains { get; set; }

        [DisplayName("模組")]
        [Comment("日誌的模組名稱包含此字串（不分大小寫）")]
        public string? ModuleContains { get; set; }

        [DisplayName("操作")]
        [Comment("日誌的操作名稱包含此字串（不分大小寫）")]
        public string? OperationContains { get; set; }

        [DisplayName("例外類型")]
        [Comment("例外型別名稱包含此字串（不分大小寫），例如 SqlException")]
        public string? ExceptionContains { get; set; }

        [DisplayName("動作")]
        [Comment("符合時的動作：Alert / Suppress")]
        public string Action { get; set; } = AlertRuleActions.Alert;

        [DisplayName("冷卻秒數")]
        [Comment("相同告警的冷卻秒數；空值沿用全域設定")]
        public int? CooldownSeconds { get; set; }

        [DisplayName("收件人")]
        [Comment("此規則的收件人（逗號分隔 Email）；空值沿用全域收件人")]
        public string? Recipients { get; set; }

        // --- IAuditable 實作（[AuditIgnore] 已標於介面成員） ---
        [Editable(false)]
        [DisplayName("建立者")]
        [Comment("建立者")]
        public string CreatedBy { get; set; } = string.Empty;

        [Editable(false)]
        [DisplayName("建立時間 (UTC)")]
        [Comment("建立時間 (UTC)")]
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        [DisplayName("最後修改者")]
        [Comment("最後修改者")]
        public string? UpdatedBy { get; set; }

        [DisplayName("最後修改時間 (UTC)")]
        [Comment("最後修改時間 (UTC)")]
        public DateTimeOffset? UpdatedAtUtc { get; set; }
    }

    public class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
    {
        public void Configure(EntityTypeBuilder<AlertRule> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("AlertRules", "core");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.LogType).HasMaxLength(20).IsUnicode(false).IsRequired();
            builder.Property(x => x.MinLevel).HasMaxLength(20).IsUnicode(false).IsRequired();
            builder.Property(x => x.MessageContains).HasMaxLength(200);
            builder.Property(x => x.ModuleContains).HasMaxLength(100);
            builder.Property(x => x.OperationContains).HasMaxLength(100);
            builder.Property(x => x.ExceptionContains).HasMaxLength(200);
            builder.Property(x => x.Action).HasMaxLength(20).IsUnicode(false).IsRequired();
            builder.Property(x => x.Recipients).HasMaxLength(1000);
            builder.Property(x => x.CreatedBy).HasMaxLength(256);
            builder.Property(x => x.UpdatedBy).HasMaxLength(256);

            builder.HasIndex(x => x.Name).IsUnique();
            builder.HasIndex(x => new { x.IsEnabled, x.Priority });
        }
    }
}
