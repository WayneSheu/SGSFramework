using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Abstractions.Entities.SystemSettings;
using SGSFramework.Identity.DTOs;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.Identity.Services
{
    /// <summary>
    /// Identity 規則的執行期套用器（Singleton）。
    /// <para>
    /// UserManager / SignInManager 透過 IOptions&lt;IdentityOptions&gt; 取得的是同一個 Singleton 實例，
    /// 且 IOptions 不會重新讀取組態；因此系統配置變更時直接更新該實例的屬性，使新規則對後續請求立即生效。
    /// 建構時先擷取 appsettings / 啟動設定的原始值作為「預設規則」，供管理頁面還原。
    /// </para>
    /// </summary>
    public sealed class IdentityPolicyRuntime
    {
        private readonly IdentityOptions _live;
        private readonly IdentityPolicySettingsDto _defaults;
        private readonly Lock _sync = new();

        public IdentityPolicyRuntime(IOptions<IdentityOptions> identityOptions)
        {
            ArgumentNullException.ThrowIfNull(identityOptions);

            _live = identityOptions.Value;
            _defaults = Capture(_live);
        }

        /// <summary>啟動時的預設規則（複本）</summary>
        public IdentityPolicySettingsDto Defaults => Clone(_defaults);

        /// <summary>目前生效中的規則</summary>
        public IdentityPolicySettingsDto Current
        {
            get
            {
                lock (_sync)
                {
                    return Capture(_live);
                }
            }
        }

        /// <summary>
        /// 將規則套用至執行中的 IdentityOptions
        /// </summary>
        public void Apply(IdentityPolicySettingsDto policy)
        {
            ArgumentNullException.ThrowIfNull(policy);

            lock (_sync)
            {
                var password = _live.Password;
                password.RequiredLength = policy.Password.RequiredLength;
                password.RequiredUniqueChars = policy.Password.RequiredUniqueChars;
                password.RequireDigit = policy.Password.RequireDigit;
                password.RequireLowercase = policy.Password.RequireLowercase;
                password.RequireUppercase = policy.Password.RequireUppercase;
                password.RequireNonAlphanumeric = policy.Password.RequireNonAlphanumeric;

                var lockout = _live.Lockout;
                lockout.AllowedForNewUsers = policy.Lockout.AllowedForNewUsers;
                lockout.MaxFailedAccessAttempts = policy.Lockout.MaxFailedAccessAttempts;
                lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(policy.Lockout.LockoutMinutes);
            }
        }

        private static IdentityPolicySettingsDto Capture(IdentityOptions options) => new()
        {
            Password = new PasswordPolicyDto
            {
                RequiredLength = options.Password.RequiredLength,
                RequiredUniqueChars = options.Password.RequiredUniqueChars,
                RequireDigit = options.Password.RequireDigit,
                RequireLowercase = options.Password.RequireLowercase,
                RequireUppercase = options.Password.RequireUppercase,
                RequireNonAlphanumeric = options.Password.RequireNonAlphanumeric
            },
            Lockout = new LockoutPolicyDto
            {
                AllowedForNewUsers = options.Lockout.AllowedForNewUsers,
                MaxFailedAccessAttempts = options.Lockout.MaxFailedAccessAttempts,
                LockoutMinutes = (int)Math.Clamp(options.Lockout.DefaultLockoutTimeSpan.TotalMinutes, 1, 1440)
            }
        };

        private static IdentityPolicySettingsDto Clone(IdentityPolicySettingsDto source) => new()
        {
            Password = new PasswordPolicyDto
            {
                RequiredLength = source.Password.RequiredLength,
                RequiredUniqueChars = source.Password.RequiredUniqueChars,
                RequireDigit = source.Password.RequireDigit,
                RequireLowercase = source.Password.RequireLowercase,
                RequireUppercase = source.Password.RequireUppercase,
                RequireNonAlphanumeric = source.Password.RequireNonAlphanumeric
            },
            Lockout = new LockoutPolicyDto
            {
                AllowedForNewUsers = source.Lockout.AllowedForNewUsers,
                MaxFailedAccessAttempts = source.Lockout.MaxFailedAccessAttempts,
                LockoutMinutes = source.Lockout.LockoutMinutes
            }
        };
    }

    /// <summary>
    /// 應用程式啟動時，將資料庫中已儲存的 Identity 規則套用至執行期（HostedService 於 Migration 完成後才啟動）。
    /// </summary>
    public sealed class IdentityPolicyStartupLoader(
        IServiceScopeFactory scopeFactory,
        IdentityPolicyRuntime runtime,
        ILogger<IdentityPolicyStartupLoader> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

                var stored = await dbContext.Set<SystemSetting>()
                    .AsNoTracking()
                    .Where(s => s.Key == SystemSettingKeys.IdentityPolicy)
                    .Select(s => s.Value)
                    .FirstOrDefaultAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(stored))
                {
                    logger.LogInformation("尚未自訂 Identity 規則，沿用 appsettings 預設值。");
                    return;
                }

                var policy = JsonSerializer.Deserialize<IdentityPolicySettingsDto>(stored, SystemSettingKeys.JsonOptions);
                if (policy is not null)
                {
                    runtime.Apply(policy);
                    logger.LogInformation("已套用資料庫中的 Identity 規則（密碼最小長度 {Length}、失敗鎖定 {Attempts} 次）。",
                        policy.Password.RequiredLength, policy.Lockout.MaxFailedAccessAttempts);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 不阻擋系統啟動：讀取失敗時沿用 appsettings 預設規則
                logger.LogError(ex, "載入資料庫 Identity 規則失敗，沿用 appsettings 預設值。");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>
    /// 系統配置分類鍵與序列化設定
    /// </summary>
    public static class SystemSettingKeys
    {
        public const string Branding = "Branding";
        public const string IdentityPolicy = "IdentityPolicy";

        /// <summary>SignalR 廣播原因代碼，前端據此重新載入外觀識別</summary>
        public const string ChangedReason = "system-settings-changed";

        internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    }
}
