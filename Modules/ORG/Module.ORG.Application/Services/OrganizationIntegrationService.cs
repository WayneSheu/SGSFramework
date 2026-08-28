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
}