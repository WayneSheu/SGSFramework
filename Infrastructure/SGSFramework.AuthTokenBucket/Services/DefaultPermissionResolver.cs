namespace SGSFramework.AuthTokenBucket.Services;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Helpers;

/// <summary>
/// 預設使用者權限解析策略 (支援 SuperAdmin、SystemAdmin 與 IsSuperAdmin Claim)
/// </summary>
public class DefaultPermissionResolver : IPermissionResolver
{
    private readonly ILogger<DefaultPermissionResolver> _logger;

    public DefaultPermissionResolver(ILogger<DefaultPermissionResolver> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(string PermissionMask, bool IsAdmin)> ResolveUserPermissionsAsync<TUser>(
        TUser user,
        UserManager<TUser> userManager) where TUser : ApplicationUser, new()
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(userManager);

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
                // 最高管理者開啟全區段位元，確保通過 SYS.ROLE.READ, SYS.MENU.READ 等所有檢查
                permission.SetAllPermissions();
            }
            else
            {
                // 一般使用者預設權限位元設置
                permission.SetPermission(5);
            }

            return (permission.ToString(), isAdmin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析使用者權限位元時發生未預期例外，UserId: {UserId}", user.Id);
            throw;
        }
    }
}