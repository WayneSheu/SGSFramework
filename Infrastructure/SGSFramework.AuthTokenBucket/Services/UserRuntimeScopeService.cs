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
public class UserRuntimeScopeService(
    IOrganizationIntegrationService orgIntegrationService,
    IMemoryCache cache,
    ILogger<UserRuntimeScopeService> logger,
    UserManager<ApplicationUser> userManager,
    IDynamicControllerRepository<ControllerMetadata> controllerRepo,
    IDynamicMenuService menuService)
    : IUserRuntimeScopeService
{
    private readonly IOrganizationIntegrationService _orgIntegrationService = orgIntegrationService ?? throw new ArgumentNullException(nameof(orgIntegrationService));
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly ILogger<UserRuntimeScopeService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly UserManager<ApplicationUser> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    private readonly IDynamicControllerRepository<ControllerMetadata> _controllerRepo = controllerRepo ?? throw new ArgumentNullException(nameof(controllerRepo));
    private readonly IDynamicMenuService _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));

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
    /// 取得使用者的預設主實驗室 TenantLabId (由 user_lab_mappings 中的 isPrimary 判斷)[cite: 8]
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
    /// 切換作用中實驗室上下文 (具備自我修復退路與警告通知機制)[cite: 8]
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
    /// 舊有相容介面切換實作 (內部轉呼叫降級機制)[cite: 8]
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
    /// 獲取使用者在特定實驗室下的最終權限 Key 集合 (支援全域/組織級權限與實驗室層級隔離)[cite: 8]
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

            // 1. 若 activeLabId 為 null（代表組織/全域層級請求），僅撈取不綁定實驗室的全域/組織級權限
            if (!activeLabId.HasValue)
            {
                var globalRawPermissions = await GetCachedOrFetchGlobalPermissionsAsync(userId, cancellationToken).ConfigureAwait(false);
                return ParsePermissionKeys(globalRawPermissions);
            }

            // 2. 若帶有特定實驗室 ID，需確保該實驗室在使用者可存取清單或其父組織鏈路範圍內
            var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken).ConfigureAwait(false);
            var targetLab = accessibleLabs.FirstOrDefault(l => l.TenantLabId == activeLabId.Value);

            if (targetLab == null)
            {
                _logger.LogWarning("[PermissionCheck] 使用者 {UserId} 嘗試查詢未授權之實驗室範圍權限: {LabId}", userId, activeLabId);
                return [];
            }

            // 3. 組合全域權限 + 該實驗室範圍權限 (包含符合母子組織繼承或指定實驗室的權限集)
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
    /// 驗證使用者於特定實驗室下的特定模組與 Bitmask 權限點[cite: 8]
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
            var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
            if (user == null) return false;

            if (await IsSystemAdminAsync(user).ConfigureAwait(false)) return true;

            bool isLabScopeValid = await _orgIntegrationService.IsInUserScopeAsync(userId, activeLabId, cancellationToken).ConfigureAwait(false);
            if (!isLabScopeValid)
            {
                _logger.LogWarning("使用者 {UserId} 嘗試越權存取未授權實驗室 {LabId}", userId, activeLabId);
                return false;
            }

            var userPermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId, cancellationToken).ConfigureAwait(false);
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
    /// 獲取使用者可存取的實驗室清單 (整合 SystemAdmin 與一般使用者對映，強型別補齊所有 Required 屬性)[cite: 8]
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
    /// 解析最終作用中實驗室 (Requested -> Primary -> FirstAvailable)
    /// </summary>
    /// <param name="accessibleLabs"></param>
    /// <param name="requestedLabId"></param>
    /// <returns></returns>
    /// <exception cref="UnauthorizedAccessException"></exception>
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

    /// <summary>
    /// 嘗試從快取中取得使用者在特定實驗室下的權限，若不存在則從資料庫撈取並快取結果  
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="labId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
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
    /// 從資料庫中取得使用者在特定實驗室下的模組權限點對應 (實際專案中請結合使用者角色與 labId 查詢)
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="labId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private Task<Dictionary<string, long>> FetchUserModulePermissionsFromDbAsync(
        string userId,
        Guid labId,
        CancellationToken cancellationToken)
    {
        // 實際專案中請在此處實作：結合使用者角色權限、指定 labId 及其父組織層級 (Parent TenantLabId) 的資料庫查詢邏輯
        var mockPermissions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "ReportManagement", 15L },
            { "UserManagement", 3L },
            { "LabConfiguration", 1L },
            { "ORG", 7L } // 包含 ORG_LAB_READ 等位元遮罩或對應權限
        };

        return Task.FromResult(mockPermissions);
    }

    private Task<Dictionary<string, long>> FetchUserGlobalPermissionsFromDbAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        // 實際專案中請在此處實作：僅查詢 LabId 為 null (全域/組織級) 的權限點對應
        var mockGlobalPermissions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "ORG", 4L }, // 對應 ORG_LAB_READ 或全域組織管理權限
            { "SYSTEM.ROLEMANAGEMENT", 3L }
        };

        return Task.FromResult(mockGlobalPermissions);
    }

    private static HashSet<string> ParsePermissionKeys(IEnumerable<string> rawPermissions)
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
                        permissionKeys.Add($"{module}.Bit{bit}");
                    }
                }
            }
            else
            {
                permissionKeys.Add(permString);
            }
        }

        // 同時也將原始字串加入（相容字串型態的 PermissionKey，例如 "ORG_LAB_READ"）
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