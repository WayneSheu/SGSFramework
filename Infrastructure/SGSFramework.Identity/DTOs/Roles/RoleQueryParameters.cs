using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Paginations;

namespace SGSFramework.Identity.DTOs.Roles
{
    /// <summary>
    /// 角色分頁查詢條件模型（POST api/v1/roles/search 的 Request Body）
    /// </summary>
    public sealed record RoleQueryParameters : PagedQueryParameters<RoleFilterCriteria>
    {
    }

    /// <summary>
    /// 角色專用篩選條件模型
    /// </summary>
    public sealed record RoleFilterCriteria
    {
        /// <summary>
        /// 角色範圍：Global（全域）/ LabScoped（實驗室專屬）；null 表示不限
        /// </summary>
        public RoleScopeType? ScopeType { get; init; }

        /// <summary>
        /// 角色代碼（模糊比對，不分大小寫）
        /// </summary>
        public string? Code { get; init; }

        /// <summary>
        /// 角色名稱（模糊比對）
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// 角色狀態（對應 RoleStatus 數值）；null 表示不限
        /// </summary>
        public RoleStatus? Status { get; init; }
    }
}
