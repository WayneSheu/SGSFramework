#nullable enable

namespace SGSFramework.AuthTokenBucket.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionAudits;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionGrants;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionTree;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionUsers;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.RolePermissions;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.UserPermissions;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using SGSFramework.Core.Abstractions.Permissions.Repositories;
using SGSFramework.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class PermissionManagementService<TDbContext> : IPermissionManagementService
    where TDbContext : DbContext, ITokenDbContext
{
    private readonly TDbContext _dbContext;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IUserPermissionRepository _userPermissionRepository;
    private readonly IUserLabRepository _userLabRepository;
    private readonly IOrganizationIntegrationService _orgIntegrationService;
    private readonly IPermissionRegistry _permissionRegistry;
    private readonly IPermissionBitmaskService _bitmaskService;
    private readonly IPermissionChangeNotifier _permissionChangeNotifier;
    private readonly IRoleAuditService _roleAuditService;
    private readonly ILabFunctionScopeService _labFunctionScopeService;
    private readonly ILabAuditService _labAuditService;
    private readonly IModuleStatusReader _moduleStatusReader;
    private readonly ILogger<PermissionManagementService<TDbContext>> _logger;

    public PermissionManagementService(
        TDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IRolePermissionRepository rolePermissionRepository,
        IUserPermissionRepository userPermissionRepository,
        IOrganizationIntegrationService orgIntegrationService,
        IUserLabRepository userLabRepository,
        IPermissionRegistry permissionRegistry,
        IPermissionBitmaskService bitmaskService,
        IPermissionChangeNotifier permissionChangeNotifier,
        IRoleAuditService roleAuditService,
        ILabFunctionScopeService labFunctionScopeService,
        ILabAuditService labAuditService,
        IModuleStatusReader moduleStatusReader,
        ILogger<PermissionManagementService<TDbContext>> logger)
    {
        _moduleStatusReader = moduleStatusReader ?? throw new ArgumentNullException(nameof(moduleStatusReader));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _rolePermissionRepository = rolePermissionRepository ?? throw new ArgumentNullException(nameof(rolePermissionRepository));
        _userPermissionRepository = userPermissionRepository ?? throw new ArgumentNullException(nameof(userPermissionRepository));
        _orgIntegrationService = orgIntegrationService ?? throw new ArgumentNullException(nameof(orgIntegrationService));
        _userLabRepository = userLabRepository ?? throw new ArgumentNullException(nameof(userLabRepository));
        _permissionRegistry= permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
        _bitmaskService = bitmaskService ?? throw new ArgumentNullException(nameof(bitmaskService));
        _permissionChangeNotifier = permissionChangeNotifier ?? throw new ArgumentNullException(nameof(permissionChangeNotifier));
        _roleAuditService = roleAuditService ?? throw new ArgumentNullException(nameof(roleAuditService));
        _labFunctionScopeService = labFunctionScopeService ?? throw new ArgumentNullException(nameof(labFunctionScopeService));
        _labAuditService = labAuditService ?? throw new ArgumentNullException(nameof(labAuditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> HasPermissionAsync(string userId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        return await Task.FromResult(true).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task GrantPermissionToRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RevokePermissionFromRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<PermissionMetadataDto>> SyncPermissionMetadataAsync(CancellationToken cancellationToken = default)
    {
        var metadataList = await _dbContext.Set<PermissionMetadata>()
            .AsNoTracking()
            .Select(m => new PermissionMetadataDto(
                m.Id,
                m.ModuleName ?? string.Empty,
                m.ModuleTitle ?? string.Empty,
                m.ControllerName ?? string.Empty,
                m.ControllerTitle ?? string.Empty,
                m.ActionName ?? string.Empty,
                m.ActionTitle ?? string.Empty,
                m.PermissionKey ?? string.Empty,
                m.PermissionTitle ?? string.Empty,
                m.Description ?? string.Empty,
                m.BitPosition
            ))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return metadataList.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<List<PermissionModuleDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default)
        => (await BuildPermissionTreesAsync(cancellationToken).ConfigureAwait(false)).Legacy;

    /// <inheritdoc />
    public async Task<List<PermissionModuleDto>> GetPermissionMenuTreeAsync(CancellationToken cancellationToken = default)
        => (await BuildPermissionTreesAsync(cancellationToken).ConfigureAwait(false)).Menu;

    /// <inheritdoc />
    public Task<PermissionTreeSet> GetPermissionTreeSetAsync(CancellationToken cancellationToken = default)
        => BuildPermissionTreesAsync(cancellationToken);

    private async Task<PermissionTreeSet> BuildPermissionTreesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 權限鍵值、位元與權限標題的自訂仍以 PermissionMetadata 為準；
            // 模組 / 功能 / 動作名稱與說明改取自 ControllerMetadata（含「系統功能管理」的自訂名稱），各畫面顯示一致
            var metadataList = await _dbContext.Set<PermissionMetadata>()
                .AsNoTracking()
                .OrderBy(m => m.PermissionKey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (metadataList.Count == 0)
            {
                return new PermissionTreeSet(new List<PermissionModuleDto>(), new List<PermissionModuleDto>());
            }

            var controllerMetadatas = await _dbContext.Set<ControllerMetadata>()
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var overrides = await _dbContext.Set<FunctionGroupOverride>()
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var resolver = new FunctionGroupResolver(controllerMetadatas, overrides);
            // 與側邊選單同源：模組 = 選單區段、功能 = 選單頁面，排序與標題依選單樹
            var menu = await BuildMenuLookupAsync(cancellationToken).ConfigureAwait(false);
            var actionLookup = controllerMetadatas
                .GroupBy(c => FunctionStatusRules.BuildKey(c.ControllerName, c.ActionName), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            string ModuleTitleOf(PermissionMetadata p) =>
                resolver.FindModuleTitle(p.ModuleName) ?? p.ModuleTitle ?? p.ModuleName;

            string ControllerTitleOf(PermissionMetadata p) =>
                resolver.FindControllerTitle(p.ModuleName, p.ControllerName) ?? p.ControllerTitle ?? p.ControllerName;

            PermissionActionDto ToAction(PermissionMetadata p, bool isRead, string controllerTitle)
            {
                actionLookup.TryGetValue(FunctionStatusRules.BuildKey(p.ControllerName, p.ActionName), out var action);
                var actionTitle = !string.IsNullOrWhiteSpace(action?.DisplayName) ? action.DisplayName
                    : !string.IsNullOrWhiteSpace(p.ActionTitle) ? p.ActionTitle
                    : p.PermissionTitle;

                return new PermissionActionDto
                {
                    PermissionKey = p.PermissionKey ?? string.Empty,
                    // 未於權限清單自訂的「讀取」權限，名稱跟隨功能 (Controller) 名稱
                    PermissionTitle = !p.IsCustomized && isRead ? controllerTitle : p.PermissionTitle,
                    ActionName = p.ActionName,
                    ActionTitle = actionTitle,
                    Description = p.IsCustomized || string.IsNullOrWhiteSpace(action?.Description) ? p.Description : action.Description,
                    BitPosition = p.BitPosition,
                    IsCustomized = p.IsCustomized
                };
            }

            var result = new List<PermissionModuleDto>();
            // 選單檢視用：功能的實際模組與選單位置、各模組的主要選單群組
            var functionInfo = new Dictionary<PermissionFunctionDto, (string Module, MenuPlacement? Place)>();
            var moduleHomes = new Dictionary<string, MenuPlacement?>(StringComparer.OrdinalIgnoreCase);
            var moduleGroups = metadataList
                .GroupBy(m => m.ModuleName ?? string.Empty)
                .Select(g => new
                {
                    Group = g,
                    // 模組所在的選單群組：取功能最多者（同數量取選單排序最前）；模組內其他選單群組的功能以 GroupTitle 標示
                    Section = g.Select(m => menu.Find(g.Key, m.ControllerName ?? string.Empty))
                        .Where(pl => pl is not null)
                        .GroupBy(pl => pl!.TopKey)
                        .OrderByDescending(x => x.Count())
                        .ThenBy(x => x.First()!.SectionOrder)
                        .ThenBy(x => x.First()!.GroupOrder)
                        .Select(x => x.First())
                        .FirstOrDefault()
                })
                .OrderBy(x => x.Section?.SectionOrder ?? resolver.ModuleOrder(x.Group.Key) ?? int.MaxValue)
                .ThenBy(x => x.Section?.GroupOrder ?? int.MaxValue)
                .ThenBy(x => x.Section?.GroupTitle ?? ModuleTitleOf(x.Group.First()), StringComparer.CurrentCulture)
                .ToList();

            foreach (var moduleEntry in moduleGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var moduleGroup = moduleEntry.Group;
                moduleHomes[moduleGroup.Key] = moduleEntry.Section;

                var moduleDto = new PermissionModuleDto
                {
                    ModuleName = moduleGroup.Key,
                    // 模組標題 = 選單群組名稱（區段下第一層，例如 系統管理）；領域類另存 SectionTitle
                    ModuleTitle = moduleEntry.Section?.GroupTitle ?? moduleEntry.Section?.SectionTitle ?? ModuleTitleOf(moduleGroup.First()),
                    SectionTitle = moduleEntry.Section?.SectionTitle,
                    MenuItemCount = moduleEntry.Section?.TopChildCount,
                    Description = moduleGroup.FirstOrDefault()?.Description,
                    Functions = new List<PermissionFunctionDto>()
                };

                // 功能排序：選單「群組/頁面」順序 → 頁面順序；不在選單中的功能排最後（依功能自訂順序）
                var controllerGroups = moduleGroup
                    .GroupBy(c => c.ControllerName ?? string.Empty)
                    .OrderBy(g => menu.Find(moduleGroup.Key, g.Key)?.GroupOrder ?? int.MaxValue)
                    .ThenBy(g => menu.Find(moduleGroup.Key, g.Key)?.PageOrder ?? int.MaxValue)
                    .ThenBy(g => resolver.EffectiveControllerOrder(moduleGroup.Key, g.Key))
                    .ThenBy(g => ControllerTitleOf(g.First()), StringComparer.CurrentCulture);

                foreach (var controllerGroup in controllerGroups)
                {
                    var first = controllerGroup.First();
                    var controllerTitle = ControllerTitleOf(first);
                    var functionDto = new PermissionFunctionDto
                    {
                        FunctionName = controllerGroup.Key,
                        FunctionTitle = controllerTitle,
                        ModuleName = moduleGroup.Key,
                        GroupTitle = FunctionGroupTitle(menu.Find(moduleGroup.Key, controllerGroup.Key), moduleEntry.Section),
                        GroupItemCount = FunctionGroupItemCount(menu.Find(moduleGroup.Key, controllerGroup.Key), moduleEntry.Section),
                        Description = first.Description,
                        ActionPermissions = new List<PermissionActionDto>()
                    };

                    // 讀取權限判斷沿用 PermissionMetadata 原始標題（程式碼預設讀取權限標題與 Controller 標題相同）
                    var readEntity = controllerGroup.FirstOrDefault(a =>
                        (!string.IsNullOrEmpty(a.PermissionKey) && a.PermissionKey.EndsWith(".READ", StringComparison.OrdinalIgnoreCase)) ||
                        string.Equals(a.PermissionTitle, first.ControllerTitle, StringComparison.OrdinalIgnoreCase));

                    if (readEntity != null)
                    {
                        functionDto.ReadPermission = ToAction(readEntity, isRead: true, controllerTitle);
                    }

                    foreach (var actionEntity in controllerGroup.DistinctBy(x => x.PermissionKey))
                    {
                        if (readEntity != null && actionEntity.PermissionKey == readEntity.PermissionKey)
                        {
                            continue;
                        }

                        functionDto.ActionPermissions.Add(ToAction(actionEntity, isRead: false, controllerTitle));
                    }

                    functionInfo[functionDto] = (moduleGroup.Key, menu.Find(moduleGroup.Key, controllerGroup.Key));
                    moduleDto.Functions.Add(functionDto);
                }

                result.Add(moduleDto);
            }

            // 選單頁面：IsMenu = true 的功能（每個頁面一列）；同一權限鍵的多個入口只算一頁
            var pagesByController = controllerMetadatas
                .Where(c => c.IsMenu && !string.IsNullOrWhiteSpace(c.PermissionKey))
                .GroupBy(c => c.ControllerName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(c => c.DisplayOrder).ThenBy(c => c.ActionName, StringComparer.Ordinal).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            return new PermissionTreeSet(result, BuildMenuView(result, functionInfo, moduleHomes, pagesByController));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionManagementService] 建立權限樹狀圖時發生錯誤");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PermissionActionDto> UpdatePermissionMetadataAsync(
        string permissionKey,
        string controllerName,
        string permissionTitle,
        string? description,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(controllerName);

        var title = permissionTitle?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new ArgumentException("權限標題不可為空白。", nameof(permissionTitle));
        }

        if (title.Length > 128)
        {
            throw new ArgumentException("權限標題長度不可超過 128 字。", nameof(permissionTitle));
        }

        var desc = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        if (desc.Length > 256)
        {
            throw new ArgumentException("說明長度不可超過 256 字。", nameof(description));
        }

        var rows = await _dbContext.Set<PermissionMetadata>()
            .Where(p => p.PermissionKey == permissionKey && p.ControllerName == controllerName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            throw new KeyNotFoundException($"找不到權限 '{permissionKey}' (控制器 '{controllerName}')。");
        }

        foreach (var row in rows)
        {
            // 動作標題 (ActionTitle) 取自程式碼不可修改，只更新權限標題與說明
            row.PermissionTitle = title;
            row.Description = desc;
            // 標記為已自訂：啟動同步不再以程式碼的值覆蓋權限標題與說明
            row.IsCustomized = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var first = rows[0];
        return new PermissionActionDto
        {
            PermissionKey = first.PermissionKey,
            PermissionTitle = first.PermissionTitle,
            ActionName = first.ActionName,
            ActionTitle = first.ActionTitle,
            Description = first.Description,
            BitPosition = first.BitPosition,
            IsCustomized = first.IsCustomized
        };
    }

    /// <inheritdoc />
    public async Task<RolePermissionMatrixDto?> GetRoleGlobalPermissionsAsync(string roleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

        try
        {
            var rolePermissions = await _dbContext.Set<RoleGlobalPermission>()
                .AsNoTracking()
                .Where(r => r.RoleId == roleId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (rolePermissions.Count == 0)
            {
                return new RolePermissionMatrixDto
                {
                    RoleId = roleId,
                    GrantedPermissionKeys = new List<string>()
                };
            }

            var dbBitmaskMap = rolePermissions
                .Select(x => new { x.PermissionKey, x.Bitmask })
                .ToDictionary(
                    x => x.PermissionKey,
                    x => x.Bitmask,
                    StringComparer.OrdinalIgnoreCase);

            var decodedDirectPermissions = await _bitmaskService
                .DecodeBitmaskToPermissionsAsync(dbBitmaskMap, cancellationToken)
                .ConfigureAwait(false);

            return new RolePermissionMatrixDto
            {
                RoleId = roleId,
                GrantedPermissionKeys = decodedDirectPermissions
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PermissionManagementService] 查詢角色 [{RoleId}] 權限作業已取消。", roleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionManagementService] 查詢角色 [{RoleId}] 權限矩陣時發生未預期異常。", roleId);
            throw;
        }
    }

    /// <summary>將「模組鍵:位元遮罩」展開為完整權限鍵集合</summary>
    private HashSet<string> ExpandPermissionKeys(IReadOnlyDictionary<string, long>? moduleMasks)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (moduleMasks is null) return keys;

        foreach (var (module, mask) in moduleMasks)
        {
            for (int bit = 0; bit < 64; bit++)
            {
                if ((mask & (1L << bit)) == 0) continue;
                var key = _permissionRegistry.ResolvePermissionKey(module, bit);
                keys.Add(string.IsNullOrEmpty(key) ? $"{module}#{bit}" : key);
            }
        }
        return keys;
    }

    /// <inheritdoc />
    public async Task<(bool Succeeded, string Message)> UpdateRolePermissionsAsync(UpdateRoleGlobalPermissionsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RoleId))
        {
            _logger.LogWarning("[PermissionManagementService] 更新失敗：傳入的 RoleId 為空。");
            return (false, "角色識別碼 (RoleId) 不得為空。");
        }

        try
        {
            // 已停用 / 已鎖定的角色不再授予任何權限：禁止異動其權限設定
            var role = await _roleManager.FindByIdAsync(request.RoleId).ConfigureAwait(false);
            if (role is not null && role.Status is not (RoleStatus.Active or RoleStatus.PendingApproval))
            {
                return (false, $"角色 [{role.Name}] 目前為「{role.Status.GetDisplayName()}」，無法異動權限設定。");
            }

            var targetPermissions = request.PermissionKeys?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            Dictionary<string, long> moduleBitmaskDict = await _bitmaskService
                .CalculateModuleBitmasksAsync(targetPermissions, cancellationToken)
                .ConfigureAwait(false);

            // 異動前快照，供歷程記錄權限差異
            var previousKeys = ExpandPermissionKeys(await _rolePermissionRepository
                .GetGlobalPermissionsAsync(request.RoleId, cancellationToken)
                .ConfigureAwait(false));

            bool success = await _rolePermissionRepository
                .SaveRoleGlobalPermissionsAsync(request.RoleId, moduleBitmaskDict, cancellationToken)
                .ConfigureAwait(false);

            if (!success)
            {
                _logger.LogWarning("[PermissionManagementService] 角色全域權限寫入資料庫失敗。RoleId: {RoleId}", request.RoleId);
                return (false, "寫入角色全域權限資料時發生失敗。");
            }

            _logger.LogInformation(
                "[PermissionManagementService] 成功更新角色 [{RoleId}] 全域權限，共影響 [{Count}] 個模組。",
                request.RoleId,
                moduleBitmaskDict.Count);

            // 角色成員的有效權限已改變：清除快取並即時通知前端
            if (role?.Name is not null)
            {
                await _permissionChangeNotifier.NotifyRoleMembersAsync(role.Name, "role-permissions-updated", cancellationToken).ConfigureAwait(false);

                var currentKeys = ExpandPermissionKeys(moduleBitmaskDict);
                var added = currentKeys.Except(previousKeys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();
                var removed = previousKeys.Except(currentKeys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();
                var memberCount = (await _userManager.GetUsersInRoleAsync(role.Name).ConfigureAwait(false)).Count;

                await _roleAuditService.RecordAsync(role.Id, role.Name, RoleAuditActions.PermissionsUpdated,
                    $"全域權限調整：新增 {added.Count} 項、移除 {removed.Count} 項，影響 {memberCount} 位成員",
                    memberCount, new { Added = added, Removed = removed }, cancellationToken).ConfigureAwait(false);
            }

            return (true, $"角色全域權限更新成功，共更新 {moduleBitmaskDict.Count} 個模組區段。");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PermissionManagementService] 更新角色 [{RoleId}] 全域權限作業已取消。", request.RoleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionManagementService] 更新角色 [{RoleId}] 全域權限時發生未預期異常。", request.RoleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<UserAuditPermissionsResponseDto?> GetUserAllPermissionsAsync(
        string userId,
        Guid? tenantLabId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        try
        {
            var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
            if (user == null)
            {
                _logger.LogWarning("[PermissionManagementService] 查詢稽核資料失敗：找不到識別碼為 [{UserId}] 的使用者。", userId);
                return null;
            }

            var roleNames = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
            var roleAuditList = new List<RoleAuditInfoItemDto>();
            var effectiveRoleIds = new List<string>();

            // 1. 查詢角色詳情，並套用與執行期授權 (RoleEffectivePermissionProvider) 相同的生效規則：
            //    已刪除 / 已停用 / 新建待審核 / 不在生效期間的角色不計入繼承權限
            var roles = await _roleManager.Roles.AsNoTracking()
                .Where(r => r.Name != null && roleNames.Contains(r.Name))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var roleGuids = roles.Select(r => r.Id).ToList();
            var pendingChanges = await _dbContext.Set<RoleChangeRequest>().AsNoTracking()
                .Where(x => roleGuids.Contains(x.RoleId) && x.Status == RoleChangeRequestStatus.Pending)
                .Select(x => new { x.RoleId, x.PreviousRoleStatus })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var now = DateTimeOffset.UtcNow;
            foreach (var role in roles.OrderBy(r => r.IsDeleted).ThenBy(r => r.Name))
            {
                var pendingChange = pendingChanges.FirstOrDefault(p => p.RoleId == role.Id);
                string? ineffectiveReason =
                    role.IsDeleted ? "角色已刪除" :
                    role.Status == RoleStatus.Disabled ? "角色已停用" :
                    role.Status == RoleStatus.PendingApproval && pendingChange?.PreviousRoleStatus != RoleStatus.Active ? "新角色待審核，尚未啟用" :
                    role.EffectiveFrom > now ? "角色尚未生效" :
                    role.EffectiveTo < now ? "角色已過期" :
                    null;

                if (ineffectiveReason is null) effectiveRoleIds.Add(role.Id.ToString());

                roleAuditList.Add(new RoleAuditInfoItemDto
                {
                    RoleName = role.Name!,
                    Description = role.Description,
                    Status = role.IsDeleted ? "已刪除" : role.Status.GetDisplayName(),
                    IsEffective = ineffectiveReason is null,
                    IneffectiveReason = ineffectiveReason,
                    HasPendingPermissionChange = pendingChange is not null
                });
            }

            var claims = await _userManager.GetClaimsAsync(user).ConfigureAwait(false);
            const string permissionClaimType = "Permission";
            var claimPermissions = claims
                .Where(c => c.Type == permissionClaimType)
                .Select(c => c.Value)
                .ToList();

            // 2. 解析全域直接權限
            var globalBitmaskMap = await _userPermissionRepository.GetGlobalPermissionsAsync(userId, cancellationToken).ConfigureAwait(false);
            var decodedGlobalDirect = await _bitmaskService.DecodeBitmaskToPermissionsAsync(globalBitmaskMap, cancellationToken).ConfigureAwait(false);

            var globalDirectKeys = claimPermissions
                .Union(decodedGlobalDirect, StringComparer.OrdinalIgnoreCase)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            // 3. 解析角色繼承權限（僅計入生效中的角色；權限變更送審中者為核准前的既有權限）
            //    與執行期一致：全域 / 系統角色處處有效；實驗室專屬角色僅於其指派的實驗室內生效
            var roleKeysById = new Dictionary<string, List<string>>();
            foreach (var roleId in effectiveRoleIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var roleMatrix = await GetRoleGlobalPermissionsAsync(roleId, cancellationToken).ConfigureAwait(false);
                roleKeysById[roleId] = roleMatrix?.GrantedPermissionKeys ?? [];
            }

            var labScopedRoles = roles.Where(r => r.ScopeType == RoleScopeType.LabScoped && effectiveRoleIds.Contains(r.Id.ToString())).ToList();
            var labScopedIds = labScopedRoles.Select(r => r.Id).ToList();
            var roleLabScopes = await _dbContext.Set<RoleLabScope>().AsNoTracking()
                .Where(s => labScopedIds.Contains(s.RoleId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            HashSet<Guid> LabsOf(ApplicationRole role)
            {
                var labs = roleLabScopes.Where(s => s.RoleId == role.Id).Select(s => s.TenantLabId).ToHashSet();
                if (labs.Count == 0 && role.TenantLabId.HasValue) labs.Add(role.TenantLabId.Value);
                return labs;
            }

            // 稽核角色：權限僅於具有效稽核指派的實驗室生效（全域範圍不計入）
            var auditRoles = roles.Where(r => r.RequiresAuditAssignment && effectiveRoleIds.Contains(r.Id.ToString())).ToList();
            var auditRoleIds = auditRoles.Select(r => r.Id).ToList();
            var activeAuditLabs = await _labAuditService.GetActiveLabsAsync(user.Id, cancellationToken).ConfigureAwait(false);

            var globalRoleKeys = roleKeysById
                .Where(kv => !labScopedIds.Contains(Guid.Parse(kv.Key)) && !auditRoleIds.Contains(Guid.Parse(kv.Key)))
                .SelectMany(kv => kv.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p)
                .ToList();

            List<string> LabRoleKeys(Guid labId) => globalRoleKeys
                .Concat(labScopedRoles.Where(r => LabsOf(r).Contains(labId)).SelectMany(r => roleKeysById[r.Id.ToString()]))
                .Concat(activeAuditLabs.ContainsKey(labId) ? auditRoles.SelectMany(r => roleKeysById[r.Id.ToString()]) : [])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p)
                .ToList();

            // 稽核角色：列出目前有效的稽核實驗室與迄日
            foreach (var item in roleAuditList.Where(i => i.IsEffective && auditRoles.Any(r => r.Name == i.RoleName)))
            {
                var names = new List<string>();
                foreach (var (labId, until) in activeAuditLabs)
                {
                    var org = await _orgIntegrationService.GetOrganizationByIdAsync(labId, cancellationToken).ConfigureAwait(false);
                    names.Add($"{org?.Name ?? labId.ToString()[..8]}（至 {until.ToLocalTime():yyyy-MM-dd}）");
                }

                item.ScopeLabIds = activeAuditLabs.Keys.ToList();
                item.ScopeLabNames = names;
                item.Status = names.Count > 0
                    ? $"{item.Status} · 稽核角色（稽核中：{string.Join("、", names)}）"
                    : $"{item.Status} · 稽核角色（目前無有效稽核指派，權限不生效）";
            }

            foreach (var item in roleAuditList.Where(i => i.IsEffective))
            {
                var role = labScopedRoles.FirstOrDefault(r => r.Name == item.RoleName);
                if (role is null) continue;

                // 列出實驗室專屬角色實際適用的實驗室，讓稽核者直接看出角色在哪些實驗室生效
                var names = new List<string>();
                foreach (var labId in LabsOf(role))
                {
                    var org = await _orgIntegrationService.GetOrganizationByIdAsync(labId, cancellationToken).ConfigureAwait(false);
                    names.Add(org?.Name ?? labId.ToString()[..8]);
                }

                item.ScopeLabIds = LabsOf(role).ToList();
                item.ScopeLabNames = names;
                item.Status = $"{item.Status} · 實驗室專屬（{string.Join("、", names)}）";
            }

            // 4. 解析全域有效權限
            // 管理員（SuperAdmin / SystemAdmin 角色、IsSuperAdmin Claim 或內建 sysadmin / admin 帳號）於執行期
            // 由 DefaultPermissionResolver 授與全部權限且不受實驗室範圍限制，不依賴資料表權限列；
            // 稽核檢視須與之一致，改列出所有已註冊權限，否則會誤顯示為 0。
            bool isAdmin =
                claims.Any(c => string.Equals(c.Type, "IsSuperAdmin", StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase)) ||
                roleNames.Any(r => string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(r, "SystemAdmin", StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(user.UserName, "sysadmin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.UserName, "admin", StringComparison.OrdinalIgnoreCase);

            List<string> allRegisteredKeys = [];
            if (isAdmin)
            {
                allRegisteredKeys = (await _dbContext.Set<PermissionMetadata>()
                        .AsNoTracking()
                        .Where(m => m.PermissionKey != null && m.PermissionKey != string.Empty)
                        .Select(m => m.PermissionKey!)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p)
                    .ToList();
            }

            var globalEffectiveKeys = globalDirectKeys
                .Union(globalRoleKeys, StringComparer.OrdinalIgnoreCase)
                .Union(allRegisteredKeys, StringComparer.OrdinalIgnoreCase)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            // 轉換為豐富資訊之 PermissionAuditInfoItemDto
            var menuLookup = await BuildMenuLookupAsync(cancellationToken).ConfigureAwait(false);
            var globalDirectPermissions = EnrichPermissionDetails(globalDirectKeys, menuLookup);
            var globalRolePermissions = EnrichPermissionDetails(globalRoleKeys, menuLookup);
            var globalEffectivePermissions = EnrichPermissionDetails(globalEffectiveKeys, menuLookup);

            var responseDto = new UserAuditPermissionsResponseDto
            {
                UserId = user.Id.ToString(),
                Username = user.UserName ?? string.Empty,
                Roles = roleAuditList,
                GlobalDirectPermissions = globalDirectPermissions,
                GlobalRolePermissions = globalRolePermissions,
                GlobalEffectivePermissions = globalEffectivePermissions,
                SecondaryLabPermissions = new List<UserLabPermissionAuditDto>()
            };

            // 5. 解析主區域、兼區域與稽核指派實驗室的權限（與執行期授權同源規則）
            if (Guid.TryParse(userId, out var userGuid))
            {
                async Task<UserLabPermissionAuditDto> BuildLabAsync(Guid labTenantId, bool isPrimary, string? jobTitle,
                    DateTimeOffset? effectiveDate, DateTimeOffset? expiryDate, DateTimeOffset? auditUntil, bool isAuditOnly)
                {
                    var orgInfo = await _orgIntegrationService.GetOrganizationByIdAsync(labTenantId, cancellationToken).ConfigureAwait(false);

                    var labBitmaskMap = await _userPermissionRepository.GetPermissionsByLabAsync(userId, labTenantId, cancellationToken).ConfigureAwait(false);
                    var labDirectKeys = (await _bitmaskService.DecodeBitmaskToPermissionsAsync(labBitmaskMap, cancellationToken).ConfigureAwait(false))
                        .OrderBy(p => p)
                        .ToList();

                    var labRoleKeys = LabRoleKeys(labTenantId);

                    var labEffectiveKeys = labDirectKeys
                        .Union(labRoleKeys, StringComparer.OrdinalIgnoreCase)
                        .Union(globalDirectKeys, StringComparer.OrdinalIgnoreCase)
                        .Distinct()
                        .OrderBy(p => p)
                        .ToList();

                    // 區域實驗室功能開放範圍：有效權限不得超出實驗室開放的功能（管理員於執行期不受此限）
                    labEffectiveKeys = isAdmin
                        ? labEffectiveKeys.Union(allRegisteredKeys, StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList()
                        : (await _labFunctionScopeService.FilterAsync(labTenantId, labEffectiveKeys, cancellationToken).ConfigureAwait(false)).ToList();

                    return new UserLabPermissionAuditDto
                    {
                        TenantLabId = labTenantId,
                        CategoryCode = orgInfo?.ParentLabCode ?? string.Empty,
                        CategoryName = orgInfo?.ParentLabName ?? string.Empty,
                        LabCode = orgInfo?.Code ?? string.Empty,
                        LabName = orgInfo?.Name ?? string.Empty,
                        IsPrimary = isPrimary,
                        JobTitle = jobTitle,
                        EffectiveDate = effectiveDate,
                        ExpiryDate = expiryDate,
                        IsAuditAssignment = isAuditOnly,
                        AuditUntil = auditUntil,
                        LabDirectPermissions = EnrichPermissionDetails(labDirectKeys, menuLookup),
                        LabRolePermissions = EnrichPermissionDetails(labRoleKeys, menuLookup),
                        LabEffectivePermissions = EnrichPermissionDetails(labEffectiveKeys, menuLookup)
                    };
                }

                bool InRequestedScope(Guid labTenantId) =>
                    !tenantLabId.HasValue || tenantLabId.Value == Guid.Empty || tenantLabId.Value == labTenantId;

                var accessibleLabs = await _userLabRepository.GetAccessibleLabsAsync(userGuid, cancellationToken).ConfigureAwait(false) ?? [];
                foreach (var lab in accessibleLabs.Where(l => l.IsActive && InRequestedScope(l.TenantLabId)))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var labAuditDto = await BuildLabAsync(lab.TenantLabId, lab.IsPrimary, lab.JobTitle, lab.EffectiveDate, lab.ExpiryDate,
                        activeAuditLabs.TryGetValue(lab.TenantLabId, out var until) ? until : null, isAuditOnly: false).ConfigureAwait(false);

                    if (lab.IsPrimary) responseDto.PrimaryLabPermission = labAuditDto;
                    else responseDto.SecondaryLabPermissions.Add(labAuditDto);
                }

                // 僅經由稽核指派可進入的實驗室（非人事歸屬）
                foreach (var (auditLabId, auditUntil) in activeAuditLabs.Where(a => accessibleLabs.All(l => l.TenantLabId != a.Key) && InRequestedScope(a.Key)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    responseDto.SecondaryLabPermissions.Add(await BuildLabAsync(auditLabId, false, "稽核指派", null, auditUntil, auditUntil, isAuditOnly: true).ConfigureAwait(false));
                }
            }

            responseDto.SecondaryLabPermissions = responseDto.SecondaryLabPermissions
                .OrderBy(l => l.CategoryCode)
                .ThenBy(l => l.LabCode)
                .ToList();

            return responseDto;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PermissionManagementService] 查詢使用者權限稽核資料作業已取消。UserId: {UserId}", userId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionManagementService] 查詢使用者權限稽核資料時發生異常。UserId: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// 依據 PermissionKey 查表/對照 Registry 補全 Title 與 Description 資訊
    /// </summary>
    /// <summary>選單位置：功能（Module + Controller）對應到實際選單樹的區段 / 群組</summary>
    /// <param name="SectionTitle">選單頂層區段名稱（與側邊選單一致）</param>
    /// <param name="SectionOrder">區段排序</param>
    /// <param name="GroupTitle">選單群組名稱；頁面直接掛在區段下時為 null</param>
    /// <param name="GroupOrder">頁面所屬「區段下第一層節點」的排序（群組或頁面本身），決定功能在區段內的先後</param>
    /// <param name="PageOrder">頁面在其父節點內的排序</param>
    /// <param name="SubGroupTitle">頁面與「區段下第一層節點」之間的子群組名稱（例如 系統管理 > 權限管理 > 頁面 的「權限管理」）；無則為 null</param>
    /// <param name="TopKey">區段下第一層節點的唯一識別（區段 + 群組），用來判斷兩個功能是否在同一個選單群組</param>
    private sealed record MenuPlacement(string SectionTitle, int SectionOrder, string? GroupTitle, int GroupOrder, int PageOrder, string? SubGroupTitle, string TopKey, int TopChildCount, int SubGroupChildCount, int ChildOrder);

    /// <summary>選單頁面入口</summary>
    private sealed record MenuEntry(string Title, string? Route);

    private sealed record MenuLookup(
        Dictionary<string, MenuPlacement> ByController,
        Dictionary<string, MenuPlacement> ByControllerName,
        Dictionary<string, MenuPlacement> ByModule,
        Dictionary<string, MenuEntry> EntriesByKey,
        FunctionGroupResolver ModuleResolver)
    {
        /// <summary>依 (模組, Controller) 找選單位置；找不到時以 Controller 名稱比對（權限註冊表與資料庫的模組名稱可能不同）</summary>
        public MenuPlacement? Find(string moduleName, string controllerName) =>
            ByController.TryGetValue(ControllerKey(moduleName, controllerName), out var exact) ? exact
            : ByControllerName.TryGetValue(controllerName, out var byName) ? byName
            : null;
    }

    private static string ControllerKey(string module, string controller) => $"{module}|{controller}".ToUpperInvariant();

    /// <summary>
    /// 依實際選單樹 (Section → Group → Page) 建立權限分組對照：
    /// 選單頁面所屬的功能歸入該頁面所在群組；同一功能的其他動作權限一併歸入，與使用者看到的選單一致
    /// </summary>
    private async Task<MenuLookup> BuildMenuLookupAsync(CancellationToken cancellationToken)
    {
        // 查詢條件與排序須與側邊選單（GetUserMenuTreeQuery）完全相同：
        // 同 DisplayOrder 的項目先後由資料庫決定，這裡以「查詢結果名次」作為排序依據，才能與選單的實際顯示順序一致
        var items = await _dbContext.MenuItems.AsNoTracking()
            .Where(m => m.IsActive && m.IsVisible)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var byId = items.ToDictionary(m => m.Id);
        // 各節點的直接子項目數（與側邊選單徽章同算法：可見且啟用的直接子節點）
        var childCount = items.Where(m => m.ParentId.HasValue).GroupBy(m => m.ParentId!.Value).ToDictionary(g => g.Key, g => g.Count());
        int ChildCountOf(MenuItem? node) => node is not null && childCount.TryGetValue(node.Id, out var c) ? c : 0;
        var rank = new Dictionary<Guid, int>(items.Count);
        for (var i = 0; i < items.Count; i++) rank[items[i].Id] = i;

        // 模組中文名稱：與「指派權限」樹狀圖一致，經 FunctionGroupResolver 解析（含功能管理的改名/改掛）
        var controllerMetadatas = await _dbContext.Set<ControllerMetadata>().AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var overrides = await _dbContext.Set<FunctionGroupOverride>().AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var moduleResolver = new FunctionGroupResolver(controllerMetadatas, overrides);

        var byController = new Dictionary<string, MenuPlacement>(StringComparer.OrdinalIgnoreCase);
        var byControllerName = new Dictionary<string, MenuPlacement>(StringComparer.OrdinalIgnoreCase);
        var byModule = new Dictionary<string, MenuPlacement>(StringComparer.OrdinalIgnoreCase);
        var entries = new Dictionary<string, MenuEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in items.Where(m => m.Type == MenuType.Page).OrderBy(m => m.DisplayOrder))
        {
            var key = !string.IsNullOrWhiteSpace(page.PermissionKey) ? page.PermissionKey! : page.Key;
            entries.TryAdd(key, new MenuEntry(page.DisplayName, page.Route));

            // 同一把權限鍵可能由多個功能（Controller）共用（例如專案維護與交付物管理共用 PMS.PROJECT.READ）：
            // 全部歸入此選單頁面所在群組，而不是只取註冊表的第一筆，否則其餘功能會掉到獨立的模組群組
            var metas = _permissionRegistry.GetAllPermissions()
                .Where(p => string.Equals(p.PermissionKey, key, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (metas.Count == 0) continue;

            // 自頁面往上走到頂層區段：區段 → (群組) → 頁面
            // top = 區段下第一層節點（群組，或直接掛在區段下的頁面本身）
            var top = page;
            var root = page;
            var topChild = page; // 區段下第一層節點（top）之下、包含此頁面的子節點（子群組或頁面本身）
            while (root.ParentId is { } parentId && byId.TryGetValue(parentId, out var parentNode))
            {
                topChild = top;
                top = root;
                root = parentNode;
            }

            if (ReferenceEquals(root, page)) continue;
            var groupNode = !ReferenceEquals(top, page) ? top : null;
            // 子群組：頁面的直接父節點，且不是區段本身或區段下第一層節點
            var directParent = page.ParentId is { } directParentId && byId.TryGetValue(directParentId, out var dp) ? dp : null;
            var subGroup = directParent is not null && !ReferenceEquals(directParent, root) && !ReferenceEquals(directParent, top) ? directParent : null;

            foreach (var meta in metas)
            {
                var placement = new MenuPlacement(root.DisplayName, rank[root.Id], groupNode?.DisplayName, rank[top.Id], rank[page.Id], subGroup?.DisplayName, top.Key, ChildCountOf(top), ChildCountOf(subGroup), rank[topChild.Id]);
                byController.TryAdd(ControllerKey(meta.ModuleName, meta.ControllerName), placement);
                byControllerName.TryAdd(meta.ControllerName, placement);
                if (!byModule.TryGetValue(meta.ModuleName, out var existingModulePlacement) || placement.SectionOrder < existingModulePlacement.SectionOrder)
                {
                    byModule[meta.ModuleName] = placement;
                }
            }
        }

        return new MenuLookup(byController, byControllerName, byModule, entries, moduleResolver);
    }

    /// <summary>
    /// 功能在權限樹中的群組標題：與模組同一個選單群組 → 只顯示子群組；位於其他選單群組 → 「群組 › 子群組」
    /// </summary>
    private static string? FunctionGroupTitle(MenuPlacement? function, MenuPlacement? moduleHome)
    {
        if (function is null) return null;
        var sameTop = moduleHome is not null && string.Equals(function.TopKey, moduleHome.TopKey, StringComparison.Ordinal);
        if (sameTop) return function.SubGroupTitle;
        return string.IsNullOrWhiteSpace(function.SubGroupTitle)
            ? function.GroupTitle
            : (string.IsNullOrWhiteSpace(function.GroupTitle) ? function.SubGroupTitle : $"{function.GroupTitle} › {function.SubGroupTitle}");
    }

    /// <summary>
    /// 選單檢視：節點 = 選單群組（區段下第一層），依選單順序排列；
    /// 不在選單中的功能併入其所屬模組的主要選單群組（排在最後），模組完全不在選單時以模組自成一個節點（排在所有選單群組之後）。
    /// ModuleName 為選單節點 Key（唯一），實際模組名稱記於各功能的 ModuleName。
    /// </summary>
    private static List<PermissionModuleDto> BuildMenuView(
        List<PermissionModuleDto> legacy,
        Dictionary<PermissionFunctionDto, (string Module, MenuPlacement? Place)> functionInfo,
        Dictionary<string, MenuPlacement?> moduleHomes,
        Dictionary<string, List<ControllerMetadata>> pagesByController)
    {
        var buckets = new Dictionary<string, (MenuPlacement? Home, PermissionModuleDto Dto, List<(PermissionFunctionDto Fn, MenuPlacement? Place)> Items)>(StringComparer.Ordinal);

        foreach (var module in legacy)
        {
            foreach (var function in module.Functions)
            {
                var (moduleName, place) = functionInfo[function];
                var home = place ?? (moduleHomes.TryGetValue(moduleName, out var h) ? h : null);
                var key = home is null ? $"M|{moduleName}" : home.TopKey;

                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = (home, new PermissionModuleDto
                    {
                        ModuleName = key,
                        ModuleTitle = home is null ? module.ModuleTitle : (home.GroupTitle ?? home.SectionTitle),
                        SectionTitle = home?.SectionTitle,
                        MenuItemCount = home?.TopChildCount,
                        Description = module.Description
                    }, new List<(PermissionFunctionDto, MenuPlacement?)>());
                    buckets[key] = bucket;
                }

                bucket.Items.Add((function, place));
            }
        }

        var result = new List<PermissionModuleDto>();
        foreach (var bucket in buckets.Values
                     .OrderBy(b => b.Home is null ? 1 : 0)
                     .ThenBy(b => b.Home?.SectionOrder ?? int.MaxValue)
                     .ThenBy(b => b.Home?.GroupOrder ?? int.MaxValue)
                     .ThenBy(b => b.Dto.ModuleTitle, StringComparer.CurrentCulture))
        {
            foreach (var (function, place) in bucket.Items
                         .OrderBy(i => i.Place?.ChildOrder ?? int.MaxValue)
                         .ThenBy(i => i.Place?.PageOrder ?? int.MaxValue))
            {
                // 功能（Controller）展開為選單頁面；快取內的物件由兩種檢視共用，一律複製後再改
                pagesByController.TryGetValue(function.FunctionName, out var pages);
                foreach (var pageRow in SplitIntoPages(function, place, pages))
                {
                    bucket.Dto.Functions.Add(pageRow);
                }
            }

            result.Add(bucket.Dto);
        }

        return result;
    }

    private static PermissionActionDto CloneAction(PermissionActionDto source, string? permissionTitle) => new()
    {
        PermissionKey = source.PermissionKey,
        PermissionTitle = permissionTitle,
        ActionName = source.ActionName,
        ActionTitle = source.ActionTitle,
        Description = source.Description,
        BitPosition = source.BitPosition,
        IsCustomized = source.IsCustomized,
        IsDisabled = source.IsDisabled,
        DisabledReason = source.DisabledReason
    };

    /// <summary>
    /// 選單檢視的「頁面」層：Controller 內每個 IsMenu = true 的功能（以權限鍵去重）成為一列，
    /// 頁面標題取自 [Function] 標題，其權限為該頁的讀取權限；該 Controller 的其餘操作權限（IsMenu = false）
    /// 掛在排序最前的頁面下，其餘頁面只含自己的讀取權限。Controller 沒有選單頁面時維持單一功能列。
    /// 子群組（區塊 = [ControllerTitle]）以 GroupTitle 標示。
    /// </summary>
    private static List<PermissionFunctionDto> SplitIntoPages(PermissionFunctionDto function, MenuPlacement? place, List<ControllerMetadata>? pages)
    {
        string? blockTitle = string.IsNullOrWhiteSpace(place?.SubGroupTitle) ? null : place!.SubGroupTitle;
        int? blockCount = blockTitle is null ? null : place!.SubGroupChildCount;

        var allPermissions = new List<PermissionActionDto>();
        if (function.ReadPermission is not null) allPermissions.Add(function.ReadPermission);
        allPermissions.AddRange(function.ActionPermissions);

        var pageRows = (pages ?? new List<ControllerMetadata>())
            .GroupBy(p => p.PermissionKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Where(p => allPermissions.Any(a => string.Equals(a.PermissionKey, p.PermissionKey.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (pageRows.Count == 0)
        {
            return new List<PermissionFunctionDto>
            {
                new()
                {
                    ModuleName = function.ModuleName,
                    FunctionName = function.FunctionName,
                    FunctionTitle = function.FunctionTitle,
                    GroupTitle = blockTitle,
                    GroupItemCount = blockCount,
                    Description = function.Description,
                    ReadPermission = function.ReadPermission,
                    ActionPermissions = function.ActionPermissions
                }
            };
        }

        var pageKeys = pageRows.Select(p => p.PermissionKey.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var operations = allPermissions
            .Where(a => !pageKeys.Contains(a.PermissionKey))
            .DistinctBy(a => a.PermissionKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<PermissionFunctionDto>();
        for (var i = 0; i < pageRows.Count; i++)
        {
            var page = pageRows[i];
            var read = allPermissions.First(a => string.Equals(a.PermissionKey, page.PermissionKey.Trim(), StringComparison.OrdinalIgnoreCase));
            var pageTitle = string.IsNullOrWhiteSpace(page.DisplayName) ? function.FunctionTitle : page.DisplayName;

            result.Add(new PermissionFunctionDto
            {
                ModuleName = function.ModuleName,
                FunctionName = $"{function.FunctionName}.{page.ActionName}",
                FunctionTitle = pageTitle,
                GroupTitle = blockTitle,
                GroupItemCount = blockCount,
                Description = string.IsNullOrWhiteSpace(page.Description) ? function.Description : page.Description,
                // 未自訂名稱的讀取權限，名稱跟隨頁面標題
                ReadPermission = CloneAction(read, read.IsCustomized ? read.PermissionTitle : pageTitle),
                ActionPermissions = i == 0
                    ? operations.Select(a => CloneAction(a, a.PermissionTitle)).ToList()
                    : new List<PermissionActionDto>()
            });
        }

        return result;
    }

    /// <summary>與 <see cref="FunctionGroupTitle"/> 對應的選單項目數</summary>
    private static int? FunctionGroupItemCount(MenuPlacement? function, MenuPlacement? moduleHome)
    {
        if (function is null) return null;
        var sameTop = moduleHome is not null && string.Equals(function.TopKey, moduleHome.TopKey, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(function.SubGroupTitle)) return function.SubGroupChildCount;
        return sameTop ? null : function.TopChildCount;
    }

    private List<PermissionAuditInfoItemDto> EnrichPermissionDetails(IEnumerable<string> permissionKeys, MenuLookup? menu = null)
    {
        return permissionKeys.Select(key =>
        {
            _permissionRegistry.TryGetPermission(key, out var meta);

            var title = !string.IsNullOrWhiteSpace(meta?.PermissionTitle)
                ? meta.PermissionTitle
                : (!string.IsNullOrWhiteSpace(meta?.ActionTitle) ? meta.ActionTitle : key);

            var functionTitle = !string.IsNullOrWhiteSpace(meta?.ControllerTitle) ? meta!.ControllerTitle! : meta?.ControllerName ?? "其他";
            MenuPlacement? placement = meta is not null ? menu?.Find(meta.ModuleName, meta.ControllerName) : null;
            // 無選單入口的功能：歸入同模組其他功能所在的選單區段
            MenuPlacement? modulePlacement = null;
            if (placement is null && meta is not null) menu?.ByModule.TryGetValue(meta.ModuleName, out modulePlacement);
            MenuEntry? entry = null;
            menu?.EntriesByKey.TryGetValue(key, out entry);

            var sectionTitle = placement?.SectionTitle
                ?? modulePlacement?.SectionTitle
                ?? (meta is not null ? menu?.ModuleResolver.FindModuleTitle(meta.ModuleName) : null)
                ?? meta?.ModuleTitle ?? meta?.ModuleName ?? "其他";
            int? sectionOrder = placement?.SectionOrder ?? modulePlacement?.SectionOrder;
            if (sectionOrder is null && menu is not null)
            {
                var sameTitle = menu.ByController.Values.FirstOrDefault(v => string.Equals(v.SectionTitle, sectionTitle, StringComparison.Ordinal));
                if (sameTitle is not null) sectionOrder = sameTitle.SectionOrder;
            }

            return new PermissionAuditInfoItemDto
            {
                PermissionKey = key,
                Title = title,
                Description = meta?.Description,
                // 無選單入口的功能：以模組 / 功能標題作為分組（排序置後）
                SectionTitle = sectionTitle,
                SectionOrder = sectionOrder ?? int.MaxValue,
                GroupTitle = placement?.GroupTitle ?? functionTitle,
                GroupOrder = placement?.GroupOrder ?? int.MaxValue,
                FunctionTitle = functionTitle,
                ActionTitle = !string.IsNullOrWhiteSpace(meta?.ActionTitle) ? meta!.ActionTitle : title,
                IsMenuEntry = entry is not null,
                MenuTitle = entry?.Title,
                MenuPath = entry?.Route
            };
        }).ToList();
    }


    /// <inheritdoc />
    public async Task<(bool Succeeded, string Message)> AssignUserPermissionsAsync(
        string userId,
        Guid? tenantLabId,
        AssignUserPermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
            if (user == null)
            {
                _logger.LogWarning("[PermissionManagementService] 指派權限失敗：找不到識別碼為 [{UserId}] 的使用者。", userId);
                return (false, $"找不到識別碼為 '{userId}' 的使用者。");
            }

            var targetPermissions = request.Permissions?.Distinct().ToList() ?? new List<string>();

            var moduleBitmaskDict = await _bitmaskService
                .CalculateModuleBitmasksAsync(targetPermissions, cancellationToken)
                .ConfigureAwait(false);

            // 停用權限不可新增指派 (既有綁定保留)：以相同遮罩編碼比對「新增的位元」是否落在停用權限上
            var existingMasks = tenantLabId.HasValue && tenantLabId.Value != Guid.Empty
                ? await _userPermissionRepository.GetPermissionsByLabAsync(userId, tenantLabId.Value, cancellationToken).ConfigureAwait(false)
                : await _userPermissionRepository.GetGlobalPermissionsAsync(userId, cancellationToken).ConfigureAwait(false);
            var newlyGrantedDisabled = await FindNewlyGrantedDisabledAsync(moduleBitmaskDict, existingMasks, cancellationToken).ConfigureAwait(false);
            if (newlyGrantedDisabled.Count > 0)
            {
                _logger.LogWarning("[PermissionManagementService] 拒絕指派使用者 [{UserId}] 已停用的權限: {Keys}", userId, string.Join(",", newlyGrantedDisabled));
                return (false, DisabledPermissionMessages.NewlyGranted(newlyGrantedDisabled));
            }

            bool success;
            if (tenantLabId.HasValue && tenantLabId.Value != Guid.Empty)
            {
                success = await _userPermissionRepository.SaveUserLabPermissionsAsync(
                    userId,
                    tenantLabId.Value,
                    moduleBitmaskDict,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                success = await _userPermissionRepository.SaveUserGlobalPermissionsAsync(
                    userId,
                    moduleBitmaskDict,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!success)
            {
                _logger.LogWarning("[PermissionManagementService] 將使用者 [{UserId}] 權限寫入資料庫時失敗。", userId);
                return (false, "將使用者權限寫入資料庫時發生錯誤，請稍後再試。");
            }

            _logger.LogInformation(
                "[PermissionManagementService] 成功更新使用者 [{UserId}] 的直接權限遮罩，影響模組數: [{Count}]",
                userId,
                moduleBitmaskDict.Count);

            await _permissionChangeNotifier.NotifyUsersAsync([userId], "user-permissions-updated", cancellationToken).ConfigureAwait(false);

            return (true, "使用者直接權限指派成功。");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PermissionManagementService] 指派使用者權限作業已取消。UserId: {UserId}", userId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionManagementService] 更新使用者直接權限時發生未預期異常。UserId: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// 找出「本次新增」且已停用的權限：目標遮罩中原本未設定、且屬於停用權限的位元，再解碼為權限鍵值
    /// </summary>
    private async Task<List<string>> FindNewlyGrantedDisabledAsync(
        Dictionary<string, long> targetMasks,
        Dictionary<string, long>? existingMasks,
        CancellationToken cancellationToken)
    {
        var unassignable = await _moduleStatusReader.GetUnassignablePermissionKeysAsync(cancellationToken).ConfigureAwait(false);
        if (unassignable.Count == 0) return [];

        var disabledMasks = await _bitmaskService.CalculateModuleBitmasksAsync(unassignable.Keys, cancellationToken).ConfigureAwait(false);

        var offending = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var (prefix, mask) in targetMasks)
        {
            long existing = existingMasks is not null && existingMasks.TryGetValue(prefix, out var e) ? e : 0L;
            long disabled = disabledMasks.TryGetValue(prefix, out var d) ? d : 0L;
            long added = mask & ~existing & disabled;
            if (added != 0) offending[prefix] = added;
        }

        if (offending.Count == 0) return [];

        var keys = await _bitmaskService.DecodeBitmaskToPermissionsAsync(offending, cancellationToken).ConfigureAwait(false);
        return keys.Where(unassignable.ContainsKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <inheritdoc />
    public async Task<PermissionUsersMasterDto> GetUsersByPermissionKeyAsync(
        string permissionKey,
        Guid? tenantLabId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        try
        {
            var normalizedPermissionKey = permissionKey.Trim();

            var allMeta = await _dbContext.Set<PermissionMetadata>()
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var permissionMeta = allMeta.FirstOrDefault(m =>
                string.Equals(m.PermissionKey, normalizedPermissionKey, StringComparison.OrdinalIgnoreCase));

            if (permissionMeta == null)
            {
                _logger.LogWarning("[PermissionManagementService] 查無權限代碼 [{PermissionKey}] 之元資料。", normalizedPermissionKey);
                return new PermissionUsersMasterDto();
            }

            var moduleName = permissionMeta.ModuleName ?? string.Empty;
            var masterDto = new PermissionUsersMasterDto
            {
                ModuleName = moduleName,
                ModuleTitle = permissionMeta.ModuleTitle ?? string.Empty,
                FunctionTitle = permissionMeta.ControllerTitle ?? string.Empty,
                PermissionTitle = permissionMeta.PermissionTitle ?? string.Empty,
                PermissionDescription = permissionMeta.Description ?? string.Empty,
                BitPosition = permissionMeta.BitPosition,
                TargetBitmaskFlag = (permissionMeta.BitPosition >= 0 && permissionMeta.BitPosition < 63) ? (1L << permissionMeta.BitPosition) : 0L
            };

            var userDetailDict = new Dictionary<string, PermissionUserDetailDto>(StringComparer.OrdinalIgnoreCase);

            PermissionUserDetailDto CreateOrGetUserDetailDto(string userId, string username, string email)
            {
                if (!userDetailDict.TryGetValue(userId, out var dto))
                {
                    dto = new PermissionUserDetailDto
                    {
                        UserId = userId,
                        Username = username,
                        Email = email,
                        HasDirectPermission = false
                    };
                    userDetailDict[userId] = dto;
                }
                return dto;
            }

            HashSet<string>? validLabUserIds = null;
            if (tenantLabId.HasValue && tenantLabId.Value != Guid.Empty)
            {
                var candidateUserList = await _userManager.Users.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
                var labUserIdsList = new List<string>();

                foreach (var candUser in candidateUserList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var accessibleLabs = await _userLabRepository.GetAccessibleLabsAsync(candUser.Id, cancellationToken).ConfigureAwait(false);
                    if (accessibleLabs.Any(l => l.TenantLabId == tenantLabId.Value && l.IsActive))
                    {
                        labUserIdsList.Add(candUser.Id.ToString());
                    }
                }

                validLabUserIds = new HashSet<string>(labUserIdsList, StringComparer.OrdinalIgnoreCase);
                if (validLabUserIds.Count == 0) return masterDto;
            }

            var allRolePermissions = await _dbContext.Set<RoleGlobalPermission>()
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var matchingRolePermissions = allRolePermissions
                .Where(r => (string.Equals(r.PermissionKey, moduleName, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(r.PermissionKey, normalizedPermissionKey, StringComparison.OrdinalIgnoreCase) ||
                             normalizedPermissionKey.StartsWith(r.PermissionKey + ".", StringComparison.OrdinalIgnoreCase) ||
                             r.PermissionKey.StartsWith(moduleName + ".", StringComparison.OrdinalIgnoreCase))
                         && (masterDto.TargetBitmaskFlag != 0 && (r.Bitmask & masterDto.TargetBitmaskFlag) != 0))
                .ToList();

            var roleBitmaskMap = matchingRolePermissions.ToDictionary(
                r => r.RoleId,
                r => r.Bitmask,
                StringComparer.OrdinalIgnoreCase);

            if (roleBitmaskMap.Count > 0)
            {
                var targetRoleIds = roleBitmaskMap.Keys.ToList();
                var matchingRoles = await _roleManager.Roles
                    .AsNoTracking()
                    .Where(r => targetRoleIds.Contains(r.Id.ToString()))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (matchingRoles.Count == 0)
                {
                    matchingRoles = await _roleManager.Roles
                        .AsNoTracking()
                        .Where(r => targetRoleIds.Contains(r.Name))
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                foreach (var role in matchingRoles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var roleIdStr = role.Id.ToString();
                    var roleName = role.Name ?? roleIdStr;

                    if (!roleBitmaskMap.TryGetValue(roleIdStr, out var roleBitmask))
                    {
                        roleBitmaskMap.TryGetValue(roleName, out roleBitmask);
                    }

                    var usersInRole = await _userManager.GetUsersInRoleAsync(roleName).ConfigureAwait(false);
                    foreach (var user in usersInRole)
                    {
                        var userIdStr = user.Id.ToString();
                        if (validLabUserIds != null && !validLabUserIds.Contains(userIdStr)) continue;

                        var userDto = CreateOrGetUserDetailDto(userIdStr, user.UserName ?? string.Empty, user.Email ?? string.Empty);

                        if (!userDto.GrantedByRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase))
                        {
                            userDto.GrantedByRoles.Add(roleName);
                            userDto.RoleBitmaskDetails[roleName] = roleBitmask;
                        }
                    }
                }
            }

            var candidateUsers = await _userManager.Users.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var user in candidateUsers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var userIdStr = user.Id.ToString();

                if (validLabUserIds != null && !validLabUserIds.Contains(userIdStr)) continue;

                Dictionary<string, long> dbBitmaskMap;
                if (tenantLabId.HasValue && tenantLabId.Value != Guid.Empty)
                {
                    dbBitmaskMap = await _userPermissionRepository.GetPermissionsByLabAsync(userIdStr, tenantLabId.Value, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    dbBitmaskMap = await _userPermissionRepository.GetGlobalPermissionsAsync(userIdStr, cancellationToken).ConfigureAwait(false);
                }

                long userBitmask = 0;
                if (!dbBitmaskMap.TryGetValue(moduleName, out userBitmask))
                {
                    var kvp = dbBitmaskMap.FirstOrDefault(x =>
                        string.Equals(x.Key, moduleName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Key, normalizedPermissionKey, StringComparison.OrdinalIgnoreCase) ||
                        normalizedPermissionKey.StartsWith(x.Key + ".", StringComparison.OrdinalIgnoreCase));

                    userBitmask = kvp.Value;
                }

                bool hasDirect = masterDto.TargetBitmaskFlag != 0 && (userBitmask & masterDto.TargetBitmaskFlag) != 0;

                if (hasDirect)
                {
                    var userDto = CreateOrGetUserDetailDto(userIdStr, user.UserName ?? string.Empty, user.Email ?? string.Empty);
                    userDto.HasDirectPermission = true;
                    userDto.RawUserBitmask = userBitmask;
                }
            }

            masterDto.Users = userDetailDict.Values
                .Where(u => u.HasDirectPermission || (u.GrantedByRoles != null && u.GrantedByRoles.Count > 0))
                .OrderBy(u => u.Username)
                .ToList();

            if (masterDto.Users.Count > 0)
            {
                var matchedUserGuids = masterDto.Users
                    .Select(u => Guid.TryParse(u.UserId, out var g) ? g : Guid.Empty)
                    .Where(g => g != Guid.Empty)
                    .ToList();

                var labMappingDtoDict = new Dictionary<string, List<UserLabDto>>(StringComparer.OrdinalIgnoreCase);

                foreach (var userIdGuid in matchedUserGuids)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var accessibleLabs = await _userLabRepository.GetAccessibleLabsAsync(userIdGuid, cancellationToken).ConfigureAwait(false);
                    List<UserLabDto> labDtos = new List<UserLabDto>();

                    if (accessibleLabs != null && accessibleLabs.Count > 0)
                    {
                        foreach (var accessLab in accessibleLabs)
                        {
                            var orgInfo = await _orgIntegrationService.GetOrganizationByIdAsync(accessLab.TenantLabId, cancellationToken);
                            if (orgInfo != null)
                            {
                                var lab = new UserLabDto
                                {
                                    LabId = accessLab.LabId,
                                    TenantLabId = accessLab.TenantLabId,
                                    CategoryCode= orgInfo.ParentLabCode??string.Empty,
                                    CategoryName=orgInfo.ParentLabName??string.Empty,
                                    LabCode = orgInfo.Code,
                                    LabName = orgInfo.Name,
                                    IsPrimary = accessLab.IsPrimary,
                                    JobTitle = accessLab.JobTitle,
                                    EffectiveDate = accessLab.EffectiveDate,
                                    ExpiryDate = accessLab.ExpiryDate
                                };

                                labDtos.Add(lab);
                            }
                        }
                    }

                    if (labDtos.Count > 0)
                    {
                        labMappingDtoDict[userIdGuid.ToString()] = labDtos;
                    }
                }

                foreach (var userDto in masterDto.Users)
                {
                    if (labMappingDtoDict.TryGetValue(userDto.UserId, out var mappings))
                    {
                        userDto.LabMappings = mappings;
                    }
                }
            }

            return masterDto;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PermissionManagementService] 查詢具備權限 [{PermissionKey}] 的使用者清單作業已取消。", permissionKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionManagementService] 查詢具備權限 [{PermissionKey}] 的使用者時發生異常。", permissionKey);
            throw;
        }
    }
}