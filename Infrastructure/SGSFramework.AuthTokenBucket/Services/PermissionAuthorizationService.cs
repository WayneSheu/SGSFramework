// ==========================================
// 檔案路徑: src/SGSFramework/Infrastructure/SGSFramework.AuthTokenBucket/Services/PermissionAuthorizationService.cs
// 架構層級: Infrastructure Layer / Services
// ==========================================

namespace SGSFramework.AuthTokenBucket.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Models.Identities;

/// <summary>
/// 企業級權限授權驗證服務實作（採用策略模式與高效能位元/字串雙軌解析優化版）
/// </summary>
public sealed class PermissionAuthorizationService : IPermissionAuthorizationService
{
    private readonly ILogger<PermissionAuthorizationService> _logger;
    private readonly IEnumerable<IPermissionEvaluator> _permissionEvaluators;

    private static readonly string[] SuperAdminRoles = ["SystemAdmin", "sysadmin", "Administrator", "Admin"];
    private static readonly string[] SuperAdminClaimTypes = ["is_admin", "is_sysadmin", "superuser", "role"];

    public PermissionAuthorizationService(
        ILogger<PermissionAuthorizationService> logger,
        IEnumerable<IPermissionEvaluator> permissionEvaluators)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _permissionEvaluators = permissionEvaluators ?? Enumerable.Empty<IPermissionEvaluator>();
    }

    public Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        try
        {
            if (user.Identity is not { IsAuthenticated: true })
            {
                return Task.FromResult(false);
            }

            // 特權通道檢查：全域放行超級管理員 / 系統管理員
            if (IsSuperAdmin(user))
            {
                return Task.FromResult(true);
            }

            bool hasPermission = false;
            foreach (var evaluator in _permissionEvaluators)
            {
                if (evaluator.Evaluate(user, permissionKey))
                {
                    hasPermission = true;
                    break;
                }
            }

            if (!hasPermission)
            {
                hasPermission = EvaluateDefaultPermissions(user, permissionKey);
            }

            if (!hasPermission)
            {
                _logger.LogWarning("[PermissionCheck] 使用者 {UserId} 欠缺所需權限點: {PermissionKey}",
                    user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                    permissionKey);
            }

            return Task.FromResult(hasPermission);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionCheck] 執行權限點 {PermissionKey} 比對時發生內部異常", permissionKey);
            return Task.FromResult(false);
        }
    }

    private static bool IsSuperAdmin(ClaimsPrincipal user)
    {
        foreach (var role in SuperAdminRoles)
        {
            if (user.IsInRole(role))
            {
                return true;
            }
        }

        foreach (var claimType in SuperAdminClaimTypes)
        {
            if (user.HasClaim(c => c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase) &&
                                   (c.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                    c.Value.Equals("1") ||
                                    SuperAdminRoles.Contains(c.Value, StringComparer.OrdinalIgnoreCase))))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EvaluateDefaultPermissions(ClaimsPrincipal user, string permissionKey)
    {
        foreach (var claim in user.Claims)
        {
            // 檢查顯式字串宣告
            if ((claim.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase) ||
                 claim.Type.Equals("permission_key", StringComparison.OrdinalIgnoreCase)) &&
                claim.Value.Equals(permissionKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 檢查 Bitmask 封裝字串宣告 (permissions)
            if (claim.Type.Equals("permissions", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // 修正 1：將 claim.Value 字串正確解析為 IEnumerable<long> 傳入 BigBitmaskPermission
                    var permissionIds = claim.Value
                        .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => long.TryParse(s.Trim(), out var id) ? id : (long?)null)
                        .Where(id => id.HasValue)
                        .Select(id => id.Value)
                        .ToList();

                    var bitmask = new BigBitmaskPermission(permissionIds);

                    // 修正 2：若 permissionKey 為數字 ID，可直接比對清單；若為字串則進行字串相符檢查
                    if (long.TryParse(permissionKey, out var numericId))
                    {
                        if (permissionIds.Contains(numericId))
                        {
                            return true;
                        }
                    }
                    else if (claim.Value.Equals(permissionKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                    if (claim.Value.Equals(permissionKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}

public interface IPermissionEvaluator
{
    bool Evaluate(ClaimsPrincipal user, string permissionKey);
}