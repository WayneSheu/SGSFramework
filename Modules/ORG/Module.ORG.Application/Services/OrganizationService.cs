using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGSFramework.Persistent.Repositories.Hierarchy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGS.Modules.ORG.Application.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IHierarchicalRepository<ORGDbContext, Organization> _orgRepo;

        public OrganizationService(IHierarchicalRepository<ORGDbContext, Organization> orgRepo)
        {
            _orgRepo = orgRepo ?? throw new ArgumentNullException(nameof(orgRepo));
        }

        /// <summary>
        /// 新增組織節點
        /// </summary>
        public async Task<Organization> CreateOrganizationAsync(Organization org, CancellationToken cancellationToken = default)
        {
            if (org is null)
            {
                throw new ArgumentNullException(nameof(org));
            }

            if (string.IsNullOrWhiteSpace(org.Name))
            {
                throw new ArgumentException("Organization name cannot be empty.", nameof(org));
            }

            try
            {
                return await _orgRepo.AddNodeAsync(org, null);
            }
            catch (Exception)
            {
                // 不要靜默吞掉異常，保持 Exception 向上拋出或記錄 Log 後重新拋出
                throw;
            }
        }

        /// <summary>
        /// 取得單一組織節點（包含階層資訊）
        /// </summary>
        public async Task<Organization?> GetOrganizationByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _orgRepo.GetByIdAsync(id);
        }

        /// <summary>
        /// 取得組織階層根節點
        /// </summary>
        public async Task<List<Organization>> GetOrganizationTreeAsync(CancellationToken cancellationToken = default)
        {
            return await _orgRepo.GetRoots().ToListAsync(cancellationToken);
        }

        /// <summary>
        /// 獲取所有未軟刪除的組織節點 (方案 A 所需的全表查詢介面)
        /// </summary>
        public async Task<List<Organization>> GetAllActiveOrganizationsAsync(CancellationToken cancellationToken = default)
        {
            // 透過 IHierarchicalRepository 取得 Queryable 並以 AsNoTracking 查詢
            return await _orgRepo.GetRoots()
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// 完整更新組織節點（包含階層資訊）
        /// </summary>
        public async Task<Organization> EditOrganizationAsync(Organization org, CancellationToken cancellationToken = default)
        {
            if (org is null)
            {
                throw new ArgumentNullException(nameof(org));
            }

            return await _orgRepo.EditNodeAsync(org);
        }

        /// <summary>
        /// 部分更新組織節點
        /// </summary>
        public async Task<Organization> PatchOrganizationTreeAsync(Organization org, CancellationToken cancellationToken = default)
        {
            if (org is null)
            {
                throw new ArgumentNullException(nameof(org));
            }

            return await _orgRepo.PatchNodeAsync(org);
        }

        /// <summary>
        /// 移動部門子樹
        /// </summary>
        public async Task MoveOrganizationAsync(int nodeId, int newParentId, CancellationToken cancellationToken = default)
        {
            await _orgRepo.MoveSubtreeAsync(nodeId, newParentId);
        }

        /// <summary>
        /// 刪除組織節點（包含子節點）
        /// </summary>
        public async Task DeleteOrganizationAsync(int nodeId, CancellationToken cancellationToken = default)
        {
            await _orgRepo.DeleteNodeWithChildrenAsync(nodeId);
        }
    }
}