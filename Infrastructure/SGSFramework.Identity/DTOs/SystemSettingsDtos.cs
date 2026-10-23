using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 系統配置總覽（管理頁面使用）
    /// </summary>
    public sealed class SystemSettingsDto
    {
        public BrandingSettingsDto Branding { get; init; } = new();

        /// <summary>目前生效的 Identity 規則</summary>
        public IdentityPolicySettingsDto IdentityPolicy { get; init; } = new();

        /// <summary>appsettings / 程式啟動時的 Identity 預設規則，供「還原預設」使用</summary>
        public IdentityPolicySettingsDto IdentityPolicyDefaults { get; init; } = new();
    }

    /// <summary>
    /// 外觀識別設定（Logo、系統標題）。登入頁未驗證即可讀取，請勿放入敏感資訊。
    /// </summary>
    public sealed class BrandingSettingsDto
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "系統標題不可為空。")]
        [StringLength(100, ErrorMessage = "系統標題長度不可超過 100 字。")]
        public string SystemTitle { get; set; } = string.Empty;

        /// <summary>Logo 圖片 (data:image/...;base64,...)；null 表示使用系統預設圖示</summary>
        public string? LogoDataUrl { get; set; }

        public DateTimeOffset? UpdatedAt { get; set; }

        public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// Identity 安全規則（密碼規則 + 帳號鎖定），儲存後立即套用於 UserManager。
    /// </summary>
    public sealed class IdentityPolicySettingsDto
    {
        [Required]
        public PasswordPolicyDto Password { get; set; } = new();

        [Required]
        public LockoutPolicyDto Lockout { get; set; } = new();

        public DateTimeOffset? UpdatedAt { get; set; }

        public string? UpdatedBy { get; set; }
    }

    public sealed class PasswordPolicyDto
    {
        [Range(6, 128, ErrorMessage = "密碼最小長度必須介於 6 到 128 之間。")]
        public int RequiredLength { get; set; } = 8;

        [Range(1, 128, ErrorMessage = "相異字元數必須介於 1 到 128 之間。")]
        public int RequiredUniqueChars { get; set; } = 1;

        public bool RequireDigit { get; set; } = true;

        public bool RequireLowercase { get; set; } = true;

        public bool RequireUppercase { get; set; } = true;

        public bool RequireNonAlphanumeric { get; set; } = true;
    }

    public sealed class LockoutPolicyDto
    {
        /// <summary>新建立的帳號是否啟用登入失敗鎖定</summary>
        public bool AllowedForNewUsers { get; set; } = true;

        [Range(1, 20, ErrorMessage = "登入失敗次數上限必須介於 1 到 20 之間。")]
        public int MaxFailedAccessAttempts { get; set; } = 5;

        [Range(1, 1440, ErrorMessage = "鎖定時間必須介於 1 到 1440 分鐘之間。")]
        public int LockoutMinutes { get; set; } = 15;
    }

    /// <summary>
    /// 系統配置異動歷程
    /// </summary>
    public sealed class SystemSettingChangeLogDto
    {
        public Guid Id { get; init; }
        public string SettingKey { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public string? ActorName { get; init; }
        public string? ClientIp { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }
}
