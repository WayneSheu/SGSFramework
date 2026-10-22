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
        ILogger<PermissionManagementService<TDbContext>> logger)
    {
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
    {
        try
        {
            var metadataList = await _dbContext.Set<PermissionMetadata>()
                .AsNoTracking()
                .OrderBy(m => m.ModuleTitle)
                .ThenBy(m => m.ControllerTitle)
                .ThenBy(m => m.PermissionKey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (metadataList.Count == 0)
            {
                return new List<PermissionModuleDto>();
            }

            var result = new List<PermissionModuleDto>();
            var moduleGroups = metadataList.GroupBy(m => new { m.ModuleName, m.ModuleTitle });

            foreach (var moduleGroup in moduleGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var moduleDto = new PermissionModuleDto
                {
                    ModuleName = moduleGroup.Key.ModuleName ?? string.Empty,
                    ModuleTitle = moduleGroup.Key.ModuleTitle ?? string.Empty,
                    Description = moduleGroup.FirstOrDefault()?.Description,
                    Functions = new List<PermissionFunctionDto>()
                };

                var controllerGroups = moduleGroup.GroupBy(c => new { c.ControllerName, c.ControllerTitle });

                foreach (var controllerGroup in controllerGroups)
                {
                    var functionDto = new PermissionFunctionDto
                    {
                        FunctionName = controllerGroup.Key.ControllerName ?? string.Empty,
                        FunctionTitle = controllerGroup.Key.ControllerTitle ?? string.Empty,
                        Description = controllerGroup.FirstOrDefault()?.Description,
                        ActionPermissions = new List<PermissionActionDto>()
                    };

                    var readEntity = controllerGroup.FirstOrDefault(a =>
                        (!string.IsNullOrEmpty(a.PermissionKey) && a.PermissionKey.EndsWith(".READ", StringComparison.OrdinalIgnoreCase)) ||
                        string.Equals(a.PermissionTitle, controllerGroup.Key.ControllerTitle, StringComparison.OrdinalIgnoreCase));

                    if (readEntity != null)
                    {
                        functionDto.ReadPermission = new PermissionActionDto
                        {
                            PermissionKey = readEntity.PermissionKey ?? string.Empty,
                            PermissionTitle = readEntity.PermissionTitle,
                            ActionName = readEntity.ActionName,
                            ActionTitle = readEntity.ActionTitle,
                            Description = readEntity.Description,
                            BitPosition = readEntity.BitPosition
                        };
                    }
                    var actions = controllerGroup.DistinctBy(x => x.PermissionKey).ToList();
                    foreach (var actionEntity in actions)
                    {
                        if (readEntity != null && actionEntity.PermissionKey == readEntity.PermissionKey)
                        {
                            continue;
                        }

                        functionDto.ActionPermissions.Add(new PermissionActionDto
                        {
                            PermissionKey = actionEntity.PermissionKey ?? string.Empty,
                            PermissionTitle = actionEntity.PermissionTitle,
                            ActionName = actionEntity.ActionName,
                            ActionTitle = !string.IsNullOrWhiteSpace(actionEntity.ActionTitle)
                                ? actionEntity.ActionTitle
                                : actionEntity.PermissionTitle,
                            Description = actionEntity.Description,
                            BitPosition = actionEntity.BitPosition
                        });
                    }

                    moduleDto.Functions.Add(functionDto);
                }

                result.Add(moduleDto);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionManagementService] 建立權限樹狀圖時發生錯誤");
            throw;
        }
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
            var globalEffectiveKeys = globalDirectKeys
                .Union(globalRoleKeys, StringComparer.OrdinalIgnoreCase)
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

                    // 區域實驗室功能開放範圍：有效權限不得超出實驗室開放的功能
                    labEffectiveKeys = (await _labFunctionScopeService.FilterAsync(labTenantId, labEffectiveKeys, cancellationToken).ConfigureAwait(false)).ToList();

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
    private sealed record MenuPlacement(string SectionTitle, int SectionOrder, string GroupTitle, int GroupOrder);

    /// <summary>選單頁面入口</summary>
    private sealed record MenuEntry(string Title, string? Route);

    private sealed record MenuLookup(
        Dictionary<string, MenuPlacement> ByController,
        Dictionary<string, MenuEntry> EntriesByKey);

    private static string ControllerKey(string module, string controller) => $"{module}|{controller}".ToUpperInvariant();

    /// <summary>
    /// 依實際選單樹 (Section → Group → Page) 建立權限分組對照：
    /// 選單頁面所屬的功能歸入該頁面所在群組；同一功能的其他動作權限一併歸入，與使用者看到的選單一致
    /// </summary>
    private async Task<MenuLookup> BuildMenuLookupAsync(CancellationToken cancellationToken)
    {
        var items = await _dbContext.MenuItems.AsNoTracking()
            .Where(m => m.IsActive && m.IsVisible)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var byId = items.ToDictionary(m => m.Id);

        var byController = new Dictionary<string, MenuPlacement>(StringComparer.OrdinalIgnoreCase);
        var entries = new Dictionary<string, MenuEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in items.Where(m => m.Type == MenuType.Page).OrderBy(m => m.DisplayOrder))
        {
            var key = !string.IsNullOrWhiteSpace(page.PermissionKey) ? page.PermissionKey! : page.Key;
            entries.TryAdd(key, new MenuEntry(page.DisplayName, page.Route));

            if (!_permissionRegistry.TryGetPermission(key, out var meta) || meta is null) continue;

            MenuItem? group = null;
            if (page.ParentId is { } groupId && byId.TryGetValue(groupId, out var groupNode)) group = groupNode;
            var section = group?.ParentId is { } sectionId && byId.TryGetValue(sectionId, out var s) ? s : null;
            if (group is null) continue;

            byController.TryAdd(ControllerKey(meta.ModuleName, meta.ControllerName), new MenuPlacement(
                section?.DisplayName ?? meta.ModuleTitle ?? meta.ModuleName, section?.DisplayOrder ?? int.MaxValue,
                group.DisplayName, group.DisplayOrder));
        }

        return new MenuLookup(byController, entries);
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
            MenuPlacement? placement = null;
            if (meta is not null) menu?.ByController.TryGetValue(ControllerKey(meta.ModuleName, meta.ControllerName), out placement);
            MenuEntry? entry = null;
            menu?.EntriesByKey.TryGetValue(key, out entry);

            return new PermissionAuditInfoItemDto
            {
                PermissionKey = key,
                Title = title,
                Description = meta?.Description,
                // 無選單入口的功能：以模組 / 功能標題作為分組（排序置後）
                SectionTitle = placement?.SectionTitle ?? meta?.ModuleTitle ?? meta?.ModuleName ?? "其他",
                SectionOrder = placement?.SectionOrder ?? int.MaxValue,
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