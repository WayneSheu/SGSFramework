using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.Controllers.Services;
using SGSFramework.Core.DTOs;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>
/// 多租戶與動態實驗室（Lab/Department/Organization）架構下的核心執行期上下文服務。
/// </summary>
public class UserRuntimeScopeService : IUserRuntimeScopeService
{
    private readonly IOrganizationIntegrationService _orgIntegrationService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UserRuntimeScopeService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDynamicControllerRepository<ControllerMetadata> _controllerRepo;
    private readonly IDynamicMenuService _menuService;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

    public UserRuntimeScopeService(
        IOrganizationIntegrationService orgIntegrationService,
        IMemoryCache cache,
        ILogger<UserRuntimeScopeService> logger,
        UserManager<ApplicationUser> userManager,
        IDynamicControllerRepository<ControllerMetadata> controllerRepo,
        IDynamicMenuService menuService)
    {
        _orgIntegrationService = orgIntegrationService ?? throw new ArgumentNullException(nameof(orgIntegrationService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _controllerRepo = controllerRepo ?? throw new ArgumentNullException(nameof(controllerRepo));
        _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
    }

    public async Task<UserPermissionProfileDto> InitializeUserScopeAsync(
        string userId,
        string? requestedLabId,
        CancellationToken cancellationToken)
    {
        var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken);

        var targetLab = accessibleLabs.FirstOrDefault(l => l.LabId.ToString() == requestedLabId)
                     ?? accessibleLabs.FirstOrDefault();

        if (targetLab == null)
        {
            _logger.LogError("使用者 {UserId} 無權存取任何實驗室，且全域開路降級亦未找到可用節點。", userId);
            throw new UnauthorizedAccessException("無權存取任何實驗室。");
        }

        var permissions = await GetUserPermissionsAsync(userId, targetLab.LabId, cancellationToken);
        var menuTree = await _menuService.GetUserMenuAsync(permissions);

        return new UserPermissionProfileDto
        {
            UserId = userId,
            LabId = targetLab.LabId,
            LabName = targetLab.LabName,
            Menus = menuTree,
            Permissions = permissions
        };
    }

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(
        string userId,
        Guid? activeLabId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && (await _userManager.IsInRoleAsync(user, "SuperAdmin") || await _userManager.IsInRoleAsync(user, "sysadmin")))
            {
                var allMetas = await _controllerRepo.GetAllActiveAsync();

                var allPermissions = allMetas
                    .Where(m => !string.IsNullOrEmpty(m.PermissionKey))
                    .Select(m => m.PermissionKey!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                allPermissions.Add("sysadmin");
                allPermissions.Add("SuperAdmin");

                return allPermissions;
            }

            if (!activeLabId.HasValue)
            {
                var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken);
                var defaultLab = accessibleLabs.FirstOrDefault();
                if (defaultLab == null)
                {
                    return Enumerable.Empty<string>();
                }
                activeLabId = defaultLab.LabId;
            }

            var userPermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId.Value, cancellationToken);
            var permissionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var permString in userPermissions)
            {
                var parts = permString.Split(':');
                if (parts.Length >= 2 && long.TryParse(parts[1], out long mask))
                {
                    string module = parts[0];
                    for (int bit = 0; bit < 64; bit++)
                    {
                        if ((mask & (1L << bit)) != 0)
                        {
                            permissionKeys.Add($"{module}.Bit{bit}");
                        }
                    }
                }
                else
                {
                    permissionKeys.Add(permString);
                }
            }

            return permissionKeys;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取使用者權限清單時發生異常。UserId: {UserId}", userId);
            return Enumerable.Empty<string>();
        }
    }

    public async Task<bool> ValidateRuntimePermissionAsync(
        string userId,
        Guid activeLabId,
        string module,
        int bitPosition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(module);

        if (bitPosition is < 0 or > 63)
        {
            _logger.LogWarning("位元位置 {BitPosition} 超過 64 位元遮罩合法範圍 (0-63)", bitPosition);
            return false;
        }

        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && (await _userManager.IsInRoleAsync(user, "sysadmin") || await _userManager.IsInRoleAsync(user, "SuperAdmin")))
            {
                return true;
            }

            bool isLabScopeValid = await _orgIntegrationService.IsInUserScopeAsync(userId, activeLabId, cancellationToken);
            if (!isLabScopeValid)
            {
                _logger.LogWarning("使用者 {UserId} 嘗試存取越權實驗室 {LabId}", userId, activeLabId);
                return false;
            }

            var userPermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId, cancellationToken);
            var modulePermissionString = userPermissions.FirstOrDefault(p => p.StartsWith($"{module}:", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(modulePermissionString))
            {
                return false;
            }

            var parts = modulePermissionString.Split(':');
            if (parts.Length < 2 || !long.TryParse(parts[1], out long bitmask))
            {
                return false;
            }

            long targetBit = 1L << bitPosition;
            return (bitmask & targetBit) != 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證執行期權限發生異常. UserId: {UserId}, LabId: {LabId}, Module: {Module}", userId, activeLabId, module);
            return false;
        }
    }

    public async Task<UserPermissionProfileDto?> SwitchLaboratoryAsync(
        string userId,
        Guid targetLabId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        try
        {
            var orgTask = _orgIntegrationService.GetOrganizationByIdAsync(targetLabId, cancellationToken);
            var labsTask = GetAccessibleLabsAsync(userId, cancellationToken);

            await Task.WhenAll(orgTask, labsTask);

            var targetOrg = await orgTask;
            if (targetOrg is null) return null;

            if (!await IsAuthorizedToLabAsync(userId, targetLabId, cancellationToken))
            {
                _logger.LogWarning("使用者 {UserId} 嘗試存取未授權實驗室 {TargetLabId}", userId, targetLabId);
                return null;
            }

            var permissions = await GetCachedOrFetchPermissionsAsync(userId, targetLabId, cancellationToken);
            var menus = await _menuService.GetUserMenuAsync(permissions);

            return new UserPermissionProfileDto
            {
                UserId = userId,
                LabId = targetOrg.Id,
                LabName = targetOrg.Name,
                Permissions = permissions,
                Menus = menus
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切換實驗室上下文失敗。UserId: {UserId}, TargetLabId: {TargetLabId}", userId, targetLabId);
            return null;
        }
    }

    private async Task<bool> IsAuthorizedToLabAsync(string userId, Guid labId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null && (await _userManager.IsInRoleAsync(user, "sysadmin") || await _userManager.IsInRoleAsync(user, "SuperAdmin"))) return true;

        return await _orgIntegrationService.IsInUserScopeAsync(userId, labId, ct);
    }

    /// <summary>
    /// 獲取使用者可存取的實驗室清單 (完全由 IOrganizationIntegrationService 抽象層隔離)
    /// </summary>
    public async Task<List<AccessibleLabDto>> GetAccessibleLabsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            bool isSysAdmin = user != null && (await _userManager.IsInRoleAsync(user, "SuperAdmin") || await _userManager.IsInRoleAsync(user, "sysadmin"));

            // 1. 若為系統管理員，直接呼叫 Integration Service 專為管理員開放的跨模組介面
            if (isSysAdmin)
            {
                _logger.LogInformation("使用者 {UserId} 為系統管理員，透過 Integration Service 載入全系統區域實驗室...", userId);

                var allOrgs = await _orgIntegrationService.GetAllActiveOrganizationsAsync(cancellationToken);

                var allAdminLabs = allOrgs.Select((lab, index) => new AccessibleLabDto
                {
                    LabId = lab.Id,
                    LabName = lab.Name,
                    Path = lab.NodePathString,
                    HierarchyLevel = lab.HierarchyLevel,
                    IsPrimary = index == 0
                }).ToList();

                // 2. 備援防護
                if (!allAdminLabs.Any())
                {
                    allAdminLabs.Add(new AccessibleLabDto
                    {
                        LabId = Guid.Empty,
                        LabName = "全域管理員預設實驗室",
                        Path = "/GLOBAL",
                        HierarchyLevel = 0,
                        IsPrimary = true
                    });
                }

                return allAdminLabs;
            }

            // 3. 一般使用者流程
            var orgs = await _orgIntegrationService.GetUserAccessibleOrganizationsAsync(userId, cancellationToken);

            return orgs.Select(x => new AccessibleLabDto
            {
                LabId = x.Id,
                LabName = x.Name,
                Path = x.NodePathString,
                HierarchyLevel = x.HierarchyLevel
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取可存取實驗室清單時發生異常。UserId: {UserId}", userId);

            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && (await _userManager.IsInRoleAsync(user, "SuperAdmin") || await _userManager.IsInRoleAsync(user, "sysadmin")))
            {
                return new List<AccessibleLabDto>
                {
                    new()
                    {
                        LabId = Guid.Empty,
                        LabName = "全域管理員預設實驗室",
                        Path = "/GLOBAL",
                        HierarchyLevel = 0,
                        IsPrimary = true
                    }
                };
            }

            return new List<AccessibleLabDto>();
        }
    }

    #region Private Helper Methods

    private async Task<IEnumerable<string>> GetCachedOrFetchPermissionsAsync(
        string userId,
        Guid labId,
        CancellationToken cancellationToken)
    {
        string cacheKey = $"perm_scope:{userId}:{labId}";

        var permissions = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiration;

            var permissionDict = await FetchUserModulePermissionsFromDbAsync(userId, labId, cancellationToken);

            return permissionDict.Select(p => $"{p.Key}:{p.Value}").ToList();
        });

        return permissions ?? Enumerable.Empty<string>();
    }

    private async Task<Dictionary<string, long>> FetchUserModulePermissionsFromDbAsync(
        string userId,
        Guid labId,
        CancellationToken cancellationToken)
    {
        return await Task.FromResult(new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "ReportManagement", 15L },
            { "UserManagement", 3L },
            { "LabConfiguration", 1L }
        });
    }

    #endregion
}