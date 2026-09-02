// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.AuthTokenBucket/Services/PermissionManagementService.cs
// ==========================================

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using System.Security.Claims;
namespace SGSFramework.AuthTokenBucket.Services
{
    /// <summary>
    /// 企業級泛型權限管理服務實作 (支援 Bitmask 與 3 層階層式權限樹)
    /// </summary>
    /// <typeparam name="TContext">資料庫上下文型態</typeparam>
    /// <typeparam name="TRole">Identity 角色型態</typeparam>
    /// <typeparam name="TKey">主鍵型態</typeparam>
    public class PermissionManagementService<TContext, TRole, TKey> : IPermissionManagementService
        where TContext : DbContext
        where TRole : IdentityRole<TKey>
        where TKey : IEquatable<TKey>
    {
        private readonly TContext _dbContext;
        private readonly RoleManager<TRole> _roleManager;
        private readonly IPermissionRegistry _permissionRegistry;
        private readonly ILogger<PermissionManagementService<TContext, TRole, TKey>> _logger;

        public PermissionManagementService(
            TContext dbContext,
            RoleManager<TRole> roleManager,
            IPermissionRegistry permissionRegistry,
            ILogger<PermissionManagementService<TContext, TRole, TKey>> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _permissionRegistry = permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region 基本權限檢查與授權

        public async Task<bool> HasPermissionAsync(string userId, string permissionCode, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);
            ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

            try
            {
                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "檢查使用者 {UserId} 之權限 {PermissionCode} 時發生例外。", userId, permissionCode);
                throw;
            }
        }

        public async Task GrantPermissionToRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
            ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

            try
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                if (role == null)
                {
                    _logger.LogWarning("授予權限失敗，找不到角色 ID: {RoleId}", roleId);
                    return;
                }

