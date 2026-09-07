namespace SGSFramework.Core.Identities.Services;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Identities;

/// <summary>
/// 當前使用者資訊解析服務 (Scoped 生命週期，支援 HTTP Request 層級快取)
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CurrentUserService> _logger;

    // 單一 HTTP 請求層級快取變數 (避免重複走訪 Claims 集合造成效能消耗)
    private string? _cachedUserId;
    private Guid? _cachedUserGuid;
    private IReadOnlyList<string>? _cachedRoles;
    private IReadOnlyList<string>? _cachedPermissions;
    private HashSet<string>? _cachedPermissionSet;
    private bool? _cachedIsAdmin;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<CurrentUserService> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    /// <summary>
    /// 是否已通過驗證
    /// </summary>
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// 使用者字串識別碼
    /// </summary>
    public string? UserId => _cachedUserId ??= (GetClaimValue(ClaimTypes.NameIdentifier)
                                              ?? GetClaimValue(JwtRegisteredClaimNames.Sub)); 

    /// <summary>
    /// 強型別使用者 Guid 識別碼
    /// </summary>
    public Guid UserGuid
    {
        get
        {
            if (_cachedUserGuid.HasValue) return _cachedUserGuid.Value;

            _cachedUserGuid = Guid.TryParse(UserId, out var parsedGuid) ? parsedGuid : Guid.Empty; 
            return _cachedUserGuid.Value;
        }
    }

    /// <summary>
    /// 使用者名稱
    /// </summary>
    public string? UserName => GetClaimValue(ClaimTypes.Name)
                               ?? GetClaimValue("preferred_username")
                               ?? GetClaimValue(JwtRegisteredClaimNames.Name); 

    /// <summary>
    /// 電子郵件信箱
    /// </summary>
    public string? Email => GetClaimValue(ClaimTypes.Email)
                            ?? GetClaimValue(JwtRegisteredClaimNames.Email); 

    /// <summary>
    /// 租戶識別碼
    /// </summary>
    public string? TenantId => GetClaimValue("tenant_id")
                              ?? GetClaimValue("tenant"); 

    /// <summary>
    /// 是否為超級管理員 (檢查角色或 "is_admin" 特殊宣告)
    /// </summary>
    public bool IsAdmin
    {
        get
        {
            if (_cachedIsAdmin.HasValue) return _cachedIsAdmin.Value;

            if (!IsAuthenticated)
            {
                _cachedIsAdmin = false;
                return false;
            }

            try
            {
                _cachedIsAdmin = Roles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                                             || r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                              || string.Equals(GetClaimValue("is_admin"), "true", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析管理員權限識別時發生例外，UserId: {UserId}", UserId);
                _cachedIsAdmin = false;
            }

            return _cachedIsAdmin.Value;
        }
    }

    /// <summary>
    /// 角色清單 (自動排除空白並進行不區分大小寫去重)
    /// </summary>
    public IReadOnlyList<string> Roles
    {
        get
        {
            if (_cachedRoles != null) return _cachedRoles;

            try
            {
                var roleClaims = User?.FindAll(ClaimTypes.Role); 
                if (roleClaims == null) return _cachedRoles = Array.Empty<string>();

                _cachedRoles = roleClaims
                    .Select(c => c.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                    .AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析使用者角色 Claim 時發生例外，UserId: {UserId}", UserId);
                _cachedRoles = Array.Empty<string>();
            }

            return _cachedRoles;
        }
    }

    /// <summary>
    /// 權限點清單 (自動排除空白並進行不區分大小寫去重)
    /// </summary>
    public IReadOnlyList<string> Permissions
    {
        get
        {
            if (_cachedPermissions != null) return _cachedPermissions;

            try
            {
                var permClaims = User?.FindAll("permission"); 
                if (permClaims == null) return _cachedPermissions = Array.Empty<string>();

                _cachedPermissions = permClaims
                    .Select(c => c.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                    .AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析使用者權限 Claim 時發生例外，UserId: {UserId}", UserId);
                _cachedPermissions = Array.Empty<string>();
            }

            return _cachedPermissions;
        }
    }

    /// <summary>
    /// 依據指定之 ClaimType 提取 Claim Value
    /// </summary>
    public string? GetClaimValue(string claimType)
    {
        if (string.IsNullOrWhiteSpace(claimType)) return null; 

        try
        {
            return User?.FindFirst(claimType)?.Value; 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取 Claim '{ClaimType}' 時發生例外", claimType);
            return null;
        }
    }

    /// <summary>
    /// 檢查是否持有特定權限點
    /// </summary>
    public bool HasPermission(string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey)) return false;
        if (IsAdmin) return true;

        var permissionSet = GetUserPermissionsSet();
        return permissionSet.Contains(permissionKey);
    }

    /// <summary>
    /// 異步取得權限點 HashSet 集合 (專為選單樹裁切等高頻運算設計)
    /// </summary>
    public Task<HashSet<string>> GetUserPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetUserPermissionsSet());
    }

    /// <summary>
    /// 內部封裝：取得或建構 HashSet 快取
    /// </summary>
    private HashSet<string> GetUserPermissionsSet()
    {
        if (_cachedPermissionSet != null) return _cachedPermissionSet;

        try
        {
            _cachedPermissionSet = new HashSet<string>(Permissions, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立權限雜湊表時發生例外，UserId: {UserId}", UserId);
            _cachedPermissionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return _cachedPermissionSet;
    }
}