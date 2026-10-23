using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Entities.SystemSettings;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SGSFramework.Identity.Services
{
    /// <summary>
    /// 系統配置管理服務實作：設定持久化於 core.SystemSettings，每次異動寫入 core.SystemSettingChangeLogs，
    /// 並透過 SignalR 廣播通知所有前端重新載入。
    /// </summary>
    public sealed partial class SystemSettingsService(
        DbContext dbContext,
        IMemoryCache cache,
        ICurrentUserService currentUserService,
        IPermissionChangeNotifier permissionChangeNotifier,
        IdentityPolicyRuntime identityPolicyRuntime,
        TimeProvider timeProvider,
        ILogger<SystemSettingsService> logger) : ISystemSettingsService
    {
        /// <summary>未自訂時的預設系統標題</summary>
        public const string DefaultSystemTitle = "PhysLIMS 實驗室資訊管理系統";

        /// <summary>Logo 圖檔大小上限 (解碼後位元組)</summary>
        public const int MaxLogoBytes = 256 * 1024;

        private const string BrandingCacheKey = "system-settings:branding";
        private static readonly TimeSpan BrandingCacheDuration = TimeSpan.FromMinutes(10);

        private readonly DbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        private readonly ICurrentUserService _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        private readonly IPermissionChangeNotifier _permissionChangeNotifier = permissionChangeNotifier ?? throw new ArgumentNullException(nameof(permissionChangeNotifier));
        private readonly IdentityPolicyRuntime _identityPolicyRuntime = identityPolicyRuntime ?? throw new ArgumentNullException(nameof(identityPolicyRuntime));
        private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        private readonly ILogger<SystemSettingsService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        [GeneratedRegex(@"^data:image/(png|jpeg|gif|webp|svg\+xml);base64,([A-Za-z0-9+/]+={0,2})$", RegexOptions.CultureInvariant)]
        private static partial Regex LogoDataUrlRegex();

        public async Task<SystemSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            var branding = await GetBrandingAsync(cancellationToken);

            var identityRow = await FindAsync(SystemSettingKeys.IdentityPolicy, tracking: false, cancellationToken);
            var identityPolicy = _identityPolicyRuntime.Current;
            identityPolicy.UpdatedAt = identityRow?.UpdatedAt;
            identityPolicy.UpdatedBy = identityRow?.UpdatedByName;

            return new SystemSettingsDto
            {
                Branding = branding,
                IdentityPolicy = identityPolicy,
                IdentityPolicyDefaults = _identityPolicyRuntime.Defaults
            };
        }

        public async Task<BrandingSettingsDto> GetBrandingAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(BrandingCacheKey, out BrandingSettingsDto? cached) && cached is not null)
            {
                return cached;
            }

            var row = await FindAsync(SystemSettingKeys.Branding, tracking: false, cancellationToken);
            var branding = Deserialize<BrandingSettingsDto>(row) ?? new BrandingSettingsDto();

            if (string.IsNullOrWhiteSpace(branding.SystemTitle))
            {
                branding.SystemTitle = DefaultSystemTitle;
            }

            branding.UpdatedAt = row?.UpdatedAt;
            branding.UpdatedBy = row?.UpdatedByName;

            _cache.Set(BrandingCacheKey, branding, BrandingCacheDuration);
            return branding;
        }

        public async Task<BrandingSettingsDto> UpdateBrandingAsync(BrandingSettingsDto branding, string? clientIp, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(branding);

            var normalized = new BrandingSettingsDto
            {
                SystemTitle = branding.SystemTitle?.Trim() ?? string.Empty,
                LogoDataUrl = string.IsNullOrWhiteSpace(branding.LogoDataUrl) ? null : branding.LogoDataUrl.Trim()
            };

            if (normalized.SystemTitle.Length is 0 or > 100)
            {
                throw new ArgumentException("系統標題不可為空，且長度不可超過 100 字。");
            }

            if (normalized.LogoDataUrl is not null)
            {
                ValidateLogo(normalized.LogoDataUrl);
            }

            var previous = await GetBrandingAsync(cancellationToken);

            var changes = new List<string>();
            if (!string.Equals(previous.SystemTitle, normalized.SystemTitle, StringComparison.Ordinal))
            {
                changes.Add($"系統標題：{previous.SystemTitle} → {normalized.SystemTitle}");
            }
            if (!string.Equals(previous.LogoDataUrl, normalized.LogoDataUrl, StringComparison.Ordinal))
            {
                changes.Add(normalized.LogoDataUrl is null ? "Logo：已移除（改用預設圖示）" : "Logo：已更新");
            }

            if (changes.Count == 0)
            {
                return previous;
            }

            await SaveAsync(SystemSettingKeys.Branding, normalized, changes, clientIp, cancellationToken);
            _cache.Remove(BrandingCacheKey);

            await BroadcastAsync(cancellationToken);
            return await GetBrandingAsync(cancellationToken);
        }

        public async Task<IdentityPolicySettingsDto> UpdateIdentityPolicyAsync(IdentityPolicySettingsDto policy, string? clientIp, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(policy);
            ArgumentNullException.ThrowIfNull(policy.Password);
            ArgumentNullException.ThrowIfNull(policy.Lockout);

            if (policy.Password.RequiredUniqueChars > policy.Password.RequiredLength)
            {
                throw new ArgumentException("相異字元數不可大於密碼最小長度。");
            }

            var normalized = new IdentityPolicySettingsDto
            {
                Password = policy.Password,
                Lockout = policy.Lockout
            };

            var previous = _identityPolicyRuntime.Current;
            var changes = DescribeChanges(previous, normalized);

            // 與 appsettings 預設規則相同時不保留自訂設定，讓 appsettings 持續作為預設來源 (日後調整 appsettings 即可生效)
            var isDefault = DescribeChanges(_identityPolicyRuntime.Defaults, normalized).Count == 0;
            if (changes.Count == 0)
            {
                if (isDefault)
                {
                    await SaveAsync<IdentityPolicySettingsDto>(SystemSettingKeys.IdentityPolicy, null, changes, clientIp, cancellationToken);
                }
                return previous;
            }

            if (isDefault)
            {
                changes.Add("已恢復為 appsettings 預設規則");
            }

            await SaveAsync(SystemSettingKeys.IdentityPolicy, isDefault ? null : normalized, changes, clientIp, cancellationToken);

            // 寫入成功後才套用，避免資料庫與執行期規則不一致
            _identityPolicyRuntime.Apply(normalized);
            _logger.LogInformation("Identity 規則已更新並立即生效：{Changes}", string.Join("；", changes));

            await BroadcastAsync(cancellationToken);

            var current = _identityPolicyRuntime.Current;
            if (!isDefault)
            {
                current.UpdatedAt = _timeProvider.GetUtcNow();
                current.UpdatedBy = _currentUserService.UserName;
            }
            return current;
        }

        public async Task<IReadOnlyList<SystemSettingChangeLogDto>> GetChangeLogsAsync(int take, CancellationToken cancellationToken = default)
        {
            take = Math.Clamp(take, 1, 500);

            return await _dbContext.Set<SystemSettingChangeLog>()
                .AsNoTracking()
                .OrderByDescending(l => l.CreatedAt)
                .Take(take)
                .Select(l => new SystemSettingChangeLogDto
                {
                    Id = l.Id,
                    SettingKey = l.SettingKey,
                    Summary = l.Summary,
                    ActorName = l.ActorName,
                    ClientIp = l.ClientIp,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }

        private static void ValidateLogo(string dataUrl)
        {
            var match = LogoDataUrlRegex().Match(dataUrl);
            if (!match.Success)
            {
                throw new ArgumentException("Logo 格式不正確，僅支援 PNG、JPEG、GIF、WebP、SVG 圖檔。");
            }

            var base64 = match.Groups[2].Value;
            var padding = base64.EndsWith("==", StringComparison.Ordinal) ? 2 : base64.EndsWith('=') ? 1 : 0;
            var decodedBytes = (base64.Length / 4 * 3) - padding;

            if (decodedBytes > MaxLogoBytes)
            {
                throw new ArgumentException($"Logo 檔案大小不可超過 {MaxLogoBytes / 1024} KB。");
            }
        }

        private static List<string> DescribeChanges(IdentityPolicySettingsDto before, IdentityPolicySettingsDto after)
        {
            var changes = new List<string>();

            void Compare<T>(string label, T oldValue, T newValue)
            {
                if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
                {
                    changes.Add($"{label}：{Format(oldValue)} → {Format(newValue)}");
                }
            }

            Compare("密碼最小長度", before.Password.RequiredLength, after.Password.RequiredLength);
            Compare("相異字元數", before.Password.RequiredUniqueChars, after.Password.RequiredUniqueChars);
            Compare("需含數字", before.Password.RequireDigit, after.Password.RequireDigit);
            Compare("需含小寫字母", before.Password.RequireLowercase, after.Password.RequireLowercase);
            Compare("需含大寫字母", before.Password.RequireUppercase, after.Password.RequireUppercase);
            Compare("需含特殊符號", before.Password.RequireNonAlphanumeric, after.Password.RequireNonAlphanumeric);
            Compare("新帳號啟用鎖定", before.Lockout.AllowedForNewUsers, after.Lockout.AllowedForNewUsers);
            Compare("登入失敗次數上限", before.Lockout.MaxFailedAccessAttempts, after.Lockout.MaxFailedAccessAttempts);
            Compare("鎖定時間(分鐘)", before.Lockout.LockoutMinutes, after.Lockout.LockoutMinutes);

            return changes;
        }

        private static string Format<T>(T value) => value is bool b ? (b ? "是" : "否") : value?.ToString() ?? "—";

        /// <summary>
        /// 儲存設定並寫入異動紀錄；<paramref name="value"/> 為 null 時移除自訂設定 (回到 appsettings 預設)
        /// </summary>
        private async Task SaveAsync<T>(string key, T? value, IReadOnlyList<string> changes, string? clientIp, CancellationToken cancellationToken)
            where T : class
        {
            var now = _timeProvider.GetUtcNow();
            Guid? actorId = Guid.TryParse(_currentUserService.UserId, out var parsed) ? parsed : null;
            var actorName = _currentUserService.UserName;

            var row = await FindAsync(key, tracking: true, cancellationToken);
            if (value is null)
            {
                if (row is not null)
                {
                    _dbContext.Set<SystemSetting>().Remove(row);
                }
            }
            else
            {
                if (row is null)
                {
                    row = new SystemSetting { Key = key };
                    _dbContext.Set<SystemSetting>().Add(row);
                }

                row.Value = JsonSerializer.Serialize(value, SystemSettingKeys.JsonOptions);
                row.UpdatedAt = now;
                row.UpdatedByUserId = actorId;
                row.UpdatedByName = actorName;
            }

            if (changes.Count == 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var summary = string.Join("；", changes);
            _dbContext.Set<SystemSettingChangeLog>().Add(new SystemSettingChangeLog
            {
                SettingKey = key,
                Summary = summary.Length > 2000 ? summary[..2000] : summary,
                ActorUserId = actorId,
                ActorName = actorName,
                ClientIp = clientIp,
                CreatedAt = now
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private Task<SystemSetting?> FindAsync(string key, bool tracking, CancellationToken cancellationToken)
        {
            var query = _dbContext.Set<SystemSetting>().Where(s => s.Key == key);
            return (tracking ? query : query.AsNoTracking()).FirstOrDefaultAsync(cancellationToken);
        }

        private static T? Deserialize<T>(SystemSetting? row) where T : class =>
            string.IsNullOrWhiteSpace(row?.Value) ? null : JsonSerializer.Deserialize<T>(row.Value, SystemSettingKeys.JsonOptions);

        private async Task BroadcastAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _permissionChangeNotifier.BroadcastAsync(SystemSettingKeys.ChangedReason, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 設定已儲存，推播失敗僅影響即時性（前端重新整理後即可取得）
                _logger.LogWarning(ex, "系統配置異動推播失敗。");
            }
        }
    }
}
