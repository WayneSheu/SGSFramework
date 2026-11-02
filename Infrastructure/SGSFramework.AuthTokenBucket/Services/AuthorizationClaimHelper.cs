namespace SGSFramework.AuthTokenBucket.Services;

using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

/// <summary>
/// 授權判斷共用的身分宣告與實驗室上下文解析，確保各授權元件（權限點、規則引擎、報告授權）判定一致。
/// </summary>
public static class AuthorizationClaimHelper
{
    private static readonly string[] AdminRoleNames = ["SuperAdmin", "SystemAdmin"];

    private static readonly string[] LabHeaderNames = ["X-Lab-Id", "TenantLabId", "X-Target-Lab-Id", "LabId", "X-Target-LabId"];

    private static readonly string[] LabClaimTypes = ["TenantLabId", "lab_id", "TargetLabId"];

    /// <summary>
    /// 僅信任伺服器簽發 Token 時由 DefaultPermissionResolver 判定的管理員宣告，以及相同定義的系統管理角色。
    /// 不得以使用者名稱或任意 Claim 值推斷（device_id 等 Claim 值來自用戶端輸入，會造成提權）。
    /// </summary>
    public static bool IsSuperAdmin(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        // 1. 特權 Claim 檢查 (明確指定 true)
        if (user.HasClaim(c =>
            (c.Type.Equals("IsSuperAdmin", StringComparison.OrdinalIgnoreCase) ||
             c.Type.Equals("is_admin", StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // 2. 角色比對 (與 DefaultPermissionResolver 的管理角色定義一致)
        return user.Claims.Any(c =>
            (c.Type.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) ||
             c.Type.Equals("roles", StringComparison.OrdinalIgnoreCase) ||
             c.Type.Equals("role", StringComparison.OrdinalIgnoreCase)) &&
            AdminRoleNames.Contains(c.Value, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 解析目前請求的區域實驗室：優先採用請求標頭，其次為 Token 宣告。
    /// </summary>
    public static Guid? ResolveTenantLabId(ClaimsPrincipal user, HttpContext? httpContext)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (httpContext != null)
        {
            foreach (var headerName in LabHeaderNames)
            {
                if (Guid.TryParse(httpContext.Request.Headers[headerName].FirstOrDefault(), out Guid headerLabId))
                {
                    return headerLabId;
                }
            }
        }

        foreach (var claimType in LabClaimTypes)
        {
            if (Guid.TryParse(user.FindFirst(claimType)?.Value, out Guid claimLabId))
            {
                return claimLabId;
            }
        }

        return null;
    }

    /// <summary>
    /// 解析使用者識別碼（NameIdentifier，其次 sub）
    /// </summary>
    public static Guid? ResolveUserId(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var raw = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
