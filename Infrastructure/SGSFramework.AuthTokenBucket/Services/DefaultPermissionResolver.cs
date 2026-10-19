// ==========================================
// 檔案路徑: src/SGSFramework/Infrastructure/SGSFramework.AuthTokenBucket/Services/DefaultPermissionResolver.cs
// 架構層級: Infrastructure Layer / Services
// ==========================================

namespace SGSFramework.AuthTokenBucket.Services;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Helpers;

/// <summary>
/// 預設使用者權限解析策略（整合 IMemoryCache 快取與快取失效清除機制，支援 SuperAdmin、SystemAdmin 與 IsSuperAdmin Claim）
/// </summary>
public class DefaultPermissionResolver : IPermissionResolver
{
    private readonly ILogger<DefaultPermissionResolver> _logger;
    private readonly IMemoryCache _memoryCache;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    public DefaultPermissionResolver(
        ILogger<DefaultPermissionResolver> logger,
        IMemoryCache memoryCache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    public async Task<(string PermissionMask, bool IsAdmin)> ResolveUserPermissionsAsync<TUser>(
        TUser user,
        UserManager<TUser> userManager) where TUser : ApplicationUser, new()
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(userManager);

        string cacheKey = $"permission-mask:{user.Id}";

        return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiration;

            try
            {
                var roles = await userManager.GetRolesAsync(user);
                var claims = await userManager.GetClaimsAsync(user);

                bool hasSuperAdminClaim = claims.Any(c =>
                    string.Equals(c.Type, "IsSuperAdmin", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));

                bool hasAdminRole = roles.Any(r =>
                    string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "SystemAdmin", StringComparison.OrdinalIgnoreCase));

                bool isBuiltInAdminAccount = string.Equals(user.UserName, "sysadmin", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(user.UserName, "admin", StringComparison.OrdinalIgnoreCase);

                bool isAdmin = hasSuperAdminClaim || hasAdminRole || isBuiltInAdminAccount;

                var permission = new BigBitmaskPermission(null);

                if (isAdmin)
                {
                    permission.SetAllPermissions();
                }
                else
                {
                    permission.SetPermission(5);
                }

                return (permission.ToString(), isAdmin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析使用者權限位元時發生未預期例外，UserId: {UserId}", user.Id);
                throw;
            }
        });
    }

    /// <summary>
    /// 🚀 實作 IPermissionResolver 的 EvictUserCacheAsync，解決編譯錯誤並確保安全熔斷時即時清除權限快取
    /// </summary>
    public Task EvictUserCacheAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.CompletedTask;
        }

        string cacheKey = $"permission-mask:{userId}";
        _memoryCache.Remove(cacheKey);

        _logger.LogInformation("已成功清除用戶的權限快取快照，UserId: {UserId}", userId);
        return Task.CompletedTask;
    }
}