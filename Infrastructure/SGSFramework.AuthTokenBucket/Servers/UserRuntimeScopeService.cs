using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs;

namespace SGSFramework.AuthTokenBucket.Servers;

public class UserRuntimeScopeService : IUserRuntimeScopeService
{
    private readonly IOrganizationIntegrationService _orgIntegrationService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UserRuntimeScopeService> _logger;

    // 快取過期時間設定 (可依需求調整或改自 appsettings.json)
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

    public UserRuntimeScopeService(
        IOrganizationIntegrationService orgIntegrationService,
        IMemoryCache cache,
        ILogger<UserRuntimeScopeService> logger)
    {
        _orgIntegrationService = orgIntegrationService ?? throw new ArgumentNullException(nameof(orgIntegrationService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            // 1. 跨模組驗證：確認使用者對該 ActiveLabId 具備合法權限 (樹狀 HierarchyId 範疇核對)
            bool isLabScopeValid = await _orgIntegrationService.IsInUserScopeAsync(userId, activeLabId, cancellationToken);
            if (!isLabScopeValid)
            {
                _logger.LogWarning("使用者 {UserId} 嘗試存取越權實驗室 {LabId}", userId, activeLabId);
                return false;
            }

            // 2. 獲取該使用者在該實驗室下的所有模組 Bitmask 矩陣 (搭配快取防禦，避免 DB 負載過高)
            var modulePermissions = await GetCachedOrFetchPermissionsAsync(userId, activeLabId, cancellationToken);

            if (!modulePermissions.TryGetValue(module, out long bitmask))
            {
                // 該模組未配置任何權限遮罩
                return false;
            }

            // 3. 執行高效能 64 位元 Bitmask 運算 (AND 位元交集)
            // 1L << bitPosition 代表將 1 移位至指定 Bit 位元
            long targetBit = 1L << bitPosition;
            bool hasPermission = (bitmask & targetBit) != 0;

            return hasPermission;
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

        var targetOrg = await _orgIntegrationService.GetOrganizationByIdAsync(targetLabId, cancellationToken);
        if (targetOrg is null) return null;

        bool hasAccess = await _orgIntegrationService.IsInUserScopeAsync(userId, targetLabId, cancellationToken);
        if (!hasAccess) return null;

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

    /// <summary>
    /// 獲取使用者可存取的所有實驗室清單
    /// </summary>
    public async Task<List<AccessibleLabDto>> GetAccessibleLabsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var orgs = await _orgIntegrationService.GetUserAccessibleOrganizationsAsync(userId, cancellationToken);

        return orgs.Select(x => new AccessibleLabDto
        {
            LabId = x.Id,
            LabName = x.Name,
            Path = x.NodePathString,
            HierarchyLevel = x.HierarchyLevel
        }).ToList();
    }

    #region Private Helper Methods

    /// <summary>
    /// 快取優先獲取權限遮罩字典 (Redis 或 MemoryCache)
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
    /// 從資料庫 (或權限總帳 Ledger 表) 讀取使用者在該實驗室的各模組 Bitmask 原始設定
    /// </summary>
    private async Task<Dictionary<string, long>> FetchUserModulePermissionsFromDbAsync(
        string userId,
        Guid labId,
        CancellationToken cancellationToken)
    {
        // 實務上請換成 EF Core 查詢 UserPermissions 表
        // 範例回傳 mock 資料：
        return await Task.FromResult(new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "ReportManagement", 15L },   // 二進位 0000 1111 (擁有 Bit 0, 1, 2, 3)
            { "UserManagement", 3L },      // 二進位 0000 0011 (擁有 Bit 0, 1)
            { "LabConfiguration", 1L }     // 二進位 0000 0001 (擁有 Bit 0)
        });
    }

    /// <summary>
    /// 依據模組 Bitmask 遮罩過濾出的動態選單樹
    /// </summary>
    private async Task<List<MenuNodeDto>> GenerateDynamicMenusAsync(
        Dictionary<string, long> bitmasks,
        CancellationToken cancellationToken)
    {
        // 可根據傳入的 bitmasks 動態組裝，此處示範回傳基本選單結構
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