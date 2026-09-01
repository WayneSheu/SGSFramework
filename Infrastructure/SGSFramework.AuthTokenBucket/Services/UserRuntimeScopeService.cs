// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.AuthTokenBucket/Services/UserRuntimeScopeService.cs
// ==========================================

namespace SGSFramework.AuthTokenBucket.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Controllers.Services;
using SGSFramework.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 多租戶與動態實驗室（Lab/Department/Organization）架構下的核心執行期上下文服務。
/// 提供 3-Tier 實驗室決策矩陣、無感切換與自我修復退路機制。
/// </summary>
public class UserRuntimeScopeService(
    IOrganizationIntegrationService orgIntegrationService,
    IMemoryCache cache,
    ILogger<UserRuntimeScopeService> logger,
    UserManager<ApplicationUser> userManager,
    IDynamicControllerRepository<ControllerMetadata> controllerRepo,
    IDynamicMenuService menuService,
    IPermissionRegistry permissionRegistry,
    IUserPermissionRepository userPermissionRepository)
    : IUserRuntimeScopeService
{
    private readonly IOrganizationIntegrationService _orgIntegrationService = orgIntegrationService ?? throw new ArgumentNullException(nameof(orgIntegrationService));
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly ILogger<UserRuntimeScopeService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly UserManager<ApplicationUser> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    private readonly IDynamicControllerRepository<ControllerMetadata> _controllerRepo = controllerRepo ?? throw new ArgumentNullException(nameof(controllerRepo));
    private readonly IDynamicMenuService _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
    private readonly IPermissionRegistry _permissionRegistry = permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
    private readonly IUserPermissionRepository _userPermissionRepository = userPermissionRepository ?? throw new ArgumentNullException(nameof(userPermissionRepository));

    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);
    private const string SystemAdminRole = "sysadmin";
    private const string SuperAdminRole = "SuperAdmin";

    /// <summary>
    /// 初始化使用者執行期實驗室作用域 (3-Tier 決策機制: Requested -> Primary -> FirstAvailable)
    /// </summary>
    public async Task<UserPermissionProfileDto> InitializeUserScopeAsync(
        string userId,
        string? requestedLabId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        // 1. 取得使用者可存取的實驗室清單
        var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken).ConfigureAwait(false);
        if (accessibleLabs == null || !accessibleLabs.Any())
        {
            _logger.LogWarning("[Auth-Scope-Denied] 使用者未授權存取任何啟用的實驗室。UserId: {UserId}, RequestedLabId: {LabId}", userId, requestedLabId);
            throw new UnauthorizedAccessException("您未獲得任何實驗室的存取權限。");
        }
        // 2. 決策最終作用中實驗室 (Requested -> Primary -> FirstAvailable)
        AccessibleLabDto targetLab = ResolveTargetLab(accessibleLabs, requestedLabId);

        _logger.LogInformation("[Auth-Scope-Resolved] 已成功鎖定執行期實驗室上下文。UserId: {UserId}, TargetLabId: {LabId}, TenantLabId: {TenantLabId}, IsPrimary: {IsPrimary}",
            userId, targetLab.LabId, targetLab.TenantLabId, targetLab.IsPrimary);

        var permissions = await GetUserPermissionsAsync(userId, targetLab.TenantLabId, cancellationToken).ConfigureAwait(false);
        var menuTree = await _menuService.GetUserMenuAsync(permissions).ConfigureAwait(false);

        return new UserPermissionProfileDto
        {
            UserId = userId,
            TenantLabId = targetLab.TenantLabId,
            LabName = targetLab.LabName,
            Menus = menuTree,
            Permissions = permissions
        };
    }

    /// <summary>
    /// 取得使用者的預設主實驗室 TenantLabId (由 user_lab_mappings 中的 isPrimary 判斷)
    /// </summary>
    public async Task<Guid?> GetPrimaryLabIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        try
        {
            var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken).ConfigureAwait(false);
            if (accessibleLabs == null || !accessibleLabs.Any())
            {
                return null;
            }

            var primaryLab = accessibleLabs.FirstOrDefault(l => l.IsPrimary) ?? accessibleLabs.FirstOrDefault();
            return primaryLab?.TenantLabId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得使用者預設主實驗室發生異常。UserId: {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// 切換作用中實驗室上下文 (具備自我修復退路與警告通知機制)
    /// </summary>
    public async Task<SwitchLabResultDto> SwitchLaboratoryWithFallbackAsync(
        string userId,
        Guid? targetLabId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken).ConfigureAwait(false);
        if (accessibleLabs == null || !accessibleLabs.Any())
        {
            _logger.LogWarning("[ScopeSwitch-Denied] 使用者無可存取實驗室。UserId: {UserId}", userId);
            throw new UnauthorizedAccessException("您未獲配任何實驗室存取權限，無法執行切換。");
        }

        bool isTargetValid = targetLabId.HasValue
            && targetLabId.Value != Guid.Empty
            && accessibleLabs.Any(l => l.TenantLabId == targetLabId.Value);

        Guid finalLabId;
        bool isFallback = false;
        string? warningMessage = null;

        if (isTargetValid)
        {
            finalLabId = targetLabId!.Value;
        }
        else
        {
            var primaryLab = accessibleLabs.FirstOrDefault(l => l.IsPrimary) ?? accessibleLabs.First();
            finalLabId = primaryLab.TenantLabId;
            isFallback = true;

            warningMessage = targetLabId.HasValue && targetLabId.Value != Guid.Empty
                ? $"您無權存取指定的實驗室，系統已自動切換至主要實驗室：{primaryLab.LabName}。"
                : $"未指定有效的實驗室，系統已自動切換至主要實驗室：{primaryLab.LabName}。";

            _logger.LogWarning("[ScopeSwitch-Fallback] 使用者 {UserId} 請求無效或未授權之實驗室 {TargetLabId}，已自動降級切換至 {FinalLabId}",
                userId, targetLabId, finalLabId);
        }

        var permissions = await GetCachedOrFetchPermissionsAsync(userId, finalLabId, cancellationToken).ConfigureAwait(false);
        var menuTree = await _menuService.GetUserMenuAsync(permissions).ConfigureAwait(false);
        var targetLabInfo = accessibleLabs.First(l => l.TenantLabId == finalLabId);

        var profile = new UserPermissionProfileDto
        {
            UserId = userId,
            TenantLabId = finalLabId,
            LabName = targetLabInfo.LabName,
            Permissions = permissions,
            Menus = menuTree
        };

        return new SwitchLabResultDto
        {
            Profile = profile,
            IsFallback = isFallback,
            WarningMessage = warningMessage
        };
    }

    /// <summary>
    /// 舊有相容介面切換實作 (內部轉呼叫降級機制)
    /// </summary>
    public async Task<UserPermissionProfileDto?> SwitchLaboratoryAsync(
        string userId,
        Guid targetLabId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await SwitchLaboratoryWithFallbackAsync(userId, targetLabId, cancellationToken).ConfigureAwait(false);
            return result.Profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切換實驗室上下文發生未預期異常。UserId: {UserId}, TargetLabId: {TargetLabId}", userId, targetLabId);
            return null;
        }
    }

    /// <summary>
    /// 獲取使用者在特定實驗室下的最終權限 Key 集合 (支援全域/組織級權限與實驗室層級隔離，若 activeLabId 為 null，自動降級解析預設主實驗室)
    /// </summary>
    public async Task<IEnumerable<string>> GetUserPermissionsAsync(
        string userId,
        Guid? activeLabId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        try
        {
            var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
            if (user == null) return [];

            if (await IsSystemAdminAsync(user).ConfigureAwait(false))
            {
                var allMetas = await _controllerRepo.GetAllActiveAsync().ConfigureAwait(false);
                var allPermissions = allMetas
                    .Where(m => !string.IsNullOrEmpty(m.PermissionKey))
                    .Select(m => m.PermissionKey!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                allPermissions.Add(SystemAdminRole);
                allPermissions.Add(SuperAdminRole);
                return allPermissions;
            }

            // 1. 若 activeLabId 帶入為 null，自動採用與 LoginAsync 相同的策略：取得使用者的預設主實驗室或第一筆可用實驗室
            if (!activeLabId.HasValue)
            {
                var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken).ConfigureAwait(false);
                if (accessibleLabs != null && accessibleLabs.Any())
                {
                    var primaryLab = accessibleLabs.FirstOrDefault(l => l.IsPrimary) ?? accessibleLabs.FirstOrDefault();
                    if (primaryLab != null)
                    {
                        activeLabId = primaryLab.TenantLabId;
                    }
                }
            }

            // 2. 若經過自動解析後仍無任何實驗室上下文，才退化回純全域/組織級權限
            if (!activeLabId.HasValue)
            {
                var globalRawPermissions = await GetCachedOrFetchGlobalPermissionsAsync(userId, cancellationToken).ConfigureAwait(false);
                return ParsePermissionKeys(globalRawPermissions);
            }

            // 3. 確保該實驗室在使用者可存取清單範圍內
            var validatedAccessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken).ConfigureAwait(false);
            var targetLab = validatedAccessibleLabs.FirstOrDefault(l => l.TenantLabId == activeLabId.Value);

            if (targetLab == null)
            {
                _logger.LogWarning("[PermissionCheck] 使用者 {UserId} 嘗試查詢未授權之實驗室範圍權限: {LabId}", userId, activeLabId);
                return [];
            }

            // 4. 取得該實驗室範圍的權限清單並透過動態註冊表還原
            var rawPermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId.Value, cancellationToken).ConfigureAwait(false);
            return ParsePermissionKeys(rawPermissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取使用者權限清單時發生異常。UserId: {UserId}, ActiveLabId: {LabId}", userId, activeLabId);
            return [];
        }
    }

    /// <summary>
    /// 驗證使用者於特定實驗室下的特定 Controller ID 與 BitPosition 權限點（支援 0~63 64位元遮罩與 Controller 級解耦）
    /// </summary>
    public async Task<bool> ValidateRuntimePermissionAsync(
        string userId,
        Guid activeLabId,
        Guid controllerId,
        int bitPosition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        if (bitPosition is < 0 or > 63)
        {
            _logger.LogWarning("權限 Bit 位元位置 {BitPosition} 超過 64 位元長整型邊界 (0-63)。", bitPosition);
            return false;
        }

        try
        {
            var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
            if (user == null) return false;

            if (await IsSystemAdminAsync(user).ConfigureAwait(false)) return true;

            bool isLabScopeValid = await _orgIntegrationService.IsInUserScopeAsync(userId, activeLabId, cancellationToken).ConfigureAwait(false);
            if (!isLabScopeValid)
            {
                _logger.LogWarning("使用者 {UserId} 嘗試越權存取未授權實驗室 {LabId}", userId, activeLabId);
                return false;
            }

            // 1. 取得使用者在該 Lab 下的權限清單
            var userPermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId, cancellationToken).ConfigureAwait(false);

            // 2. 透過 Controller ID 檢索出對應的 64 位元遮罩數值
            long assignedBitmask = GetUserBitmaskForController(userPermissions, controllerId);

            // 3. 位元運算檢查對應 bitPosition 是否被允許
            return (assignedBitmask & (1L << bitPosition)) != 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證執行期權限發生異常。UserId: {UserId}, LabId: {LabId}, ControllerId: {ControllerId}, BitPosition: {BitPosition}",
                userId, activeLabId, controllerId, bitPosition);
            return false;
        }
    }

    /// <summary>
    /// 舊有相容多載：透過字串模組名稱驗證執行期權限
    /// </summary>
    public async Task<bool> ValidateRuntimePermissionAsync(
        string userId,
        Guid activeLabId,
        string module,
        int bitPosition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(module);

        if (bitPosition is < 0 or > 63) return false;

        try
        {
            var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
            if (user == null) return false;

            if (await IsSystemAdminAsync(user).ConfigureAwait(false)) return true;

            bool isLabScopeValid = await _orgIntegrationService.IsInUserScopeAsync(userId, activeLabId, cancellationToken).ConfigureAwait(false);
            if (!isLabScopeValid) return false;

            var userPermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId, cancellationToken).ConfigureAwait(false);
            var modulePerm = userPermissions.FirstOrDefault(p => p.StartsWith($"{module}:", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(modulePerm)) return false;

            var parts = modulePerm.Split(':');
            if (parts.Length < 2 || !long.TryParse(parts[1], out long bitmask)) return false;

            return (bitmask & (1L << bitPosition)) != 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證執行期權限(模組字串)發生異常。UserId: {UserId}, LabId: {LabId}, Module: {Module}", userId, activeLabId, module);
            return false;
        }
    }

    /// <summary>
    /// 獲取使用者可存取的實驗室清單 (整合 SystemAdmin 與一般使用者對映)
    /// </summary>
    public async Task<List<AccessibleLabDto>> GetAccessibleLabsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        try
        {
            var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
            if (user == null) return [];

            if (await IsSystemAdminAsync(user).ConfigureAwait(false))
            {
                var allOrgs = await _orgIntegrationService.GetAllActiveOrganizationsAsync(cancellationToken).ConfigureAwait(false);

                var adminLabs = allOrgs.Select((lab, index) => new AccessibleLabDto
                {
                    LabId = lab.Id,
                    TenantLabId = lab.TenantLabId,
                    LabCode = lab.Code ?? string.Empty,
                    LabName = lab.Name,
                    Path = lab.NodePathString,
                    HierarchyLevel = lab.HierarchyLevel,
                    IsPrimary = index == 0,
                    ParentLabId = lab.ParentLabId,
                    ParentTenantLabId = lab.ParentTenantLabId,
                    ParentLabCode = lab.ParentLabCode,
                    ParentLabName = lab.ParentLabName
                }).ToList();

                if (!adminLabs.Any())
                {
                    adminLabs.Add(CreateFallbackAdminLab());
                }

                return adminLabs;
            }

            var orgs = await _orgIntegrationService.GetUserAccessibleOrganizationsAsync(userId, cancellationToken).ConfigureAwait(false);

            return orgs.Select(x => new AccessibleLabDto
            {
                LabId = x.Id,
                TenantLabId = x.TenantLabId,
                LabCode = x.Code ?? string.Empty,
                LabName = x.Name,
                Path = x.NodePathString,
                HierarchyLevel = x.HierarchyLevel,
                IsPrimary = x.IsPrimary,
                ParentLabId = x.ParentLabId,
                ParentTenantLabId = x.ParentTenantLabId,
                ParentLabCode = x.ParentLabCode,
                ParentLabName = x.ParentLabName
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取可存取實驗室清單時發生異常。UserId: {UserId}", userId);
            return [];
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// 從權限字串集合中解析出特定 Controller ID 所對應的 64 位元遮罩數值
    /// </summary>
    private static long GetUserBitmaskForController(IEnumerable<string> rawPermissions, Guid controllerId)
    {
        string targetIdStr = controllerId.ToString();
        foreach (var perm in rawPermissions)
        {
            var parts = perm.Split(':');
            if (parts.Length >= 2 && parts[0].Equals(targetIdStr, StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(parts[1], out long mask)) return mask;
            }
        }
        return 0L;
    }

    /// <summary>
    /// 解析最終作用中實驗室 (Requested -> Primary -> FirstAvailable)
    /// </summary>
    private static AccessibleLabDto ResolveTargetLab(List<AccessibleLabDto> accessibleLabs, string? requestedLabId)
    {
        AccessibleLabDto? target = null;

        if (!string.IsNullOrWhiteSpace(requestedLabId))
        {
            target = accessibleLabs.FirstOrDefault(l =>
                l.LabId.ToString().Equals(requestedLabId, StringComparison.OrdinalIgnoreCase) ||
                l.TenantLabId.ToString().Equals(requestedLabId, StringComparison.OrdinalIgnoreCase) ||
                l.LabCode.Equals(requestedLabId, StringComparison.OrdinalIgnoreCase));
        }

        target ??= accessibleLabs.FirstOrDefault(l => l.IsPrimary);
        target ??= accessibleLabs.FirstOrDefault();

        return target ?? baseResult();

        AccessibleLabDto baseResult() => throw new UnauthorizedAccessException("無法解析有效的實驗室節點。");
    }

    private async Task<bool> IsSystemAdminAsync(ApplicationUser? user)
    {
        if (user == null) return false;

        return await _userManager.IsInRoleAsync(user, SuperAdminRole).ConfigureAwait(false) ||
               await _userManager.IsInRoleAsync(user, SystemAdminRole).ConfigureAwait(false);
    }

    private static AccessibleLabDto CreateFallbackAdminLab()
    {
        return new AccessibleLabDto
        {
            LabId = 0,
            TenantLabId = Guid.Empty,
            LabCode = "GLOBAL",
            LabName = "全域管理員預設實驗室",
            Path = "/GLOBAL",
            HierarchyLevel = 0,
            IsPrimary = true,
            ParentLabId = null,
            ParentTenantLabId = null,
            ParentLabCode = null,
            ParentLabName = null
        };
    }

    private async Task<IEnumerable<string>> GetCachedOrFetchPermissionsAsync(
        string userId,
        Guid labId,
        CancellationToken cancellationToken)
    {
        string cacheKey = $"perm_scope:{userId}:{labId}";

        var permissions = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiration;
            var permissionDict = await FetchUserModulePermissionsFromDbAsync(userId, labId, cancellationToken).ConfigureAwait(false);
            return permissionDict.Select(p => $"{p.Key}:{p.Value}").ToList();
        }).ConfigureAwait(false);

        return permissions ?? [];
    }

    private async Task<IEnumerable<string>> GetCachedOrFetchGlobalPermissionsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        string cacheKey = $"perm_scope:global:{userId}";

        var permissions = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiration;
            var permissionDict = await FetchUserGlobalPermissionsFromDbAsync(userId, cancellationToken).ConfigureAwait(false);
            return permissionDict.Select(p => $"{p.Key}:{p.Value}").ToList();
        }).ConfigureAwait(false);

        return permissions ?? [];
    }

    /// <summary>
    /// 從真實資料庫倉儲獲取使用者在特定實驗室下的模組/Controller 權限 64 位元遮罩對應表
    /// </summary>
    private async Task<Dictionary<string, long>> FetchUserModulePermissionsFromDbAsync(
        string userId,
        Guid labId,
        CancellationToken cancellationToken)
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);

            var permissions = await _userPermissionRepository.GetPermissionsByLabAsync(userId, labId, cancellationToken).ConfigureAwait(false);
            return permissions ?? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "從資料庫獲取使用者實驗室模組權限發生異常。UserId: {UserId}, LabId: {LabId}", userId, labId);
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 從真實資料庫倉儲獲取使用者的全域/組織級權限 64 位元遮罩對應表
    /// </summary>
    private async Task<Dictionary<string, long>> FetchUserGlobalPermissionsFromDbAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);

            var globalPermissions = await _userPermissionRepository.GetGlobalPermissionsAsync(userId, cancellationToken).ConfigureAwait(false);
            return globalPermissions ?? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "從資料庫獲取使用者全域權限發生異常。UserId: {UserId}", userId);
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 解析原始權限字串，透過 IPermissionRegistry 將位元遮罩動態還原為具體的業務權限 Key
    /// </summary>
    private HashSet<string> ParsePermissionKeys(IEnumerable<string> rawPermissions)
    {
        var permissionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var permString in rawPermissions)
        {
            var parts = permString.Split(':');
            if (parts.Length >= 2 && long.TryParse(parts[1], out long mask))
            {
                string module = parts[0];
                for (int bit = 0; bit < 64; bit++)
                {
                    if ((mask & (1L << bit)) != 0)
                    {
                        // 透過動態註冊表反向解析出具體權限字串（例如 ORG_LAB_READ）
                        var resolvedKey = _permissionRegistry.ResolvePermissionKey(module, bit);
                        if (!string.IsNullOrEmpty(resolvedKey))
                        {
                            permissionKeys.Add(resolvedKey);
                        }
                    }
                }
            }
            else
            {
                permissionKeys.Add(permString);
            }
        }

        foreach (var permString in rawPermissions)
        {
            var parts = permString.Split(':');
            if (parts.Length == 1)
            {
                permissionKeys.Add(parts[0]);
            }
        }

        return permissionKeys;
    }

    #endregion
}