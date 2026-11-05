namespace SGS.Modules.ORG.Infrastructure.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.DTOs;

/// <summary>
/// 跨模組組織資料整合服務實作（完整配對 IOrganizationIntegrationService 防腐層介面）
/// </summary>
public class OrganizationIntegrationService(ORGDbContext orgDbContext) : IOrganizationIntegrationService
{
    private readonly ORGDbContext _orgDbContext = orgDbContext ?? throw new ArgumentNullException(nameof(orgDbContext));

    public async Task<OrganizationInfoContract?> GetOrganizationByIdAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        var org = await _orgDbContext.Organizations
            .AsNoTracking()
            .Include(x => x.Parent)
            .FirstOrDefaultAsync(x => x.TenantLabId == orgId && !x.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (org == null) return null;

        return new OrganizationInfoContract
        {
            Id = org.Id,
            TenantLabId = org.TenantLabId ?? Guid.Empty,
            Code = org.Code ?? string.Empty,
            Name = org.Name ?? string.Empty,
            NodePathString = org.NodePath ?? string.Empty,
            HierarchyLevel = org.Level,
            IsPrimary = false,
            IsActive = org.IsActive,
            ParentLabId = org.ParentId,
            ParentTenantLabId = org.Parent?.TenantLabId,
            ParentLabCode = org.Parent?.Code,
            ParentLabName = org.Parent?.Name
        };
    }

    public async Task<bool> IsInUserScopeAsync(
        string userId,
        Guid targetOrgId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid)) return false;

        return await _orgDbContext.UserAccessibleLabReadModels
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userGuid && x.TenantLabId == targetOrgId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<OrganizationInfoContract>> GetUserAccessibleOrganizationsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid)) return [];

        return await _orgDbContext.UserAccessibleLabReadModels
            .AsNoTracking()
            .Where(x => x.UserId == userGuid)
            .Select(x => new OrganizationInfoContract
            {
                Id = x.LabId,
                TenantLabId = x.TenantLabId,
                Code = x.LabCode ?? string.Empty,
                Name = x.LabName ?? string.Empty,
                NodePathString = x.Path ?? string.Empty,
                HierarchyLevel = x.HierarchyLevel,
                IsPrimary = x.IsPrimary,
                IsActive = true,
                ParentLabId = x.ParentLabId,
                ParentTenantLabId = x.ParentTenantLabId,
                ParentLabCode = x.ParentLabCode,
                ParentLabName = x.ParentLabName
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<UserPermissionProfileDto?> GetUserLabProfileAsync(
        string userId,
        Guid targetLabId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid)) return null;

        var labPermission = await _orgDbContext.UserAccessibleLabReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userGuid && x.TenantLabId == targetLabId, cancellationToken)
            .ConfigureAwait(false);

        if (labPermission == null) return null;

        return new UserPermissionProfileDto
        {
            UserId = userId,
            TenantLabId = labPermission.TenantLabId,
            LabName = labPermission.LabName,
            Permissions = [],
            Menus = []
        };
    }

    public async Task<List<OrganizationInfoContract>> GetAllActiveOrganizationsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _orgDbContext.Organizations
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive && x.TenantLabId.HasValue)
            .Select(x => new OrganizationInfoContract
            {
                Id = x.Id,
                TenantLabId = x.TenantLabId!.Value,
                Code = x.Code ?? string.Empty,
                Name = x.Name ?? string.Empty,
                NodePathString = x.NodePath ?? string.Empty,
                HierarchyLevel = x.Level,
                IsPrimary = false,
                IsActive = x.IsActive,
                ParentLabId = x.ParentId,
                ParentTenantLabId = x.Parent != null ? x.Parent.TenantLabId : null,
                ParentLabCode = x.Parent != null ? x.Parent.Code : null,
                ParentLabName = x.Parent != null ? x.Parent.Name : null
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<Guid>> GetFilteredUserIdsAsync(
        string? laboratoryParent,
        string? regionalLaboratoryCode,
        CancellationToken cancellationToken = default)
    {
        var query = _orgDbContext.UserAccessibleLabReadModels
            .AsNoTracking()
            .AsQueryable();

        // 實驗室（上層類別）：以類別「代碼」篩選。使用者可存取實驗室檢視表只有上層名稱，
        // 故先由組織表以代碼查出類別名稱再比對；代碼不存在時比對空集合，結果為無資料
        if (!string.IsNullOrWhiteSpace(laboratoryParent))
        {
            string parentCode = laboratoryParent.Trim();
            var parentNames = await _orgDbContext.Organizations
                .AsNoTracking()
                .Where(o => !o.IsDeleted && o.Code == parentCode)
                .Select(o => o.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            query = query.Where(x => x.ParentLabName != null && parentNames.Contains(x.ParentLabName));
        }

        // 區域實驗室：以實驗室「代碼」精確比對
        if (!string.IsNullOrWhiteSpace(regionalLaboratoryCode))
        {
            string code = regionalLaboratoryCode.Trim();
            query = query.Where(x => x.LabCode != null && x.LabCode == code);
        }

        return await query
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}