                var claims = await _roleManager.GetClaimsAsync(role);
                if (!claims.Any(c => c.Type == "Permission" && c.Value == permissionCode))
                {
                    await _roleManager.AddClaimAsync(role, new Claim("Permission", permissionCode));
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                _logger.LogInformation("成功授予角色 {RoleId} 權限 {PermissionCode}", roleId, permissionCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "授予角色 {RoleId} 權限 {PermissionCode} 時發生例外。", roleId, permissionCode);
                throw;
            }
        }

        public async Task RevokePermissionFromRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
            ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

            try
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                if (role == null)
                {
                    _logger.LogWarning("收回權限失敗，找不到角色 ID: {RoleId}", roleId);
                    return;
                }

                var claims = await _roleManager.GetClaimsAsync(role);
                var targetClaim = claims.FirstOrDefault(c => c.Type == "Permission" && c.Value == permissionCode);
                if (targetClaim != null)
                {
                    await _roleManager.RemoveClaimAsync(role, targetClaim);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                _logger.LogInformation("成功收回角色 {RoleId} 之權限 {PermissionCode}", roleId, permissionCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "收回角色 {RoleId} 之權限 {PermissionCode} 時發生例外。", roleId, permissionCode);
                throw;
            }
        }

        #endregion

        #region IPermissionManagementService 控制器對接實作

        /// <summary>
        /// 取得完整系統與動態模組權限清單 (階層式：Module -> Controller -> Permissions)
        /// </summary>
        public async Task<List<PermissionModuleDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default)
        {
            // 1. 從資料庫讀取已持久化的權限與階層中繼資料
            var metadataList = await _dbContext.Set<PermissionMetadata>()
                .OrderBy(p => p.ModuleName)
                .ThenBy(p => p.ControllerName)
                .ThenBy(p => p.BitPosition)
                .ToListAsync(cancellationToken);

            // 2. 取得即時 DynamicPermissionRegistry 中的最新定義作為標題與描述的來源基準
            var registryPermissions = _permissionRegistry.GetAllPermissions();
            var registryDict = registryPermissions.ToDictionary(p => p.PermissionKey, StringComparer.OrdinalIgnoreCase);

            var moduleGroups = metadataList.GroupBy(p => p.ModuleName);
            var result = new List<PermissionModuleDto>();

            foreach (var moduleGroup in moduleGroups)
            {
                var firstItem = moduleGroup.First();
                string moduleName = moduleGroup.Key;

                // 優先從 Registry 或資料庫中取得 ModuleTitle
                string moduleTitle = !string.IsNullOrEmpty(firstItem.ModuleTitle)
                    ? firstItem.ModuleTitle
                    : moduleName;

                var controllerGroups = moduleGroup.GroupBy(p => p.ControllerName);
                var controllerDtos = new List<PermissionControllerDto>();

                foreach (var ctrlGroup in controllerGroups)
                {
                    var firstCtrlItem = ctrlGroup.First();
                    string controllerName = ctrlGroup.Key;

                    // 優先從資料庫或註冊表取得 ControllerTitle
                    string controllerTitle = !string.IsNullOrEmpty(firstCtrlItem.ControllerTitle)
                        ? firstCtrlItem.ControllerTitle
                        : controllerName;

                    var permissionItems = ctrlGroup.Select(p =>
                    {
                        // 從 DynamicPermissionRegistry 補齊最新的 Description（若有定義）
                        registryDict.TryGetValue(p.PermissionKey, out var regItem);
                        string description = !string.IsNullOrEmpty(regItem?.Description)
                            ? regItem.Description
                            : (!string.IsNullOrEmpty(p.Description) ? p.Description : p.PermissionKey);

                        return new PermissionItemDto
                        {
                            Id = p.Id,
                            PermissionKey = p.PermissionKey,
                            BitPosition = p.BitPosition,
                            ActionTitle = !string.IsNullOrEmpty(p.ActionTitle) ? p.ActionTitle : p.PermissionKey,
                            ActionName = p.ActionName ?? string.Empty,
                            Description = description,
                            ParentId = p.ParentId
                        };
                    }).ToList();

                    controllerDtos.Add(new PermissionControllerDto
                    {
                        ControllerName = controllerName,
                        ControllerTitle = controllerTitle,
                        Permissions = permissionItems
                    });
                }

                result.Add(new PermissionModuleDto
                {
                    ModuleName = moduleName,
                    ModuleTitle = moduleTitle,
                    Controllers = controllerDtos
                });
            }

            return result;
        }

        /// <summary>
        /// 取得指定角色的權限矩陣與 BitPosition 清單
        /// </summary>
        public async Task<RolePermissionMatrixDto?> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

            try
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                if (role == null)
                {
                    _logger.LogWarning("查詢角色權限失敗，找不到角色 ID: {RoleId}", roleId);
                    return null;
                }

                // 1. 取得角色所有已被授予的 Permission Claim 集合
                var claims = await _roleManager.GetClaimsAsync(role);
                var grantedPermissionKeys = claims
                    .Where(c => c.Type == "Permission")
                    .Select(c => c.Value)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // 2. 取得全域註冊清單並比對映射出位元索引集合 (計算 BitPosition)
                var allPermissions = _permissionRegistry.GetAllPermissions();
                var grantedBitPositions = allPermissions
                    .Where(p => grantedPermissionKeys.Contains(p.PermissionKey))
                    .Select(p => p.BitPosition)
                    .ToList();

                return new RolePermissionMatrixDto
                {
                    RoleId = roleId,
                    RoleName = role.Name ?? string.Empty,
                    GrantedPermissionKeys = grantedPermissionKeys.ToList(),
                    GrantedBitPositions = grantedBitPositions
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得角色 {RoleId} 之權限矩陣時發生例外。", roleId);
                throw;
            }
        }

        /// <summary>
        /// 批次更新指定角色的權限關聯
        /// </summary>
        public async Task<(bool Succeeded, string Message)> UpdateRolePermissionsAsync(
            UpdateRolePermissionsRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.RoleId);

            try
            {
                var role = await _roleManager.FindByIdAsync(request.RoleId);
                if (role == null)
                {
                    _logger.LogWarning("更新角色權限失敗，找不到指定 RoleId: {RoleId}", request.RoleId);
                    return (false, $"找不到識別碼為 '{request.RoleId}' 的角色。");
                }

                var existingClaims = await _roleManager.GetClaimsAsync(role);
                var permissionClaims = existingClaims.Where(c => c.Type == "Permission").ToList();

                foreach (var claim in permissionClaims)
                {
                    await _roleManager.RemoveClaimAsync(role, claim);
                }

                if (request.PermissionKeys != null && request.PermissionKeys.Any())
                {
                    foreach (var key in request.PermissionKeys.Distinct())
                    {
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            await _roleManager.AddClaimAsync(role, new Claim("Permission", key));
                        }
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("成功更新角色 {RoleId} 之權限設定", request.RoleId);

                return (true, "角色權限更新成功。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新角色 {RoleId} 之權限設定時發生例外。", request.RoleId);
                throw;
            }
        }

        #endregion
    }
}