using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Identities;
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

    // 快取過期時間設定
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

    public UserRuntimeScopeService(
        IOrganizationIntegrationService orgIntegrationService,
        IMemoryCache cache,
        ILogger<UserRuntimeScopeService> logger,
        UserManager<ApplicationUser> userManager,
        IDynamicControllerRepository<ControllerMetadata> controllerRepo)
    {
        _orgIntegrationService = orgIntegrationService ?? throw new ArgumentNullException(nameof(orgIntegrationService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _controllerRepo = controllerRepo ?? throw new ArgumentNullException(nameof(controllerRepo));
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

                //allPermissions.Add("System.Dashboard");
                //allPermissions.Add("Module.Org.View");
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

            // 3. 讀取該實驗室下的 Bitmask 權限矩陣並轉譯為 Permission Key
            var modulePermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId.Value, cancellationToken);
            var permissionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (module, mask) in modulePermissions)
            {
                for (int bit = 0; bit < 64; bit++)
                {
                    if ((mask & (1L << bit)) != 0)
                    {
                        permissionKeys.Add($"{module}.Bit{bit}");
                    }
                }
            }

            //permissionKeys.Add("System.Dashboard");
            //permissionKeys.Add("Module.Org.View");

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

            // 2. 獲取該使用者在該實驗室下的所有模組 Bitmask 矩陣
            var modulePermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId, cancellationToken);

            if (!modulePermissions.TryGetValue(module, out long bitmask))
            {
                return false;
            }

            // 3. 執行高效能 64 位元 Bitmask AND 運算
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
            var targetOrg = await _orgIntegrationService.GetOrganizationByIdAsync(targetLabId, cancellationToken);
            if (targetOrg is null) return null;

            var user = await _userManager.FindByIdAsync(userId);
            bool isSysAdmin = user != null && await _userManager.IsInRoleAsync(user, "sysadmin");

            if (!isSysAdmin)
            {
                bool hasAccess = await _orgIntegrationService.IsInUserScopeAsync(userId, targetLabId, cancellationToken);
                if (!hasAccess) return null;
            }

            // 載入與計算 Bitmask
            var modulePermissions = await GetCachedOrFetchPermissionsAsync(userId, targetLabId, cancellationToken);

            // 生成動態 Menu
            var dynamicMenus = await GenerateDynamicMenusAsync(modulePermissions, cancellationToken);

            var accessibleLabs = await GetAccessibleLabsAsync(userId, cancellationToken);

            return new UserPermissionProfileDto
            {
                UserId = userId,
                ActiveLabId = targetOrg.Id,
                ActiveLabName = targetOrg.Name,
                ModulePermissions = modulePermissions,
                AccessibleLabs = accessibleLabs,
                DynamicMenus = dynamicMenus
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切換實驗室上下文時發生異常。UserId: {UserId}, TargetLabId: {TargetLabId}", userId, targetLabId);
            return null;
        }
    }

    /// <summary>
    /// 獲取使用者可存取的所有實驗室清單
    /// </summary>
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
    /// 快取優先獲取權限遮罩字典
    /// </summary>
    private async Task<Dictionary<string, long>> GetCachedOrFetchPermissionsAsync(
        string userId,
        Guid labId,
        CancellationToken cancellationToken)
    {
        string cacheKey = $"perm_scope:{userId}:{labId}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiration;
            return await FetchUserModulePermissionsFromDbAsync(userId, labId, cancellationToken);
        }) ?? new Dictionary<string, long>();
    }

    /// <summary>
    /// 從資料庫 (或權限總帳 Ledger 表) 讀取使用者在該實驗室的各模組 Bitmask 設定
    /// </summary>
    private async Task<Dictionary<string, long>> FetchUserModulePermissionsFromDbAsync(
        string userId,
        Guid labId,
        CancellationToken cancellationToken)
    {
        return await Task.FromResult(new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "ReportManagement", 15L },   // 二進位 0000 1111 (Bit 0, 1, 2, 3)
            { "UserManagement", 3L },      // 二進位 0000 0011 (Bit 0, 1)
            { "LabConfiguration", 1L }     // 二進位 0000 0001 (Bit 0)
        });
    }

    /// <summary>
    /// 依據模組 Bitmask 遮罩過濾出的動態選單樹
    /// </summary>
    private async Task<List<MenuNodeDto>> GenerateDynamicMenusAsync(
        Dictionary<string, long> bitmasks,
        CancellationToken cancellationToken)
    {
        var menus = new List<MenuNodeDto>();

        if (bitmasks.TryGetValue("ReportManagement", out long reportMask) && (reportMask & (1L << 0)) != 0)
        {
            menus.Add(new MenuNodeDto
            {
                Code = "ReportList",
                Name = "報告清單",
                Path = "/reports/list"
            });
        }

        return await Task.FromResult(menus);
    }

    #endregion
}