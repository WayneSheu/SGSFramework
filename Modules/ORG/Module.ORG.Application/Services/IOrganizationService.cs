using SGS.Modules.ORG.Infrastructure.Entities.Org;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGS.Modules.ORG.Application.Services
{
    public interface IOrganizationService
    {
        /// <summary>
        /// 新增組織節點
        /// </summary>
        Task<Organization> CreateOrganizationAsync(Organization org, CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得單一組織節點
        /// </summary>
        Task<Organization?> GetOrganizationByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得所有根節點樹狀結構
        /// </summary>
        Task<List<Organization>> GetOrganizationTreeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 獲取所有未軟刪除的組織節點 (提供跨模組適配器高效能查詢)
        /// </summary>
        Task<List<Organization>> GetAllActiveOrganizationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 移動組織子樹
        /// </summary>
        Task MoveOrganizationAsync(int nodeId, int newParentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 完整更新組織節點
        /// </summary>
        Task<Organization> EditOrganizationAsync(Organization org, CancellationToken cancellationToken = default);

        /// <summary>
        /// 部分更新組織節點
        /// </summary>
        Task<Organization> PatchOrganizationTreeAsync(Organization org, CancellationToken cancellationToken = default);

        /// <summary>
        /// 刪除組織節點（包含子節點）
        /// </summary>
        Task DeleteOrganizationAsync(int nodeId, CancellationToken cancellationToken = default);
    
    }
}