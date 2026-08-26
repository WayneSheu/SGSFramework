using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Services
{
    /// <summary>
    /// 企業級權限授權驗證服務實作，支援 SuperAdmin 特權通道與動態 BitMask / Claim 權限點檢核。
    /// </summary>
    public sealed class PermissionAuthorizationService : IPermissionAuthorizationService
    {
        private readonly ILogger<PermissionAuthorizationService> _logger;

        public PermissionAuthorizationService(ILogger<PermissionAuthorizationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionKey, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

            try
            {
                // 1. 特權檢查：若為系統管理員角色或具備 is_admin 宣告，全域放行
                if (user.IsInRole("SystemAdmin") || user.HasClaim(c => c.Type == "is_admin" && c.Value.Equals("true", StringComparison.OrdinalIgnoreCase)))
                {
                    return Task.FromResult(true);
                }

                // 2. 比對 User Claims 中的 BitMask / Permission 權限點
                bool hasPermission = user.Claims.Any(c =>
                    (c.Type == "Permission" || c.Type == "permission_key") &&
                    c.Value.Equals(permissionKey, StringComparison.OrdinalIgnoreCase));

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
    }
}
