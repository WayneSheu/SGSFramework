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

namespace SGSFramework.AuthTokenBucket.Servers;

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
    // 快取過期時間設定
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

    public UserRuntimeScopeService(
        IOrganizationIntegrationService orgIntegrationService,
        IMemoryCache cache,
        ILogger<UserRuntimeScopeService> logger,
        UserManager<ApplicationUser> userManager,
        IDynamicControllerRepository<ControllerMetadata> controllerRepo,
        IDynamicMenuService menuService
        )
    {
        _orgIntegrationService = orgIntegrationService ?? throw new ArgumentNullException(nameof(orgIntegrationService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _controllerRepo = controllerRepo ?? throw new ArgumentNullException(nameof(controllerRepo));
        _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
    }

    /// <summary>
    /// 初始化使用者的執行期上下文，包含可存取的實驗室、權限清單與動態選單。
    /// </summary>
    public async Task<UserPermissionProfileDto> InitializeUserScopeAsync(
    string userId,
    string? requestedLabId,
    CancellationToken cancellationToken)
    {
        // 1. 取得使用者有權存取的所有實驗室
        var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken);

        // 2. 判斷目標 LabId 是否有效 (若為空則取第一筆或預設)
        var targetLab = accessibleLabs.FirstOrDefault(l => l.LabId.ToString() == requestedLabId)
                     ?? accessibleLabs.FirstOrDefault();

        if (targetLab == null) throw new UnauthorizedAccessException("無權存取任何實驗室。");

        // 3. 獲取該 Lab 的權限與選單資訊
        var permissions = await GetUserPermissionsAsync(userId, targetLab.LabId, cancellationToken);
        var menuTree = await _menuService.GetUserMenuAsync(permissions);

        // 4. 回傳封裝後的 Profile
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
    /// 獲取使用者在當前上下文/實驗室下持有的所有權限 Key 集合 (包含 sysadmin 特權過濾)
    /// </summary>
    public async Task<IEnumerable<string>> GetUserPermissionsAsync(
        string userId,
        Guid? activeLabId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        try
        {
            // 1. 特權判斷：若使用者具備 sysadmin 角色，回傳全系統註冊之所有 PermissionKey
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && await _userManager.IsInRoleAsync(user, "SuperAdmin"))
            {
                var allMetas = await _controllerRepo.GetAllActiveAsync();

                var allPermissions = allMetas
                    .Where(m => !string.IsNullOrEmpty(m.PermissionKey))
                    .Select(m => m.PermissionKey!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                allPermissions.Add("sysadmin");

                return allPermissions;
            }

            // 2. 一般使用者：確定當前作用中的實驗室
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

            // 3. 讀取該實驗室下的權限 Key 集合 (此處回傳的是 IEnumerable<string>，內含 "Module:Mask" 或對應格式)
            var userPermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId.Value, cancellationToken);
            var permissionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var permString in userPermissions)
            {
                // 解析 "Module:Mask" 格式
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
                    // 若本身就是純 Permission Key 字串則直接加入
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

    /// <summary>
    /// 後端 API 執行期驗證：檢查使用者在指定實驗室下，是否具備特定模組的指定位元權限
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
            _logger.LogWarning("位元位置 {BitPosition} 超過 64 位元遮罩合法範圍 (0-63)", bitPosition);
            return false;
        }

        try
        {
            // 特權判斷：sysadmin 直通
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && await _userManager.IsInRoleAsync(user, "sysadmin"))
            {
                return true;
            }

            // 1. 跨模組驗證：確認使用者對該 ActiveLabId 具備合法權限
            bool isLabScopeValid = await _orgIntegrationService.IsInUserScopeAsync(userId, activeLabId, cancellationToken);
            if (!isLabScopeValid)
            {
                _logger.LogWarning("使用者 {UserId} 嘗試存取越權實驗室 {LabId}", userId, activeLabId);
                return false;
            }

            // 2. 獲取該使用者在該實驗室下的所有權限集合
            var userPermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId, cancellationToken);

            // 3. 找出該模組對應的權限字串 (格式預設為 "ModuleName:BitmaskValue")
            var modulePermissionString = userPermissions.FirstOrDefault(p => p.StartsWith($"{module}:", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(modulePermissionString))
            {
                return false;
            }

            // 4. 解析 Bitmask 字串
            var parts = modulePermissionString.Split(':');
            if (parts.Length < 2 || !long.TryParse(parts[1], out long bitmask))
            {
                return false;
            }

            // 5. 執行高效能 64 位元 Bitmask AND 運算
            long targetBit = 1L << bitPosition;
            return (bitmask & targetBit) != 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證執行期權限發生異常. UserId: {UserId}, LabId: {LabId}, Module: {Module}", userId, activeLabId, module);
            return false;
        }
    }

    /// <summary>
    /// 高階主管無感切換實驗室上下文
    /// </summary>
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
        if (user != null && await _userManager.IsInRoleAsync(user, "sysadmin")) return true;

        return await _orgIntegrationService.IsInUserScopeAsync(userId, labId, ct);
    }

    /// <summary>
    /// 獲取使用者可存取的實驗室清單
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<AccessibleLabDto>> GetAccessibleLabsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        try
        {
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
            return new List<AccessibleLabDto>();
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// 快取優先獲取權限 Key 集合
    /// </summary>
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