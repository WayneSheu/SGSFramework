namespace SGSFramework.AuthTokenBucket.Services;

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;

public sealed class PermissionAuthorizationService : IPermissionAuthorizationService
{
    private readonly ILogger<PermissionAuthorizationService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PermissionAuthorizationService(
        ILogger<PermissionAuthorizationService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        if (user.Identity is not { IsAuthenticated: true })
        {
            return false;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var runtimeScopeService = scope.ServiceProvider.GetService<IUserRuntimeScopeService>();

            if (runtimeScopeService == null)
            {
                return false;
            }

            // 1. 嘗試從 Header 或 Claims 解析實驗室 ID
            Guid? activeLabId = ResolveActiveGuidLabId(user);

            // 2. 若未帶 Header 且 Token 沒記錄，自動強制抓取使用者的第一個有效實驗室（優先 IsPrimary）
            if (!activeLabId.HasValue || activeLabId == Guid.Empty)
            {
                var accessibleLabs = await runtimeScopeService.GetAccessibleLabsAsync(userId, cancellationToken);
                var primaryLab = accessibleLabs.FirstOrDefault(l => l.IsPrimary) ?? accessibleLabs.FirstOrDefault();
                if (primaryLab != null && primaryLab.TenantLabId != Guid.Empty)
                {
                    activeLabId = primaryLab.TenantLabId;
                    _logger.LogDebug("[PermissionCheck] 未偵測到 Lab Header，已自動鎖定使用者 {UserId} 的預設實驗室: {LabId}", userId, activeLabId);
                }
            }

            // 3. 帶入鎖定後的 activeLabId 查詢該實驗室下的有效權限清單
            var userPermissions = await runtimeScopeService.GetUserPermissionsAsync(userId, activeLabId, cancellationToken);

            if (userPermissions == null)
            {
                return false;
            }

            bool hasPermission = userPermissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);

            if (!hasPermission)
            {
                _logger.LogWarning("[PermissionCheck] 使用者 {UserId} 在實驗室 [{LabId}] 下欠缺所需權限點: {PermissionKey}",
                    userId, activeLabId?.ToString() ?? "None", permissionKey);
            }

            return hasPermission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionCheck] 動態解析使用者 {UserId} 權限 {PermissionKey} 時發生異常", userId, permissionKey);
            return false;
        }
    }

    private Guid? ResolveActiveGuidLabId(ClaimsPrincipal user)
    {
        var httpContext = _httpContextAccessor.HttpContext;
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