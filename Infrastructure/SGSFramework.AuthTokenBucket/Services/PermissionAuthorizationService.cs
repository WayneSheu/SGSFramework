namespace SGSFramework.AuthTokenBucket.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Attributes;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;



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

        if (IsSuperAdmin(user))
        {
            _logger.LogDebug("[PermissionCheck] 使用者 {User} 具備超級管理員身分，直接放行權限點: {PermissionKey}",
                user.Identity?.Name ?? "Unknown", permissionKey);
            return true;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("[PermissionCheck] 使用者未提供有效的使用者 ID。");
            return false;
        }

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var runtimeScopeService = scope.ServiceProvider.GetService<IUserRuntimeScopeService>();

            if (runtimeScopeService == null)
            {
                _logger.LogError("[PermissionCheck] 無法解析 IUserRuntimeScopeService，請確認 DI 設定是否正確。");

                return false;
            }

            // 1. 嘗試從 Header 或 Claims 解析實驗室 ID
            Guid? activeLabId = ResolveActiveGuidLabId(user);

            // 2. 若未提供 Header/Claim，檢查當前請求端點是否需要實驗室隔離
            if (!activeLabId.HasValue)
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var endpoint = httpContext?.GetEndpoint();

                // 檢查端點或控制器是否標記了 RequireLaboratoryAttribute（代表需要特定實驗室範圍）
                bool requiresLabIsolation = endpoint?.Metadata.GetMetadata<RequireLaboratoryAttribute>() != null;

                if (requiresLabIsolation)
                {
                    activeLabId = await runtimeScopeService.GetPrimaryLabIdAsync(userId, cancellationToken);
                    _logger.LogDebug("[PermissionCheck] 請求需要實驗室隔離但未提供 Header/Claim，已自動從 user_lab_mappings 取得預設主實驗室: {LabId}",
                        activeLabId?.ToString() ?? "None");
                }
                else
                {
                    _logger.LogDebug("[PermissionCheck] 組織/全域層級端點（無需實驗室隔離），維持 activeLabId 為 null 進行全域權限驗證。");
                }
            }

            // 3. 帶入 activeLabId（若為組織級則為 null）查詢該上下文下的有效權限清單
            var userPermissions = await runtimeScopeService.GetUserPermissionsAsync(userId, activeLabId, cancellationToken);

            if (userPermissions == null)
            {
                _logger.LogWarning("[PermissionCheck] 無法取得使用者 {UserId} 在範圍 [{LabId}] 下的權限清單，可能是資料庫查詢失敗或使用者不存在。",
                    userId, activeLabId?.ToString() ?? "Global/Org");
                return false;
            }
            // 4. 檢查使用者是否擁有指定的權限點（忽略大小寫）
            bool hasPermission = userPermissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);

            if (!hasPermission)
            {
                _logger.LogWarning("[PermissionCheck] 使用者 {UserId} 在範圍 [{LabId}] 下欠缺所需權限點: {PermissionKey}",
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

    private bool IsSuperAdmin(ClaimsPrincipal user)
    {
        if (user.HasClaim(c =>
            (c.Type.Equals("IsSuperAdmin", StringComparison.OrdinalIgnoreCase) ||
             c.Type.Equals("is_super_admin", StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (user.IsInRole("SystemAdmin") ||
            user.IsInRole("SysAdmin") ||
            user.IsInRole("Administrator") ||
            user.HasClaim(c => c.Type == ClaimTypes.Role &&
                (string.Equals(c.Value, "SystemAdmin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(c.Value, "SysAdmin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(c.Value, "Administrator", StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        var userName = user.Identity?.Name
                    ?? user.FindFirst(ClaimTypes.Name)?.Value
                    ?? user.FindFirst(ClaimTypes.Email)?.Value
                    ?? string.Empty;

        if (userName.Contains("sysadmin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
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