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

/// <summary>
/// 企業級全域與動態權限授權服務
/// 整合 Fast-Path Bitmask Claim 校驗、實驗室多租戶隔離與 SuperAdmin 放行機制
/// </summary>
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

        // 1. 最高管理者 (SuperAdmin / SystemAdmin) 特權檢查 (Fast Path 1)
        if (IsSuperAdmin(user))
        {
            _logger.LogDebug("[PermissionCheck] 使用者 {User} 具備最高管理者特權，直接放行權限點: {PermissionKey}",
                user.Identity?.Name ?? "Unknown", permissionKey);
            return true;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("[PermissionCheck] 權限校驗失敗：使用者 Claim 中欠缺 NameIdentifier (User ID)。");
            return false;
        }

        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var endpoint = httpContext?.GetEndpoint();

            // 2. 檢查當前端點是否標記 RequireLaboratoryAttribute (實驗室隔離需求)
            bool requiresLabIsolation = endpoint?.Metadata.GetMetadata<RequireLaboratoryAttribute>() != null;

            // 3. 解析當前 Active Lab ID (優先取自 Request Header，次之取自 Claim)
            Guid? activeLabId = ResolveActiveGuidLabId(user, httpContext);

            // 4. 若需要實驗室隔離且未提供 Header/Claim，自動載入預設主實驗室 ID
            if (requiresLabIsolation && !activeLabId.HasValue)
            {
                activeLabId = await _runtimeScopeService.GetPrimaryLabIdAsync(userId, cancellationToken);
                _logger.LogDebug("[PermissionCheck] 端點要求實驗室隔離，已自動載入預設主實驗室 ID: {LabId}",
                    activeLabId?.ToString() ?? "None");
            }

            // 5. 非實驗室隔離請求時，優先嘗試 Fast-Path JWT Claims Bitmask 比對 (避免耗時查詢)
            if (!requiresLabIsolation && _permissionRegistry != null)
            {
                if (TryCheckBitmaskFromClaims(user, permissionKey, out bool isAuthorizedByClaim))
                {
                    if (isAuthorizedByClaim)
                    {
                        return true;
                    }
                }
            }

            // 6. 查詢 Runtime Scope 下使用者於該 Lab/Global 範圍之有效權限點清單
            var userPermissions = await _runtimeScopeService.GetUserPermissionsAsync(userId, activeLabId, cancellationToken);

            if (userPermissions == null)
            {
                _logger.LogWarning("[PermissionCheck] 無法取得使用者 {UserId} 於範圍 [{LabId}] 下的權限集。",
                    userId, activeLabId?.ToString() ?? "Global/Org");
                return false;
            }

            // 7. 檢查使用者是否擁有指定的權限點 (忽略大小寫)
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
            _logger.LogError(ex, "[PermissionCheck] 動態解析使用者 {UserId} 權限 {PermissionKey} 時發生未預期異常", userId, permissionKey);
            return false;
        }
    }

    /// <summary>
    /// 從 JWT Claims 中快速驗證 Bitmask 權限點
    /// </summary>
    private bool TryCheckBitmaskFromClaims(ClaimsPrincipal user, string permissionKey, out bool isAuthorized)
    {
        isAuthorized = false;
        if (_permissionRegistry == null)
        {
            return false;
        }

        try
        {
            int requiredBit = _permissionRegistry.GetOrCreateBitPosition(permissionKey);

            // A. 多位元清單 (perm_bits: 例 "0,1,5,12")
            var permBitsClaim = user.FindFirst("perm_bits")?.Value;
            if (!string.IsNullOrEmpty(permBitsClaim))
            {
                var grantedBits = permBitsClaim
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(int.Parse)
                    .ToHashSet();

                isAuthorized = grantedBits.Contains(requiredBit);
                return true;
            }

            // B. 64 位元遮罩 (perm_mask: 例 "32")
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
    /// 完整對齊系統最高管理者身份驗證特徵
    /// </summary>
    private static bool IsSuperAdmin(ClaimsPrincipal user)
    {
        // 1. Claim 檢查 (IsSuperAdmin, is_super_admin, is_admin)
        if (user.HasClaim(c =>
            (c.Type.Equals("IsSuperAdmin", StringComparison.OrdinalIgnoreCase) ||
             c.Type.Equals("is_super_admin", StringComparison.OrdinalIgnoreCase) ||
             c.Type.Equals("is_admin", StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // 2. Role 檢查 (SuperAdmin, SystemAdmin, SysAdmin, Administrator)
        if (user.IsInRole("SuperAdmin") ||
            user.IsInRole("SystemAdmin") ||
            user.IsInRole("SysAdmin") ||
            user.IsInRole("Administrator") ||
            user.HasClaim(c => c.Type == ClaimTypes.Role &&
                (string.Equals(c.Value, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(c.Value, "SystemAdmin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(c.Value, "SysAdmin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(c.Value, "Administrator", StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        // 3. 內建管理者帳號名稱檢查 (sysadmin, admin)
        var userName = user.Identity?.Name
                    ?? user.FindFirst(ClaimTypes.Name)?.Value
                    ?? user.FindFirst(ClaimTypes.Email)?.Value
                    ?? string.Empty;

        return userName.Contains("sysadmin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(userName, "admin", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 解析當前請求環境下之實驗室識別碼 Guid
    /// </summary>
    private static Guid? ResolveActiveGuidLabId(ClaimsPrincipal user, HttpContext? httpContext)
    {
        if (httpContext != null)
        {
            var labHeader = httpContext.Request.Headers["TenantLabId"].FirstOrDefault()
                         ?? httpContext.Request.Headers["X-Target-Lab-Id"].FirstOrDefault()
                         ?? httpContext.Request.Headers["X-Lab-Id"].FirstOrDefault()
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