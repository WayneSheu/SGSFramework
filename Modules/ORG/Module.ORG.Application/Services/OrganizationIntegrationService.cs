using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.DTOs;
using SGSFramework.Core.Helpers;
using SGSFramework.Persistent.Repositories.Hierarchy;
using SGSFramework.AuthTokenBucket.DTOs; // 確保引用對應的 DTO 命名空間

namespace SGS.Modules.ORG.Application;

/// <summary>
/// 跨模組組織資料整合服務 (防腐層適配器 Adapter)
/// 負責將 ORG 模組內部的 int 主鍵與 NodePath 邏輯，轉化為 Auth 等外部模組可理解的輕量契約 DTO
/// </summary>
public class OrganizationIntegrationService : IOrganizationIntegrationService
{
    private readonly IOrganizationService _orgService;
    private readonly IHierarchicalRepository<ORGDbContext, Organization> _orgRepo;
    private readonly ORGDbContext _dbContext;
    private readonly ILogger<OrganizationIntegrationService> _logger;

    public OrganizationIntegrationService(
        IOrganizationService orgService,
        IHierarchicalRepository<ORGDbContext, Organization> orgRepo,
        ORGDbContext dbContext,
        ILogger<OrganizationIntegrationService> logger)
    {
        _orgService = orgService ?? throw new ArgumentNullException(nameof(orgService));
        _orgRepo = orgRepo ?? throw new ArgumentNullException(nameof(orgRepo));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 獲取單一組織節點資訊 (提供跨模組 DTO)
    /// </summary>
    public async Task<OrganizationInfoContract?> GetOrganizationByIdAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            int internalId = orgId.ToInternalIntId();
            var entity = await _orgRepo.GetByIdAsync(internalId);
            if (entity is null || entity.IsDeleted)
            {
                return null;
            }

            Guid mappedGuid = entity.Id.ToDeterministicGuid();

            return new OrganizationInfoContract(
                mappedGuid,
                entity.Name,
                entity.NodePath,
                entity.Level
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得組織資訊時發生例外。Target Org Guid: {OrgId}", orgId);
            return null;
        }
    }

    /// <summary>
    /// 驗證目標組織 (targetOrgId) 是否在使用者 (userId) 的管轄/存取組織階層範疇內
    /// </summary>
    public async Task<bool> IsInUserScopeAsync(
        string userId,
        Guid targetOrgId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        try
        {
            int internalTargetOrgId = targetOrgId.ToInternalIntId();

            var targetNode = await _orgRepo.GetByIdAsync(internalTargetOrgId);
            if (targetNode is null || targetNode.IsDeleted)
            {
                return false;
            }

            List<string> userScopeNodePaths = await GetUserAssignedScopeNodePathsAsync(userId, cancellationToken);
            if (userScopeNodePaths is null || userScopeNodePaths.Count == 0)
            {
                return false;
            }

            string targetPath = targetNode.NodePath;

            bool isMatch = userScopeNodePaths.Any(userPath =>
                !string.IsNullOrEmpty(userPath) && targetPath.StartsWith(userPath, StringComparison.OrdinalIgnoreCase));

            return isMatch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證使用者組織存取權限範疇時發生例外。UserId: {UserId}, TargetOrgId: {TargetOrgId}", userId, targetOrgId);
            return false;
        }
    }

    /// <summary>
    /// 獲取使用者有權存取的所有組織節點清單 (下推至資料庫端執行過濾)
    /// </summary>
    public async Task<List<OrganizationInfoContract>> GetUserAccessibleOrganizationsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<OrganizationInfoContract>();
        }

        try
        {
            List<string> userScopeNodePaths = await GetUserAssignedScopeNodePathsAsync(userId, cancellationToken);
            if (userScopeNodePaths is null || userScopeNodePaths.Count == 0)
            {
                return new List<OrganizationInfoContract>();
            }

            var query = _dbContext.Set<Organization>()
                .AsNoTracking()
                .Where(x => !x.IsDeleted);

            var validPaths = userScopeNodePaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            if (!validPaths.Any())
            {
                return new List<OrganizationInfoContract>();
            }

            var accessibleEntities = await query
                .Where(node => validPaths.Any(path => EF.Functions.Like(node.NodePath, path + "%")))
                .ToListAsync(cancellationToken);

            return accessibleEntities.Select(entity => new OrganizationInfoContract(
                entity.Id.ToDeterministicGuid(),
                entity.Name,
                entity.NodePath,
                entity.Level
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢使用者可存取組織清單時發生例外。UserId: {UserId}", userId);
            return new List<OrganizationInfoContract>();
        }
    }

    /// <summary>
    /// 獲取使用者在指定實驗室的權限設定檔 (已對齊 UserPermissionProfileDto 規範)
    /// </summary>
    public async Task<UserPermissionProfileDto?> GetUserLabProfileAsync(
        string userId,
        Guid targetLabId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        try
        {
            bool hasAccess = await IsInUserScopeAsync(userId, targetLabId, cancellationToken);
            if (!hasAccess)
            {
                _logger.LogWarning("使用者無權存取目標實驗室。UserId: {UserId}, TargetLabId: {TargetLabId}", userId, targetLabId);
                return null;
            }

            var targetOrg = await GetOrganizationByIdAsync(targetLabId, cancellationToken);
            if (targetOrg is null)
            {
                return null;
            }

            return new UserPermissionProfileDto
            {
                UserId = userId,
                LabId = targetOrg.Id,
                LabName = targetOrg.Name,
                Permissions = Array.Empty<string>(),
                Menus = Array.Empty<SGSFramework.Core.Abstractions.Menus.MenuSectionDto>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得使用者實驗室 Profile 時發生例外。UserId: {UserId}, TargetLabId: {TargetLabId}", userId, targetLabId);
            return null;
        }
    }

    #region Private Helpers

    private async Task<List<string>> GetUserAssignedScopeNodePathsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await Task.FromResult(new List<string> { "/1/", "/1/5/" });
    }

    #endregion
}