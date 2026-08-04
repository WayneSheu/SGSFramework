using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Adapters
{
    public record OrganizationInfoContract(Guid Id, string Name, string NodePathString, int HierarchyLevel);

    /// <summary>
    /// 跨模組組織資料整合介面 (由 Organization 外掛模組實作)
    /// </summary>
    public interface IOrganizationIntegrationService
    {
        /// <summary>
        /// 獲取單一組織節點資訊
        /// </summary>
        Task<OrganizationInfoContract?> GetOrganizationByIdAsync(Guid orgId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 驗證目標組織是否在使用者管轄的組織階層範疇內 (HierarchyId 樹狀比對)
        /// </summary>
        Task<bool> IsInUserScopeAsync(string userId, Guid targetOrgId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 獲取使用者有權存取的所有組織節點清單
        /// </summary>
        Task<List<OrganizationInfoContract>> GetUserAccessibleOrganizationsAsync(string userId, CancellationToken cancellationToken = default);
   
    }
}
