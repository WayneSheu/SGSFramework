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
        /// 新增組織節點 (含 TenantLabId 繼承邏輯與 UUIDv7 生成)
        /// </summary>
        public async Task<Organization> CreateOrganizationAsync(Organization org, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(org);

            if (string.IsNullOrWhiteSpace(org.Name))
            {
                throw new ArgumentException("組織名稱不可為空。", nameof(org));
            }

            // 🔑 處理 TenantLabId 多租戶隔離邏輯
            if (org.ParentId.HasValue && org.ParentId.Value > 0)
            {
                var parentNode = await _orgRepo.GetByIdAsync(org.ParentId.Value);
                if (parentNode != null)
                {
                    // 若父節點已有 TenantLabId，則直接繼承；否則產生全新的 UUIDv7
                    org.TenantLabId = parentNode.TenantLabId ?? org.TenantLabId ?? Guid.CreateVersion7();
                }
                else
                {
                    throw new KeyNotFoundException($"找不到指定的父級組織 (ParentId: {org.ParentId})");
                }
            }
            else
            {
                // 頂層區域節點，若無指定則自動給予 UUIDv7
                org.TenantLabId ??= Guid.CreateVersion7();
            }

            // 🔑 正確做法：傳入 org.ParentId (int?) 以及 cancellationToken
            return await _orgRepo.AddNodeAsync(org, org.ParentId, cancellationToken);
        }

        /// <summary>
        /// 取得單一組織節點
        /// </summary>
        public async Task<Organization?> GetOrganizationByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _orgRepo.GetByIdAsync(id);
            if (entity != null && entity.IsDeleted) return null;
            return entity;
        }

        /// <summary>
        /// 取得所有根節點 (滿足 IOrganizationService 介面合約)
        /// </summary>
        public async Task<List<Organization>> GetOrganizationTreeAsync(CancellationToken cancellationToken = default)
        {
            return await _orgRepo.GetRoots()
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// 依據 NodePath 前綴取得指定組織及其完整子樹
        /// </summary>
        public async Task<List<Organization>> GetSubtreeByNodePathAsync(string nodePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(nodePath)) return new List<Organization>();

            //使用 GetDescendants(nodePath) 或是全表查詢，移除 GetRoots() 的 ParentId == null 限制
            return await _orgRepo.GetDescendants("") // 抓全量節點進行 NodePath 開頭比對
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.NodePath.StartsWith(nodePath))
                .OrderBy(x => x.Level)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// 獲取所有未軟刪除的組織節點
        /// </summary>
        public async Task<List<Organization>> GetAllActiveOrganizationsAsync(CancellationToken cancellationToken = default)
        {
            return await _orgRepo.GetDescendants("") // 或透過 DBContext 查詢全量
            .AsNoTracking() // 如果 GetDescendants("") 抓 NodePath 以 "" 開頭即可涵蓋全部路徑
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Level)
            .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// 完整更新組織節點
        /// </summary>
        public async Task<Organization> EditOrganizationAsync(Organization org, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(org);
            return await _orgRepo.EditNodeAsync(org, cancellationToken);
        }

        /// <summary>
        /// 部分更新組織節點
        /// </summary>
        public async Task<Organization> PatchOrganizationTreeAsync(Organization org, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(org);
            return await _orgRepo.PatchNodeAsync(org, cancellationToken);
        }

        /// <summary>
        /// 移動部門子樹
        /// </summary>
        public async Task MoveOrganizationAsync(int nodeId, int newParentId, CancellationToken cancellationToken = default)
        {
            await _orgRepo.MoveSubtreeAsync(nodeId, newParentId, cancellationToken);
        }

        /// <summary>
        /// 刪除組織節點（包含所有子節點）
        /// </summary>
        public async Task DeleteOrganizationAsync(int nodeId, CancellationToken cancellationToken = default)
        {
            await _orgRepo.DeleteNodeWithChildrenAsync(nodeId, cancellationToken);
        }
    }
}