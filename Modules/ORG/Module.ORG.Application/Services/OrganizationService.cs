using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGSFramework.Persistent.Repositories.Hierarchy;

namespace SGS.Modules.ORG.Application.Services;

/// <summary>
/// 組織/實驗室層級服務實作 (Clean Architecture - Application Layer)
/// </summary>
public class OrganizationService : IOrganizationService
{
    private readonly IHierarchicalRepository<ORGDbContext, Organization> _orgRepo;

    public OrganizationService(IHierarchicalRepository<ORGDbContext, Organization> orgRepo)
    {
        _orgRepo = orgRepo ?? throw new ArgumentNullException(nameof(orgRepo));
    }

    /// <summary>
    /// 新增組織節點 (已重構：完全委由 Command / Entity 決定 TenantLabId，徹底移除多餘與反射 SetTenantLabId)
    /// </summary>
    public async Task<Organization> CreateOrganizationAsync(Organization org, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(org);

        try
        {
            if (string.IsNullOrWhiteSpace(org.Name))
            {
                throw new ArgumentException("組織名稱不可為空。", nameof(org.Name));
            }

            // 驗證父節點是否存在
            if (org.ParentId.HasValue && org.ParentId.Value > 0)
            {
                var parentNode = await _orgRepo.GetByIdAsync(org.ParentId.Value, cancellationToken).ConfigureAwait(false);
                if (parentNode is null)
                {
                    throw new KeyNotFoundException($"找不到指定的父級組織 (ParentId: {org.ParentId.Value})");
                }
            }

            // 🔑 專用階層 Repository (AddNodeAsync) 會自動持久化實體、計算與寫入物化路徑 (NodePath)
            return await _orgRepo.AddNodeAsync(org, org.ParentId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// 取得單一組織節點
    /// </summary>
    public async Task<Organization?> GetOrganizationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _orgRepo.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (entity is not null && entity.IsDeleted) return null;
            return entity;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// 取得所有根節點
    /// </summary>
    public async Task<List<Organization>> GetOrganizationTreeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _orgRepo.GetRoots()
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// 依據 NodePath 前綴取得指定組織及其完整子樹
    /// </summary>
    public async Task<List<Organization>> GetSubtreeByNodePathAsync(string nodePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(nodePath)) return new List<Organization>();

            return await _orgRepo.GetDescendants(nodePath)
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Level)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// 獲取所有未軟刪除的組織節點
    /// </summary>
    public async Task<List<Organization>> GetAllActiveOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _orgRepo.GetDescendants("/")
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Level)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// 完整更新組織節點
    /// </summary>
    public async Task<Organization> EditOrganizationAsync(Organization org, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(org);

        try
        {
            return await _orgRepo.EditNodeAsync(org, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// 部分更新組織節點
    /// </summary>
    public async Task<Organization> PatchOrganizationTreeAsync(Organization org, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(org);

        try
        {
            return await _orgRepo.PatchNodeAsync(org, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// 移動部門子樹（搬移後同步重新演算 TenantLabId 繼承，已移除所有反射）
    /// </summary>
    public async Task MoveOrganizationAsync(int nodeId, int newParentId, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. 執行物理物化路徑與層級移動
            var success = await _orgRepo.MoveSubtreeAsync(nodeId, newParentId > 0 ? newParentId : null, cancellationToken).ConfigureAwait(false);
            if (!success) return;

            // 2. 移動完成後，若目標 Parent 具備 TenantLabId，則同步重置子樹 TenantLabId
            if (newParentId > 0)
            {
                var newParentNode = await _orgRepo.GetByIdAsync(newParentId, cancellationToken).ConfigureAwait(false);
                if (newParentNode?.TenantLabId != null)
                {
                    var movedNode = await _orgRepo.GetByIdAsync(nodeId, cancellationToken).ConfigureAwait(false);
                    if (movedNode != null)
                    {
                        // 🔑 直接使用強型別 Domain 方法更新，傳入目標 Level
                        movedNode.SetTenantLabId(newParentNode.TenantLabId.Value, movedNode.Level);

                        var descendants = await _orgRepo.GetDescendants(movedNode.NodePath).ToListAsync(cancellationToken).ConfigureAwait(false);
                        foreach (var desc in descendants)
                        {
                            desc.SetTenantLabId(newParentNode.TenantLabId.Value, desc.Level);
                        }

                        await _orgRepo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// 刪除組織節點（包含所有子節點）
    /// </summary>
    public async Task DeleteOrganizationAsync(int nodeId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _orgRepo.DeleteNodeWithChildrenAsync(nodeId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw;
        }
    }
}