// ==========================================
// 檔案路徑: src/SGSFramework/Infrastructure/SGSFramework.AuthTokenBucket/Services/UserRuntimeScopeService.cs
// 架構層級: Infrastructure Layer / Services
// ==========================================

namespace SGSFramework.AuthTokenBucket.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.Controllers.Services;
using SGSFramework.Core.DTOs;

/// <summary>
/// 多租戶與動態實驗室（Lab/Department/Organization）架構下的核心執行期上下文服務。
/// 提供 3-Tier 實驗室決策矩陣、無感切換與自我修復退路機制。
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
    private const string SystemAdminRole = "sysadmin";
    private const string SuperAdminRole = "SuperAdmin";

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

    /// <summary>
    /// 初始化使用者執行期實驗室作用域 (3-Tier 決策機制: Requested -> Primary -> FirstAvailable)
    /// </summary>
    public async Task<UserPermissionProfileDto> InitializeUserScopeAsync(
        string userId,
        string? requestedLabId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken);
        if (accessibleLabs == null || !accessibleLabs.Any())
        {
            _logger.LogWarning("[Auth-Scope-Denied] 使用者未授權存取任何啟用的實驗室。UserId: {UserId}, RequestedLabId: {LabId}", userId, requestedLabId);
            throw new UnauthorizedAccessException("您未獲得任何實驗室的存取權限。");
        }

        AccessibleLabDto targetLab = ResolveTargetLab(accessibleLabs, requestedLabId);

        _logger.LogInformation("[Auth-Scope-Resolved] 已成功鎖定執行期實驗室上下文。UserId: {UserId}, TargetLabId: {LabId}, IsPrimary: {IsPrimary}",
            userId, targetLab.LabId, targetLab.IsPrimary);

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

    /// <summary>
    /// 切換作用中實驗室上下文 (具備自我修復退路與警告通知機制)
    /// </summary>
    public async Task<SwitchLabResultDto> SwitchLaboratoryWithFallbackAsync(
        string userId,
        Guid? targetLabId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken);
        if (accessibleLabs == null || !accessibleLabs.Any())
        {
            _logger.LogWarning("[ScopeSwitch-Denied] 使用者無可存取實驗室。UserId: {UserId}", userId);
            throw new UnauthorizedAccessException("您未獲配任何實驗室存取權限，無法執行切換。");
        }

        bool isTargetValid = targetLabId.HasValue
            && targetLabId.Value != Guid.Empty
            && accessibleLabs.Any(l => l.LabId == targetLabId.Value);

        Guid finalLabId;
        bool isFallback = false;
        string? warningMessage = null;

        if (isTargetValid)
        {
            finalLabId = targetLabId!.Value;
        }
        else
        {
            // 自動退路機制：優先選擇 IsPrimary == true，次之選擇第一個可用實驗室
            var primaryLab = accessibleLabs.FirstOrDefault(l => l.IsPrimary) ?? accessibleLabs.First();
            finalLabId = primaryLab.LabId;
            isFallback = true;

            warningMessage = targetLabId.HasValue && targetLabId.Value != Guid.Empty
                ? $"您無權存取指定的實驗室，系統已自動切換至主要實驗室：{primaryLab.LabName}。"
                : $"未指定有效的實驗室，系統已自動切換至主要實驗室：{primaryLab.LabName}。";

            _logger.LogWarning("[ScopeSwitch-Fallback] 使用者 {UserId} 請求無效或未授權之實驗室 {TargetLabId}，已自動降級切換至 {FinalLabId}",
                userId, targetLabId, finalLabId);
        }

        var permissions = await GetCachedOrFetchPermissionsAsync(userId, finalLabId, cancellationToken);
        var menuTree = await _menuService.GetUserMenuAsync(permissions);
        var targetLabInfo = accessibleLabs.First(l => l.LabId == finalLabId);

        var profile = new UserPermissionProfileDto
        {
            UserId = userId,
            LabId = finalLabId,
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
            var result = await SwitchLaboratoryWithFallbackAsync(userId, targetLabId, cancellationToken);
            return result.Profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切換實驗室上下文發生未預期異常。UserId: {UserId}, TargetLabId: {TargetLabId}", userId, targetLabId);
            return null;
        }
    }

    /// <summary>
    /// 獲取使用者在特定實驗室下的最終權限 Key 集合
    /// </summary>
    public async Task<IEnumerable<string>> GetUserPermissionsAsync(
        string userId,
        Guid? activeLabId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Enumerable.Empty<string>();

            if (await IsSystemAdminAsync(user))
            {
                var allMetas = await _controllerRepo.GetAllActiveAsync();
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
                var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken);
                var defaultLab = accessibleLabs.FirstOrDefault(l => l.IsPrimary) ?? accessibleLabs.FirstOrDefault();
                if (defaultLab == null) return Enumerable.Empty<string>();

                activeLabId = defaultLab.LabId;
            }

            var rawPermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId.Value, cancellationToken);
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
            _logger.LogError(ex, "獲取使用者權限清單時發生異常。UserId: {UserId}, ActiveLabId: {LabId}", userId, activeLabId);
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// 驗證使用者於特定實驗室下的特定模組與 Bitmask 權限點
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

        if (bitPosition is < 0 or > 63)
        {
            _logger.LogWarning("權限 Bit 位元位置 {BitPosition} 超過 64 位元長整型邊界 (0-63)。", bitPosition);
            return false;
        }

        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            if (await IsSystemAdminAsync(user)) return true;

            bool isLabScopeValid = await _orgIntegrationService.IsInUserScopeAsync(userId, activeLabId, cancellationToken);
            if (!isLabScopeValid)
            {
                _logger.LogWarning("使用者 {UserId} 嘗試越權存取未授權實驗室 {LabId}", userId, activeLabId);
                return false;
            }

            var userPermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId, cancellationToken);
            var modulePerm = userPermissions.FirstOrDefault(p => p.StartsWith($"{module}:", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(modulePerm)) return false;

            var parts = modulePerm.Split(':');
            if (parts.Length < 2 || !long.TryParse(parts[1], out long bitmask)) return false;

            return (bitmask & (1L << bitPosition)) != 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證執行期權限發生異常。UserId: {UserId}, LabId: {LabId}, Module: {Module}", userId, activeLabId, module);
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
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return new List<AccessibleLabDto>();

            if (await IsSystemAdminAsync(user))
            {
                var allOrgs = await _orgIntegrationService.GetAllActiveOrganizationsAsync(cancellationToken);

                var adminLabs = allOrgs.Select((lab, index) => new AccessibleLabDto
                {
                    LabId = lab.Id,
                    LabName = lab.Name,
                    Path = lab.NodePathString,
                    HierarchyLevel = lab.HierarchyLevel,
                    IsPrimary = index == 0 // 管理員預設鎖定第一個 Active 實驗室為主節點
                }).ToList();

                if (!adminLabs.Any())
                {
                    adminLabs.Add(CreateFallbackAdminLab());
                }

                return adminLabs;
            }

            var orgs = await _orgIntegrationService.GetUserAccessibleOrganizationsAsync(userId, cancellationToken);

            return orgs.Select(x => new AccessibleLabDto
            {
                LabId = x.Id,
                LabName = x.Name,
                Path = x.NodePathString,
                HierarchyLevel = x.HierarchyLevel,
                IsPrimary = x.IsPrimary
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取可存取實驗室清單時發生異常。UserId: {UserId}", userId);
            return new List<AccessibleLabDto>();
        }
    }

    #region Private Helper Methods

    private static AccessibleLabDto ResolveTargetLab(List<AccessibleLabDto> accessibleLabs, string? requestedLabId)
    {
        AccessibleLabDto? target = null;

        if (!string.IsNullOrWhiteSpace(requestedLabId))
        {
            target = accessibleLabs.FirstOrDefault(l =>
                l.LabId.ToString().Equals(requestedLabId, StringComparison.OrdinalIgnoreCase));
        }

        // 優先取 IsPrimary == true，次之取第一個可用的啟用實驗室
        target ??= accessibleLabs.FirstOrDefault(l => l.IsPrimary);
        target ??= accessibleLabs.FirstOrDefault();

        return target ?? throw new UnauthorizedAccessException("無法解析有效的實驗室節點。");
    }

    private async Task<bool> IsSystemAdminAsync(ApplicationUser? user)
    {
        if (user == null) return false;

        return await _userManager.IsInRoleAsync(user, SuperAdminRole) ||
               await _userManager.IsInRoleAsync(user, SystemAdminRole);
    }

    private static AccessibleLabDto CreateFallbackAdminLab()
    {
        return new AccessibleLabDto
        {
            LabId = Guid.Empty,
            LabName = "全域管理員預設實驗室",
            Path = "/GLOBAL",
            HierarchyLevel = 0,
            IsPrimary = true
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
            var permissionDict = await FetchUserModulePermissionsFromDbAsync(userId, labId, cancellationToken);
            return permissionDict.Select(p => $"{p.Key}:{p.Value}").ToList();
        });

        return permissions ?? Enumerable.Empty<string>();
    }

    private Task<Dictionary<string, long>> FetchUserModulePermissionsFromDbAsync(
        string userId,
        Guid labId,
        CancellationToken cancellationToken)
    {
        var mockPermissions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "ReportManagement", 15L },
            { "UserManagement", 3L },
            { "LabConfiguration", 1L }
        };

        return Task.FromResult(mockPermissions);
    }

    #endregion
}