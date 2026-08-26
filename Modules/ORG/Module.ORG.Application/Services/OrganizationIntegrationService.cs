using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.DTOs;

namespace SGS.Modules.ORG.Infrastructure.Services;

/// <summary>
/// 跨模組組織資料整合服務實作 (防腐層適配器 Adapter)
/// </summary>
public class OrganizationIntegrationService : IOrganizationIntegrationService
{
    private readonly ORGDbContext _orgDbContext;
    private readonly IUserLabRepository _userLabRepository;
    private readonly ILogger<OrganizationIntegrationService> _logger;

    public OrganizationIntegrationService(
        ORGDbContext orgDbContext,
        IUserLabRepository userLabRepository,
        ILogger<OrganizationIntegrationService> logger)
    {
        _orgDbContext = orgDbContext ?? throw new ArgumentNullException(nameof(orgDbContext));
        _userLabRepository = userLabRepository ?? throw new ArgumentNullException(nameof(userLabRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 獲取單一組織節點資訊
    /// </summary>
    public async Task<OrganizationInfoContract?> GetOrganizationByIdAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var org = await _orgDbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => ( x.TenantLabId == orgId) && !x.IsDeleted, cancellationToken);

            if (org == null) return null;

            return new OrganizationInfoContract(
                Id: org.TenantLabId.Value ,
                Name: org.Name ?? string.Empty,
                NodePathString: org.NodePath ?? string.Empty,
                HierarchyLevel: org.Level
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "依據 ID {OrgId} 取得組織詳細資訊時發生異常。", orgId);
            return null;
        }
    }

    /// <summary>
    /// 驗證目標組織是否在使用者的管轄/存取組織階層範疇內
    /// </summary>
    public async Task<bool> IsInUserScopeAsync(
        string userId,
        Guid targetOrgId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var accessibleOrgs = await GetUserAccessibleOrganizationsAsync(userId, cancellationToken);
            return accessibleOrgs.Any(o => o.Id == targetOrgId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證使用者 {UserId} 對目標組織 {TargetOrgId} 的存取權限時發生異常。", userId, targetOrgId);
            return false;
        }
    }

    /// <summary>
    /// 獲取使用者有權存取的所有組織節點清單
    /// </summary>
    public async Task<List<OrganizationInfoContract>> GetUserAccessibleOrganizationsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userIdGuid))
        {
            _logger.LogWarning("無效的使用者識別碼格式: {UserId}", userId);
            return new List<OrganizationInfoContract>();
        }

        try
        {
            var mappings = await _userLabRepository.GetAccessibleLabsAsync(userIdGuid, cancellationToken);
            if (mappings == null || !mappings.Any())
            {
                return new List<OrganizationInfoContract>();
            }

            var labGuids = mappings
                .Where(m => m.TenantLabId != Guid.Empty)
                .Select(m => m.TenantLabId)
                .Distinct()
                .ToList();

            var orgs = await _orgDbContext.Organizations
                .AsNoTracking()
                .Where(x => x.TenantLabId.HasValue
                         && labGuids.Contains(x.TenantLabId.Value)
                         && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            return orgs.Select(org => new OrganizationInfoContract(
                Id: org.TenantLabId!.Value,
                Name: org.Name ?? "未命名實驗室",
                NodePathString: org.NodePath ?? string.Empty,
                HierarchyLevel: org.Level
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得使用者 {UserId} 可存取的組織清單時發生異常。", userId);
            return new List<OrganizationInfoContract>();
        }
    }

    /// <summary>
    /// 獲取全系統所有未刪除且有效的區域實驗室/組織 (專供系統管理員特權開路呼叫)
    /// </summary>
    public async Task<List<OrganizationInfoContract>> GetAllActiveOrganizationsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rawOrganizations = await _orgDbContext.Organizations
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.TenantLabId.HasValue)
                .OrderBy(x => x.NodePath)
                .ToListAsync(cancellationToken);

            return rawOrganizations.Select(org => new OrganizationInfoContract(
                Id: org.TenantLabId!.Value,
                Name: org.Name ?? "未命名實驗室",
                NodePathString: org.NodePath ?? string.Empty,
                HierarchyLevel: org.Level
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得全系統活動區域實驗室時發生異常。");
            return new List<OrganizationInfoContract>();
        }
    }

    /// <summary>
    /// 獲取使用者在指定實驗室的權限設定檔
    /// </summary>
    public async Task<UserPermissionProfileDto?> GetUserLabProfileAsync(
        string userId,
        Guid targetLabId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await IsInUserScopeAsync(userId, targetLabId, cancellationToken))
            {
                _logger.LogWarning("使用者 {UserId} 嘗試存取未授權的實驗室 Profile {TargetLabId}", userId, targetLabId);
                return null;
            }

            var org = await GetOrganizationByIdAsync(targetLabId, cancellationToken);
            if (org == null) return null;

            return new UserPermissionProfileDto
            {
                UserId = userId,
                LabId = org.Id,
                LabName = org.Name,
                Permissions = Enumerable.Empty<string>(),
                Menus = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取使用者 {UserId} 在實驗室 {TargetLabId} 的權限設定檔時發生異常。", userId, targetLabId);
            return null;
        }
    }
}