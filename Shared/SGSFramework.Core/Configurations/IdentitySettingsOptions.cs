// Path: src/SGSFramework/Shared/SGSFramework.Core/Configurations/IdentitySettingsOptions.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;

namespace SGSFramework.Core.Configurations
{
    /// <summary>
    /// Identity 預設規則組態 (對應 appsettings 的 PersistentSettings:IdentitySettings)。
    /// 為系統配置頁「套用預設值」的來源；管理者於系統配置頁儲存的規則會在執行期覆蓋此預設。
    /// </summary>
    public sealed class IdentitySettingsOptions
    {
        public const string SectionName = "PersistentSettings:IdentitySettings";

        public PasswordSettings Password { get; set; } = new();

        public LockoutSettings Lockout { get; set; } = new();

        public UserSettings User { get; set; } = new();

        public sealed class PasswordSettings
        {
            [Range(6, 128)]
            public int RequiredLength { get; set; } = 8;

            [Range(1, 128)]
            public int RequiredUniqueChars { get; set; } = 1;

            public bool RequireDigit { get; set; } = true;
            public bool RequireLowercase { get; set; } = true;
            public bool RequireUppercase { get; set; } = true;
            public bool RequireNonAlphanumeric { get; set; } = true;
        }

        public sealed class LockoutSettings
        {
            /// <summary>
            /// 鎖定時間 (分鐘)；組態鍵沿用既有的 DefaultLockoutTimeSpan
            /// </summary>
            [ConfigurationKeyName("DefaultLockoutTimeSpan")]
            [Range(1, 1440)]
            public int DefaultLockoutMinutes { get; set; } = 15;

            [Range(1, 20)]
            public int MaxFailedAccessAttempts { get; set; } = 5;

            public bool AllowedForNewUsers { get; set; } = true;
        }

        public sealed class UserSettings
        {
            public bool RequireUniqueEmail { get; set; } = true;
        }

        /// <summary>
        /// 將組態套用至 ASP.NET Core Identity 選項
        /// </summary>
        public void ApplyTo(IdentityOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.Password.RequiredLength = Password.RequiredLength;
            options.Password.RequiredUniqueChars = Password.RequiredUniqueChars;
            options.Password.RequireDigit = Password.RequireDigit;
            options.Password.RequireLowercase = Password.RequireLowercase;
            options.Password.RequireUppercase = Password.RequireUppercase;
            options.Password.RequireNonAlphanumeric = Password.RequireNonAlphanumeric;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(Lockout.DefaultLockoutMinutes);
            options.Lockout.MaxFailedAccessAttempts = Lockout.MaxFailedAccessAttempts;
            options.Lockout.AllowedForNewUsers = Lockout.AllowedForNewUsers;

            options.User.RequireUniqueEmail = User.RequireUniqueEmail;
        }
    }
}
