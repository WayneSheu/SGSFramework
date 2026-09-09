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
/// 多租戶與動態實驗室架構下的核心執行期上下文服務 (重構版：支援無限位元權限點與特權繞過)
/// </summary>
public class UserRuntimeScopeService(
    IOrganizationIntegrationService orgIntegrationService,
    IMemoryCache cache,
    ILogger<UserRuntimeScopeService> logger,
    UserManager<ApplicationUser> userManager,
    IDynamicControllerRepository<ControllerMetadata> controllerRepo,
    IMenuStrategyFactory menuStrategyFactory,
    IPermissionRegistry permissionRegistry,
    IUserPermissionRepository userPermissionRepository)
    : IUserRuntimeScopeService
{
    private readonly IOrganizationIntegrationService _orgIntegrationService = orgIntegrationService ?? throw new ArgumentNullException(nameof(orgIntegrationService));
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly ILogger<UserRuntimeScopeService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly UserManager<ApplicationUser> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    private readonly IDynamicControllerRepository<ControllerMetadata> _controllerRepo = controllerRepo ?? throw new ArgumentNullException(nameof(controllerRepo));
    private readonly IMenuStrategyFactory _menuStrategyFactory = menuStrategyFactory ?? throw new ArgumentNullException(nameof(menuStrategyFactory));
    private readonly IPermissionRegistry _permissionRegistry = permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
    private readonly IUserPermissionRepository _userPermissionRepository = userPermissionRepository ?? throw new ArgumentNullException(nameof(userPermissionRepository));

    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);
    private const string SystemAdminRole = "sysadmin";
    private const string SuperAdminRole = "SuperAdmin";

    public async Task<UserPermissionProfileDto> InitializeUserScopeAsync(
        string userId,
        string? requestedLabId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId, nameof(userId));

        var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken).ConfigureAwait(false);
        if (accessibleLabs == null || accessibleLabs.Count == 0)
        {
            _logger.LogWarning("[Auth-Scope-Denied] 使用者未授權存取任何啟用的實驗室。UserId: {UserId}, RequestedLabId: {LabId}", userId, requestedLabId);
            throw new UnauthorizedAccessException("您未獲得任何實驗室的存取權限。");
        }

        AccessibleLabDto targetLab = ResolveTargetLab(accessibleLabs, requestedLabId);

        _logger.LogInformation("[Auth-Scope-Resolved] 已成功鎖定執行期實驗室上下文。UserId: {UserId}, TargetLabId: {LabId}, TenantLabId: {TenantLabId}, IsPrimary: {IsPrimary}",
            userId, targetLab.LabId, targetLab.TenantLabId, targetLab.IsPrimary);

        var permissions = await GetUserPermissionsAsync(userId, targetLab.TenantLabId, cancellationToken).ConfigureAwait(false);

        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        bool isAdmin = user != null && await IsSystemAdminAsync(user).ConfigureAwait(false);
        var menuStrategy = _menuStrategyFactory.GetStrategy(MenuStrategyType.DatabaseDriven);
        var menuTree = await menuStrategy.BuildMenuTreeAsync(permissions, isAdmin, cancellationToken).ConfigureAwait(false);

        return new UserPermissionProfileDto
        {
            UserId = userId,
            TenantLabId = targetLab.TenantLabId,
            LabName = targetLab.LabName,
            Menus = menuTree,
            Permissions = permissions
        };
    }

    public async Task<Guid?> GetPrimaryLabIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId, nameof(userId));

        try
        {
            var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken).ConfigureAwait(false);
            if (accessibleLabs == null || accessibleLabs.Count == 0) return null;

            var primaryLab = accessibleLabs.FirstOrDefault(l => l.IsPrimary) ?? accessibleLabs.FirstOrDefault();
            return primaryLab?.TenantLabId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得使用者預設主實驗室發生異常。UserId: {UserId}", userId);
            return null;
        }
    }

    public async Task<SwitchLabResultDto> SwitchLaboratoryWithFallbackAsync(
        string userId,
        Guid? targetLabId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId, nameof(userId));

        var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken).ConfigureAwait(false);
        if (accessibleLabs == null || accessibleLabs.Count == 0)
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

        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        bool isAdmin = user != null && await IsSystemAdminAsync(user).ConfigureAwait(false);
        var menuStrategy = _menuStrategyFactory.GetStrategy(MenuStrategyType.DatabaseDriven);
        var menuTree = await menuStrategy.BuildMenuTreeAsync(permissions, isAdmin, cancellationToken).ConfigureAwait(false);

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

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(
        string userId,
        Guid? activeLabId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId, nameof(userId));

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

            if (!activeLabId.HasValue)
            {
                var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken).ConfigureAwait(false);
                if (accessibleLabs != null && accessibleLabs.Count > 0)
                {
                    var primaryLab = accessibleLabs.FirstOrDefault(l => l.IsPrimary) ?? accessibleLabs.FirstOrDefault();
                    if (primaryLab != null) activeLabId = primaryLab.TenantLabId;
                }
            }

            if (!activeLabId.HasValue)
            {
                var globalRawPermissions = await GetCachedOrFetchGlobalPermissionsAsync(userId, cancellationToken).ConfigureAwait(false);
                return ParsePermissionKeys(globalRawPermissions);
            }

            var validatedAccessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken).ConfigureAwait(false);
            var targetLab = validatedAccessibleLabs.FirstOrDefault(l => l.TenantLabId == activeLabId.Value);

            if (targetLab == null)
            {
                _logger.LogWarning("[PermissionCheck] 使用者 {UserId} 嘗試查詢未授權之實驗室範圍權限: {LabId}", userId, activeLabId);
                return [];
            }

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
    /// 驗證特定 Controller ID 與 BitPosition 權限點（支援超過 64 位元之延伸索引）
    /// </summary>
    public async Task<bool> ValidateRuntimePermissionAsync(
        string userId,
        Guid activeLabId,
        Guid controllerId,
        int bitPosition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId, nameof(userId));
        if (bitPosition < 0) return false;

        try
        {
            var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
            if (user == null) return false;

            // 特權過濾：系統管理員全域放行
            if (await IsSystemAdminAsync(user).ConfigureAwait(false)) return true;

            bool isLabScopeValid = await _orgIntegrationService.IsInUserScopeAsync(userId, activeLabId, cancellationToken).ConfigureAwait(false);
            if (!isLabScopeValid)
            {
                _logger.LogWarning("使用者 {UserId} 嘗試越權存取未授權實驗室 {LabId}", userId, activeLabId);
                return false;
            }

            var userPermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId, cancellationToken).ConfigureAwait(false);
            return ValidateBitPositionPermission(userPermissions, controllerId.ToString(), bitPosition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證執行期權限發生異常。UserId: {UserId}, LabId: {LabId}, ControllerId: {ControllerId}, BitPosition: {BitPosition}",
                userId, activeLabId, controllerId, bitPosition);
            return false;
        }
    }

    /// <summary>
    /// 透過模組代碼驗證動態位元權限點（支援超過 64 位元之延伸索引）
    /// </summary>
    public async Task<bool> ValidateRuntimePermissionAsync(
        string userId,
        Guid activeLabId,
        string module,
        int bitPosition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId, nameof(userId));
        ArgumentException.ThrowIfNullOrEmpty(module, nameof(module));
        if (bitPosition < 0) return false;

        try
        {
            var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
            if (user == null) return false;

            // 特權過濾：系統管理員全域放行
            if (await IsSystemAdminAsync(user).ConfigureAwait(false)) return true;

            bool isLabScopeValid = await _orgIntegrationService.IsInUserScopeAsync(userId, activeLabId, cancellationToken).ConfigureAwait(false);
            if (!isLabScopeValid) return false;

            var userPermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId, cancellationToken).ConfigureAwait(false);
            return ValidateBitPositionPermission(userPermissions, module, bitPosition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證執行期權限(模組字串)發生異常。UserId: {UserId}, LabId: {LabId}, Module: {Module}, BitPosition: {BitPosition}",
                userId, activeLabId, module, bitPosition);
            return false;
        }
    }

    public async Task<List<AccessibleLabDto>> GetAccessibleLabsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId, nameof(userId));

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

                if (adminLabs.Count == 0) adminLabs.Add(CreateFallbackAdminLab());
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

    #region Private Helpers

    /// <summary>
    /// 解析位元位置授權：支援 Bitmask (0-63) 或 延伸 BitPosition 陣列清單[cite: 11]
    /// </summary>
    private static bool ValidateBitPositionPermission(IEnumerable<string> rawPermissions, string targetKey, int bitPosition)
    {
        foreach (var perm in rawPermissions)
        {
            var parts = perm.Split(':');
            if (parts.Length < 2 || !parts[0].Equals(targetKey, StringComparison.OrdinalIgnoreCase)) continue;

            string bitValue = parts[1];

            // 模式 A: 舊有 64 位元 Bitmask (例: "MODULE:18446744073709551615")[cite: 11, 13]
            if (bitPosition < 64 && long.TryParse(bitValue, out long bitmask))
            {
                if ((bitmask & (1L << bitPosition)) != 0) return true;
            }

            // 模式 B: 擴充超大位元陣列清單 (例: "MODULE:0,1,5,65,128")[cite: 11]
            var grantedBits = bitValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var bitStr in grantedBits)
            {
                if (int.TryParse(bitStr, out int grantedBit) && grantedBit == bitPosition)
                {
                    return true;
                }
            }
        }
        return false;
    }

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

        return target ?? throw new UnauthorizedAccessException("無法解析有效的實驗室節點。");
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

    private async Task<Dictionary<string, long>> FetchUserModulePermissionsFromDbAsync(
        string userId,
        Guid labId,
        CancellationToken cancellationToken)
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(userId, nameof(userId));
            var permissions = await _userPermissionRepository.GetPermissionsByLabAsync(userId, labId, cancellationToken).ConfigureAwait(false);
            return permissions ?? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "從資料庫獲取使用者實驗室模組權限發生異常。UserId: {UserId}, LabId: {LabId}", userId, labId);
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<Dictionary<string, long>> FetchUserGlobalPermissionsFromDbAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(userId, nameof(userId));
            var globalPermissions = await _userPermissionRepository.GetGlobalPermissionsAsync(userId, cancellationToken).ConfigureAwait(false);
            return globalPermissions ?? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "從資料庫獲取使用者全域權限發生異常。UserId: {UserId}", userId);
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private HashSet<string> ParsePermissionKeys(IEnumerable<string> rawPermissions)
    {
        var permissionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var permString in rawPermissions)
        {
            var parts = permString.Split(':');
            if (parts.Length >= 2)
            {
                string module = parts[0];
                string bitValue = parts[1];

                // 解析 64 位元 Bitmask[cite: 11, 13]
                if (long.TryParse(bitValue, out long mask))
                {
                    for (int bit = 0; bit < 64; bit++)
                    {
                        if ((mask & (1L << bit)) != 0)
                        {
                            var resolvedKey = _permissionRegistry.ResolvePermissionKey(module, bit);
                            if (!string.IsNullOrEmpty(resolvedKey)) permissionKeys.Add(resolvedKey);
                        }
                    }
                }
                else // 解析延伸 BitPositions (例: "0,1,78,128")[cite: 11]
                {
                    var bitArray = bitValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var bitStr in bitArray)
                    {
                        if (int.TryParse(bitStr, out int bitPosition))
                        {
                            var resolvedKey = _permissionRegistry.ResolvePermissionKey(module, bitPosition);
                            if (!string.IsNullOrEmpty(resolvedKey)) permissionKeys.Add(resolvedKey);
                        }
                    }
                }
            }
            else if (parts.Length == 1)
            {
                permissionKeys.Add(parts[0]);
            }
        }

        return permissionKeys;
    }

    #endregion
}