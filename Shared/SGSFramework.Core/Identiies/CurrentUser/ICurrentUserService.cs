namespace SGSFramework.Core.Abstractions.Identities;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 當前使用者上下文解析服務介面[cite: 8]
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// 使用者字串識別碼 (優先解析 NameIdentifier，降級讀取 JWT sub)[cite: 8]
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// 強型別使用者 Guid 識別碼[cite: 8]
    /// </summary>
    Guid UserGuid { get; }

    /// <summary>
    /// 使用者顯示名稱[cite: 8]
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// 電子郵件信箱[cite: 8]
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// 租戶識別碼[cite: 8]
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// 是否已通過身份驗證[cite: 8]
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// 是否具備系統管理員權限
    /// </summary>
    bool IsAdmin { get; }

    /// <summary>
    /// 角色名稱清單[cite: 8]
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// 權限點代碼清單[cite: 8]
    /// </summary>
    IReadOnlyList<string> Permissions { get; }

    /// <summary>
    /// 提取指定類型之 Claim 值[cite: 8]
    /// </summary>
    string? GetClaimValue(string claimType);

    /// <summary>
    /// 檢查是否持有特定權限點 (支援大小寫不敏感比對)
    /// </summary>
    bool HasPermission(string permissionKey);

    /// <summary>
    /// 異步取得權限點 HashSet 集合 (提供 $O(1)$ 高效運算)
    /// </summary>
    Task<HashSet<string>> GetUserPermissionsAsync(CancellationToken cancellationToken = default);
}