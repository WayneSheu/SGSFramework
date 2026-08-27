// ==========================================
// 檔案路徑: src/SGS.Modules.ORG/Infrastructure/SGS.Modules.ORG.Infrastructure/Services/OrganizationIntegrationService.cs
// 架構層級: Infrastructure Layer / Services (Adapter)
// ==========================================

namespace SGS.Modules.ORG.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        if (orgId == Guid.Empty)
        {
            return null;
        }

        try
        {
            var org = await _orgDbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantLabId == orgId && !x.IsDeleted, cancellationToken);

            if (org is null)
            {
                return null;
            }

            return new OrganizationInfoContract(
                Id: org.TenantLabId!.Value,
                Name: org.Name ?? string.Empty,
                NodePathString: org.NodePath ?? string.Empty,
                HierarchyLevel: org.Level,
                IsPrimary: false,
                IsActive: org.IsActive
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "依據 ID {OrgId} 取得組織詳細資訊時發生異常。", orgId);
            return null;
        }
    }

    /// <summary>
    /// 高效驗證目標組織是否在使用者的管轄/存取組織階層範疇內 (雙重狀態防護機制)
    /// </summary>
    public async Task<bool> IsInUserScopeAsync(
        string userId,
        Guid targetOrgId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userIdGuid) || targetOrgId == Guid.Empty)
        {
            _logger.LogWarning("無效的驗證參數。UserId: {UserId}, TargetOrgId: {TargetOrgId}", userId, targetOrgId);
            return false;
        }

        try
        {
            // 1. 驗證 User-Lab 對映紀錄與 Active 狀態
            var mappings = await _userLabRepository.GetAccessibleLabsAsync(userIdGuid, cancellationToken);
            if (mappings == null || !mappings.Any())
            {
                return false;
            }

            var targetMapping = mappings.FirstOrDefault(m => m.TenantLabId == targetOrgId && m.IsActive);
            if (targetMapping is null)
            {
                return false;
            }

            // 2. 驗證 ORG 組織主實體未被刪除且為啟用狀態
            return await _orgDbContext.Organizations
                .AsNoTracking()
                .AnyAsync(x => x.TenantLabId == targetOrgId && !x.IsDeleted && x.IsActive, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證使用者 {UserId} 對目標組織 {TargetOrgId} 的存取權限時發生異常。", userId, targetOrgId);
            return false;
        }
    }

    /// <summary>
    /// 獲取使用者有權存取的所有組織節點清單 (精確封裝 user_lab_mapping 的 IsPrimary 與 IsActive 狀態)
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
            // 1. 取得使用者對映紀錄
            var mappings = await _userLabRepository.GetAccessibleLabsAsync(userIdGuid, cancellationToken);
            if (mappings == null || !mappings.Any())
            {
                return new List<OrganizationInfoContract>();
            }

            // 2. 建立高效對映快照字典 (保留 IsPrimary 與優先序，併發排除重複實驗室識別碼)
            var validMappingsDict = mappings
                .Where(m => m.TenantLabId != Guid.Empty && m.IsActive)
                .GroupBy(m => m.TenantLabId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.IsPrimary).First(),
                    EqualityComparer<Guid>.Default
                );

            if (!validMappingsDict.Any())
            {
                return new List<OrganizationInfoContract>();
            }

            var labGuids = validMappingsDict.Keys.ToList();

            // 3. 查詢 ORG 模組實體主表 (強制限制未刪除且啟用中)
            var orgs = await _orgDbContext.Organizations
                .AsNoTracking()
                .Where(x => x.TenantLabId.HasValue
                         && labGuids.Contains(x.TenantLabId.Value)
                         && !x.IsDeleted
                         && x.IsActive)
                .ToListAsync(cancellationToken);

            // 4. 組合多層級屬性 (以 IsPrimary 優先排序)
            return orgs.Select(org =>
            {
                var labId = org.TenantLabId!.Value;
                var mapping = validMappingsDict.GetValueOrDefault(labId);

                return new OrganizationInfoContract(
                    Id: labId,
                    Name: org.Name ?? "未命名實驗室",
                    NodePathString: org.NodePath ?? string.Empty,
                    HierarchyLevel: org.Level,
                    IsPrimary: mapping?.IsPrimary ?? false,
                    IsActive: (mapping?.IsActive ?? true) && org.IsActive
                );
            })
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.HierarchyLevel)
            .ToList();
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
                .Where(x => !x.IsDeleted && x.IsActive && x.TenantLabId.HasValue)
                .OrderBy(x => x.NodePath)
                .ToListAsync(cancellationToken);

            return rawOrganizations.Select((org, index) => new OrganizationInfoContract(
                Id: org.TenantLabId!.Value,
                Name: org.Name ?? "未命名實驗室",
                NodePathString: org.NodePath ?? string.Empty,
                HierarchyLevel: org.Level,
                IsPrimary: index == 0, // 全域管理員預設第一個 Active 節點為 Primary
                IsActive: org.IsActive
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
        ArgumentException.ThrowIfNullOrEmpty(userId);

        if (targetLabId == Guid.Empty)
        {
            return null;
        }

        try
        {
            if (!await IsInUserScopeAsync(userId, targetLabId, cancellationToken))
            {
                _logger.LogWarning("使用者 {UserId} 嘗試存取未授權的實驗室 Profile {TargetLabId}", userId, targetLabId);
                return null;
            }

            var org = await GetOrganizationByIdAsync(targetLabId, cancellationToken);
            if (org is null)
            {
                return null;
            }

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