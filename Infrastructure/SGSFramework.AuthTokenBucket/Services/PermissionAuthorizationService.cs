namespace SGSFramework.AuthTokenBucket.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Attributes;
using SGSFramework.Core.Abstractions.Permissions;

public sealed class PermissionAuthorizationService : IPermissionAuthorizationService
{
    private readonly ILogger<PermissionAuthorizationService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserRuntimeScopeService _runtimeScopeService;
    private readonly IPermissionRegistry? _permissionRegistry;

    public PermissionAuthorizationService(
        ILogger<PermissionAuthorizationService> logger,
        IHttpContextAccessor httpContextAccessor,
        IUserRuntimeScopeService runtimeScopeService,
        IPermissionRegistry? permissionRegistry = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _runtimeScopeService = runtimeScopeService ?? throw new ArgumentNullException(nameof(runtimeScopeService));
        _permissionRegistry = permissionRegistry;
    }

    public async Task<bool> HasPermissionAsync(
        ClaimsPrincipal user,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        if (user.Identity is not { IsAuthenticated: true })
        {
            return false;
        }

        // 1. 最高管理者特權直通
        if (IsSuperAdmin(user))
        {
            _logger.LogDebug("[PermissionCheck] 使用者 {User} 具備最高管理者特權，直接放行權限點: {PermissionKey}",
                user.Identity?.Name ?? "Unknown", permissionKey);
            return true;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("[PermissionCheck] 權限校驗失敗：使用者 Claim 中欠缺 NameIdentifier。");
            return false;
        }

        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var endpoint = httpContext?.GetEndpoint();

            bool requiresLabIsolation = endpoint?.Metadata.GetMetadata<RequireLaboratoryAttribute>() != null;
            Guid? activeLabId = ResolveActiveGuidLabId(user, httpContext);

            if (requiresLabIsolation && !activeLabId.HasValue)
            {
                activeLabId = await _runtimeScopeService.GetPrimaryLabIdAsync(userId, cancellationToken);
            }

            // 2. 非隔離請求時優先嘗試 Fast-Path Bitmask Claim 比對
            if (!requiresLabIsolation && _permissionRegistry != null)
            {
                if (TryCheckBitmaskFromClaims(user, permissionKey, out bool isAuthorizedByClaim) && isAuthorizedByClaim)
                {
                    return true;
                }
            }

            // 3. 查核 Runtime Scope 權限清單
            var userPermissions = await _runtimeScopeService.GetUserPermissionsAsync(userId, activeLabId, cancellationToken);
            if (userPermissions == null)
            {
                _logger.LogWarning("[PermissionCheck] 無法取得使用者 {UserId} 於範圍 [{LabId}] 下的權限集。",
                    userId, activeLabId?.ToString() ?? "Global/Org");
                return false;
            }

            bool hasPermission = userPermissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
            if (!hasPermission)
            {
                _logger.LogWarning("[PermissionCheck] 使用者 {UserId} 於範圍 [{LabId}] 下欠缺所需權限點: {PermissionKey}",
                    userId, activeLabId?.ToString() ?? "Global/Org", permissionKey);
            }

            return hasPermission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionCheck] 動態解析使用者 {UserId} 權限 {PermissionKey} 時發生異常", userId, permissionKey);
            return false;
        }
    }

    /// <summary>
    /// 從 JWT Claims 快速比對 Bitmask，改用 long.TryParse 防禦 Int32 溢位 Exception
    /// </summary>
    private bool TryCheckBitmaskFromClaims(ClaimsPrincipal user, string permissionKey, out bool isAuthorized)
    {
        isAuthorized = false;
        if (_permissionRegistry == null) return false;

        try
        {
            int requiredBit = _permissionRegistry.GetOrCreateBitPosition(permissionKey);

            // A. 位元清單解析 (perm_bits: 例 "0,1,5,12" 或 64 位元位址)
            var permBitsClaim = user.FindFirst("perm_bits")?.Value;
            if (!string.IsNullOrEmpty(permBitsClaim))
            {
                var grantedBits = new HashSet<long>();
                var rawSegments = permBitsClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var segment in rawSegments)
                {
                    if (long.TryParse(segment, out long bitVal))
                    {
                        grantedBits.Add(bitVal);
                    }
                }

                if (grantedBits.Count > 0)
                {
                    isAuthorized = grantedBits.Contains(requiredBit);
                    return true;
                }
            }

            // B. 64 位元遮罩 (perm_mask)
            var permMaskClaim = user.FindFirst("perm_mask")?.Value;
            if (long.TryParse(permMaskClaim, out long mask) && requiredBit < 64)
            {
                long targetMask = 1L << requiredBit;
                isAuthorized = (mask & targetMask) == targetMask;
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PermissionCheck] 從 Claim 解析 Bitmask 時發生例外，降級走 Runtime Scope 查詢。");
        }

        return false;
    }

    /// <summary>
    /// 完整相容 JWT 原生 "roles" / "role" 陣列與標準 ClaimTypes.Role
    /// </summary>
    private static bool IsSuperAdmin(ClaimsPrincipal user)
    {
        // 1. 特權 Claim 檢查
        if (user.HasClaim(c =>
            (c.Type.Equals("IsSuperAdmin", StringComparison.OrdinalIgnoreCase) ||
             c.Type.Equals("is_super_admin", StringComparison.OrdinalIgnoreCase) ||
             c.Type.Equals("is_admin", StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // 2. 角色比對 (相容 ClaimTypes.Role、"roles"、"role")
        var adminRoleNames = new[] { "SuperAdmin", "SystemAdmin", "SysAdmin", "Administrator" };
        if (user.Claims.Any(c =>
            (c.Type.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) ||
             c.Type.Equals("roles", StringComparison.OrdinalIgnoreCase) ||
             c.Type.Equals("role", StringComparison.OrdinalIgnoreCase)) &&
            adminRoleNames.Contains(c.Value, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        // 3. 系統帳號名稱檢查
        var userName = user.Identity?.Name
                    ?? user.FindFirst(ClaimTypes.Name)?.Value
                    ?? user.FindFirst(ClaimTypes.Email)?.Value
                    ?? string.Empty;

        return userName.Contains("sysadmin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(userName, "admin", StringComparison.OrdinalIgnoreCase);
    }

    private static Guid? ResolveActiveGuidLabId(ClaimsPrincipal user, HttpContext? httpContext)
    {
        if (httpContext != null)
        {
            var labHeader = httpContext.Request.Headers["TenantLabId"].FirstOrDefault()
                         ?? httpContext.Request.Headers["X-Target-Lab-Id"].FirstOrDefault()
                         ?? httpContext.Request.Headers["LabId"].FirstOrDefault();

            if (Guid.TryParse(labHeader, out Guid headerLabId))
            {
                return headerLabId;
            }
        }

        var labClaim = user.FindFirst("TenantLabId")?.Value
                    ?? user.FindFirst("lab_id")?.Value
                    ?? user.FindFirst("TargetLabId")?.Value;

        if (Guid.TryParse(labClaim, out Guid claimLabId))
        {
            return claimLabId;
        }

        return null;
    }
}