using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGSFramework.Persistent.Repositories.Hierarchy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGS.Modules.ORG.Application.Services;

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
            var parentNode = await _orgRepo.GetByIdAsync(org.ParentId.Value, cancellationToken);
            if (parentNode != null)
            {
                // 若父節點已有 TenantLabId，則直接繼承；否則嘗試使用當前設定或產生全新 UUIDv7
                var targetTenantLabId = parentNode.TenantLabId ?? org.TenantLabId ?? Guid.CreateVersion7();
                SetTenantLabId(org, targetTenantLabId);
            }
            else
            {
                throw new KeyNotFoundException($"找不到指定的父級組織 (ParentId: {org.ParentId})");
            }
        }
        else
        {
            // 頂層區域節點，若無指定則自動給予 UUIDv7
            if (!org.TenantLabId.HasValue || org.TenantLabId == Guid.Empty)
            {
                SetTenantLabId(org, Guid.CreateVersion7());
            }
        }

        return await _orgRepo.AddNodeAsync(org, org.ParentId, cancellationToken);
    }

    /// <summary>
    /// 取得單一組織節點
    /// </summary>
    public async Task<Organization?> GetOrganizationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _orgRepo.GetByIdAsync(id, cancellationToken);
        if (entity != null && entity.IsDeleted) return null;
        return entity;
    }

    /// <summary>
    /// 取得所有根節點
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

        return await _orgRepo.GetDescendants(nodePath)
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Level)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 獲取所有未軟刪除的組織節點
    /// </summary>
    public async Task<List<Organization>> GetAllActiveOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        return await _orgRepo.GetDescendants("/")
            .AsNoTracking()
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
    /// 移動部門子樹（搬移後同步重新演算 TenantLabId 繼承）
    /// </summary>
    public async Task MoveOrganizationAsync(int nodeId, int newParentId, CancellationToken cancellationToken = default)
    {
        // 1. 執行物理物化路徑與層級移動
        var success = await _orgRepo.MoveSubtreeAsync(nodeId, newParentId > 0 ? newParentId : null, cancellationToken);
        if (!success) return;

        // 2. 移動完成後，若目標 Parent 具備 TenantLabId，則同步重置子樹 TenantLabId
        if (newParentId > 0)
        {
            var newParentNode = await _orgRepo.GetByIdAsync(newParentId, cancellationToken);
            if (newParentNode?.TenantLabId != null)
            {
                var movedNode = await _orgRepo.GetByIdAsync(nodeId, cancellationToken);
                if (movedNode != null)
                {
                    // 重置目標節點與所有子孫節點之 TenantLabId
                    SetTenantLabId(movedNode, newParentNode.TenantLabId.Value);

                    var descendants = await _orgRepo.GetDescendants(movedNode.NodePath).ToListAsync(cancellationToken);
                    foreach (var desc in descendants)
                    {
                        SetTenantLabId(desc, newParentNode.TenantLabId.Value);
                    }

                    await _orgRepo.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// 刪除組織節點（包含所有子節點）
    /// </summary>
    public async Task DeleteOrganizationAsync(int nodeId, CancellationToken cancellationToken = default)
    {
        await _orgRepo.DeleteNodeWithChildrenAsync(nodeId, cancellationToken);
    }

    /// <summary>
    /// 輔助方法：統一給予 TenantLabId（若實體有專屬方法優先呼叫，否則支援多種 Set 方式）
    /// </summary>
    private static void SetTenantLabId(Organization org, Guid tenantLabId)
    {
        // 如果 Organization 類別內有封裝業務 Setter 方法 (如 UpdateTenantLabId) 應優先使用
        var method = typeof(Organization).GetMethod("SetTenantLabId")
                     ?? typeof(Organization).GetMethod("UpdateTenantLabId");

        if (method != null)
        {
            method.Invoke(org, new object[] { tenantLabId });
        }
        else
        {
            // 透過反射強行設定私有 setter 欄位，解決 CS0272
            var prop = typeof(Organization).GetProperty(nameof(Organization.TenantLabId));
            prop?.SetValue(org, tenantLabId);
        }
    }
}