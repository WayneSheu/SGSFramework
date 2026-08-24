namespace SGSFramework.Core.Identiies.CurrentUser
{

    /// <summary>
    /// 當前登入使用者上下文服務介面
    /// </summary>
    public interface ICurrentUserService
    {
        /// <summary>
        /// 是否已通過身份驗證
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// 當前使用者 ID (字串)
        /// </summary>
        string? UserId { get; }

        /// <summary>
        /// 當前使用者 ID (Guid 型別，無效時傳回 Guid.Empty)
        /// </summary>
        Guid UserGuid { get; }

        /// <summary>
        /// 當前使用者帳號/名稱
        /// </summary>
        string? UserName { get; }

        /// <summary>
        /// 當前使用者電子郵件
        /// </summary>
        string? Email { get; }

        /// <summary>
        /// 當前租戶 ID
        /// </summary>
        string? TenantId { get; }

        /// <summary>
        /// 當前使用者所擁有的角色清單
        /// </summary>
        IReadOnlyList<string> Roles { get; }

        /// <summary>
        /// 當前使用者所擁有的權限點清單 (Permissions)
        /// </summary>
        IReadOnlyList<string> Permissions { get; }

        /// <summary>
        /// 依據 Claim Type 取得對應數值
        /// </summary>
        string? GetClaimValue(string claimType);


    }
}
