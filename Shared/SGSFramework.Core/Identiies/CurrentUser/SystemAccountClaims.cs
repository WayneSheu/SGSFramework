namespace SGSFramework.Core.Abstractions.Identities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

/// <summary>
/// 帳戶類型與系統帳號（M2M）權杖宣告常數。
/// 人類使用者權杖 account_type = USER；系統帳號權杖 account_type = SYSTEM，並以多個 scope 宣告描述授權範圍。
/// </summary>
public static class SystemAccountClaims
{
    /// <summary>帳戶類型宣告</summary>
    public const string AccountType = "account_type";

    /// <summary>操作範圍宣告（每個 Scope 一筆）</summary>
    public const string Scope = "scope";

    /// <summary>JwtBearer 對應入站宣告（MapInboundClaims）後 scope 可能被轉換成的宣告類型</summary>
    public const string MappedScope = "http://schemas.microsoft.com/identity/claims/scope";

    /// <summary>
    /// 毫秒精度的簽發時間（Unix 毫秒）。標準 iat 只有秒精度，撤銷時間點與簽發在同一秒時會誤判，故另以此宣告比對
    /// </summary>
    public const string IssuedAtMilliseconds = "iat_ms";

    /// <summary>OAuth 用戶端識別碼宣告</summary>
    public const string ClientId = "client_id";

    /// <summary>系統帳號顯示名稱宣告</summary>
    public const string DisplayName = "display_name";

    public const string UserAccountType = "USER";
    public const string SystemAccountType = "SYSTEM";

    /// <summary>系統帳號識別碼前綴</summary>
    public const string IdPrefix = "sys_";

    /// <summary>僅允許系統帳號的授權策略名稱（等同規劃文件中的 InternalSystemOnly）</summary>
    public const string SystemOnlyPolicy = "InternalSystemOnly";

    /// <summary>是否為系統帳號權杖</summary>
    public static bool IsSystemAccount(this ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true &&
        string.Equals(principal.FindFirst(AccountType)?.Value, SystemAccountType, StringComparison.Ordinal);

    /// <summary>取得系統帳號權杖的所有 Scope</summary>
    public static IReadOnlySet<string> GetScopes(this ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        // 相容以空白分隔的單一 scope 宣告（OAuth 慣例）與多筆 scope 宣告兩種格式
        return principal.Claims
            .Where(c => c.Type is Scope or MappedScope)
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);
    }
}
