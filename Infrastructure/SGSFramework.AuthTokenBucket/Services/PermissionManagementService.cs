// ==========================================
// 檔案路徑: Application/SGSFramework.AuthTokenBucket/Services/PermissionManagementService.cs
// 架構層級: Application Layer (Service Implementation)
// ==========================================

namespace SGSFramework.AuthTokenBucket.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.AuthTokenBucket.DTOs.PermissionGrants;
using SGSFramework.AuthTokenBucket.DTOs.PermissionTree;
using SGSFramework.AuthTokenBucket.DTOs.RolePermissions;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
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
    private readonly IPermissionBitmaskService _bitmaskService;
    private readonly ILogger<PermissionManagementService<TDbContext>> _logger;

    public PermissionManagementService(
        TDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IRolePermissionRepository rolePermissionRepository,
        IUserPermissionRepository userPermissionRepository,
        IPermissionBitmaskService bitmaskService,
        ILogger<PermissionManagementService<TDbContext>> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _rolePermissionRepository = rolePermissionRepository ?? throw new ArgumentNullException(nameof(rolePermissionRepository));
        _userPermissionRepository = userPermissionRepository ?? throw new ArgumentNullException(nameof(userPermissionRepository));
        _bitmaskService = bitmaskService ?? throw new ArgumentNullException(nameof(bitmaskService));
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

                    foreach (var actionEntity in controllerGroup)
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
            var targetPermissions = request.PermissionKeys?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            Dictionary<string, long> moduleBitmaskDict = await _bitmaskService
                .CalculateModuleBitmasksAsync(targetPermissions, cancellationToken)
                .ConfigureAwait(false);

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
    public async Task<List<PermissionUserDto>> GetUsersByPermissionKeyAsync(
        string permissionKey,
        Guid? tenantLabId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        try
        {
            var normalizedPermissionKey = permissionKey.Trim();

            // 1. 從 PermissionMetadata 取得完整 Metadata 資訊
            var allMeta = await _dbContext.Set<PermissionMetadata>()
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var permissionMeta = allMeta.FirstOrDefault(m =>
                string.Equals(m.PermissionKey, normalizedPermissionKey, StringComparison.OrdinalIgnoreCase));

            if (permissionMeta == null)
            {
                _logger.LogWarning("[PermissionManagementService] 查無權限代碼 [{PermissionKey}] 之元資料。", normalizedPermissionKey);
                return new List<PermissionUserDto>();
            }

            var moduleName = permissionMeta.ModuleName ?? string.Empty;
            var moduleTitle = permissionMeta.ModuleTitle ?? string.Empty;
            var functionTitle = permissionMeta.ControllerTitle ?? string.Empty;
            var permissionTitle = permissionMeta.PermissionTitle ?? string.Empty;
            var permissionDescription = permissionMeta.Description ?? string.Empty;

            var bitPosition = permissionMeta.BitPosition;
            long targetBitmaskFlag = (bitPosition >= 0 && bitPosition < 63) ? (1L << bitPosition) : 0L;

            var resultDict = new Dictionary<string, PermissionUserDto>(StringComparer.OrdinalIgnoreCase);

            // Helper: 統一建立或更正 DTO 屬性值
            PermissionUserDto CreateOrGetUserDto(string userId, string username, string email)
            {
                if (!resultDict.TryGetValue(userId, out var dto))
                {
                    dto = new PermissionUserDto
                    {
                        UserId = userId,
                        Username = username,
                        Email = email,
                        HasDirectPermission = false,
                        ModuleName = moduleName,
                        ModuleTitle = moduleTitle,
                        FunctionTitle = functionTitle,
                        PermissionTitle = permissionTitle,
                        PermissionDescription = permissionDescription,
                        BitPosition = bitPosition,
                        TargetBitmaskFlag = targetBitmaskFlag
                    };
                    resultDict[userId] = dto;
                }
                else
                {
                    dto.ModuleTitle = moduleTitle;
                    dto.FunctionTitle = functionTitle;
                    dto.PermissionTitle = permissionTitle;
                    dto.PermissionDescription = permissionDescription;
                }
                return dto;
            }

            // 2. 驗證 Lab 內部使用者
            HashSet<string>? validLabUserIds = null;
            if (tenantLabId.HasValue && tenantLabId.Value != Guid.Empty)
            {
                var labUserIds = await _dbContext.Set<UserLabMapping>()
                    .AsNoTracking()
                    .Where(m => m.TenantLabId == tenantLabId.Value && m.IsActive)
                    .Select(m => m.UserId.ToString())
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                validLabUserIds = new HashSet<string>(labUserIds, StringComparer.OrdinalIgnoreCase);
                if (validLabUserIds.Count == 0) return new List<PermissionUserDto>();
            }

            // 3. 查詢的角色全域權限 (Role Global Permissions)
            var allRolePermissions = await _dbContext.Set<RoleGlobalPermission>()
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // 修正比對邏輯：優先比對 RoleGlobalPermission 中的 PermissionKey 與 ModuleName 是否符號相符，
            // 或進行模糊匹配（如前綴、後綴匹配），並確保位元遮罩正確命中 targetBitmaskFlag
            var matchingRolePermissions = allRolePermissions
                .Where(r => (string.Equals(r.PermissionKey, moduleName, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(r.PermissionKey, normalizedPermissionKey, StringComparison.OrdinalIgnoreCase) ||
                             normalizedPermissionKey.StartsWith(r.PermissionKey + ".", StringComparison.OrdinalIgnoreCase) ||
                             r.PermissionKey.StartsWith(moduleName + ".", StringComparison.OrdinalIgnoreCase))
                         && (targetBitmaskFlag != 0 && (r.Bitmask & targetBitmaskFlag) != 0))
                .ToList();

            var roleBitmaskMap = matchingRolePermissions.ToDictionary(
                r => r.RoleId,
                r => r.Bitmask,
                StringComparer.OrdinalIgnoreCase);

            if (roleBitmaskMap.Count > 0)
            {
                // 先撈出資料庫中具備這些 RoleId 的角色實體
                var targetRoleIds = roleBitmaskMap.Keys.ToList();
                var matchingRoles = await _roleManager.Roles
                    .AsNoTracking()
                    .Where(r => targetRoleIds.Contains(r.Id.ToString()))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                // 若以 ID 為基準沒查到，則嘗試使用 Role Name 進行防禦性二次比對
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

                    // 取得對應 Bitmask，優先使用 ID，若無則使用 Name
                    if (!roleBitmaskMap.TryGetValue(roleIdStr, out var roleBitmask))
                    {
                        roleBitmaskMap.TryGetValue(roleName, out roleBitmask);
                    }

                    var usersInRole = await _userManager.GetUsersInRoleAsync(roleName).ConfigureAwait(false);
                    foreach (var user in usersInRole)
                    {
                        var userIdStr = user.Id.ToString();
                        if (validLabUserIds != null && !validLabUserIds.Contains(userIdStr)) continue;

                        var userDto = CreateOrGetUserDto(userIdStr, user.UserName ?? string.Empty, user.Email ?? string.Empty);

                        if (!userDto.GrantedByRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase))
                        {
                            userDto.GrantedByRoles.Add(roleName);
                            userDto.RoleBitmaskDetails[roleName] = roleBitmask;
                        }
                    }
                }
            }

            // 4. 查詢直接權限 (Direct User Permissions)
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

                bool hasDirect = targetBitmaskFlag != 0 && (userBitmask & targetBitmaskFlag) != 0;

                if (hasDirect)
                {
                    var userDto = CreateOrGetUserDto(userIdStr, user.UserName ?? string.Empty, user.Email ?? string.Empty);
                    userDto.HasDirectPermission = true;
                    userDto.RawUserBitmask = userBitmask;
                }
            }

            var finalResult = resultDict.Values
                .Where(u => u.HasDirectPermission || (u.GrantedByRoles != null && u.GrantedByRoles.Count > 0))
                .OrderBy(u => u.Username)
                .ToList();

            return finalResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionManagementService] 查詢具備權限 [{PermissionKey}] 的使用者時發生異常。", permissionKey);
            throw;
        }
    }
}