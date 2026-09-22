// ==========================================
// 檔案路徑: Presentation/SGSFramework.AuthTokenBucket/Controllers/v1/PermissionController.cs
// 架構層級: Presentation Layer (Controller Implementation)
// ==========================================

#nullable enable

using GSFramework.AuthTokenBucket.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.AuthTokenBucket.DTOs.PermissionTree;
using SGSFramework.AuthTokenBucket.DTOs.RolePermissions;
using SGSFramework.AuthTokenBucket.DTOs.UserPermissions;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Controllers.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.AuthTokenBucket.Controllers.v1;

/// <summary>
/// 權限管理控制器
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/permissions")]
[ControllerTitle("權限管理", Icon = "fa-solid fa-shield-halved", Order = 20, Description = "提供系統權限樹狀圖查詢、角色權限矩陣讀取與更新服務")]
[RequiresPermission("SYSTEM.PERMISSION.READ", "權限管理")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public sealed class PermissionController : ApiControllerBase
{
    private readonly IMemoryCache _memoryCache;
    private readonly IPermissionManagementService _permissionService;
    private readonly IPermissionBitmaskService _bitmaskService;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserPermissionRepository _userPermissionRepository;
    private readonly ILogger<PermissionController> _logger;

    public PermissionController(
        IMemoryCache memoryCache,
        IPermissionManagementService permissionService,
        IPermissionBitmaskService bitmaskService,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IUserPermissionRepository userPermissionRepository,
        ILogger<PermissionController> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _bitmaskService = bitmaskService ?? throw new ArgumentNullException(nameof(bitmaskService));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _userPermissionRepository = userPermissionRepository ?? throw new ArgumentNullException(nameof(userPermissionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }


    private const string PermissionTreeCacheKey = "Cache_System_Permission_Tree";

    /// <summary>
    /// 同步權限元數據並回傳 DTO 投影資料清單
    /// </summary>
    [HttpPost("metadata/sync")]
    [Function("SyncPermissionMetadata", "同步權限元數據", Icon = "fa-solid fa-sitemap", Order = 1, Description = "同步權限元數據並回傳 DTO 投影資料清單",IsMenu =true)]
    [RequiresPermission("SYSTEM.PERMISSION.READ", "權限管理")]
    [EndpointSummary("同步權限元數據")]
    [EndpointDescription("同步權限元數據並回傳 DTO 投影資料清單")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncPermissionMetadata(CancellationToken cancellationToken = default)
    {
        try
        {
            var syncedMetadataList = await _permissionService.SyncPermissionMetadataAsync(cancellationToken).ConfigureAwait(false);
            int affectedCount = syncedMetadataList.Count;

            _memoryCache.Remove(PermissionTreeCacheKey);
            _logger.LogInformation("手動觸發同步 PermissionMetadata 成功，影響筆數: {Count}", affectedCount);

            return Ok(new
            {
                success = true,
                message = $"權限元數據同步成功，共更新 {affectedCount} 筆資料。",
                count = affectedCount,
                data = syncedMetadataList
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步 PermissionMetadata 時發生未預期異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "同步權限元數據失敗。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 取得完整系統與動態模組權限清單 (階層式：Section-> Module -> Function -> Action)
    /// </summary>
    [HttpGet("tree")]
    [Function("GetPermissionTree", "系統權限清單", Icon = "fa-solid fa-sitemap", Order = 1, Description = "取得完整系統與動態模組權限清單 (階層式：Module -> Controller -> Permissions)", IsMenu = false)]
    [ProducesResponseType(typeof(List<PermissionModuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.PERMISSION.READ")]
    [EndpointSummary("系統權限清單")]
    [EndpointDescription("取得完整系統與動態模組權限清單 (階層式：Module -> Controller -> Permissions)")]
    public async Task<IActionResult> GetPermissionTree(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var tree = await _memoryCache.GetOrCreateAsync(PermissionTreeCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.Priority = CacheItemPriority.High;

                _logger.LogInformation("重新載入系統權限樹狀結構至記憶體快取。");
                var data = await _permissionService.GetPermissionTreeAsync(cancellationToken);
                return data ?? new List<PermissionModuleDto>();
            });

            stopwatch.Stop();
            _logger.LogDebug("取得權限樹狀結構耗時: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

            return Ok(tree ?? new List<PermissionModuleDto>());
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "取得權限樹狀結構時發生未預期異常。耗時: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "無法取得權限樹狀結構。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 取得指定角色的權限設定清單
    /// </summary>
    [HttpGet("role/{roleId}")]
    [Function("GetRolePermissions", "角色權限清單", Icon = "fa-solid fa-user-shield", Order = 2, Description = "取得指定角色的權限設定清單與 Bitmask 映射矩陣", IsMenu = false)]
    [RequiresPermission("SYSTEM.PERMISSION.READ")]
    [EndpointSummary("角色權限清單")]
    [EndpointDescription("取得指定角色的權限設定清單與 Bitmask 映射矩陣。")]
    [ProducesResponseType(typeof(RolePermissionMatrixDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RolePermissionMatrixDto>> GetRolePermissions(
        [FromRoute] string roleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(roleId);

        try
        {
            var result = await _permissionService.GetRolePermissionsAsync(roleId, cancellationToken);
            if (result == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "資源不存在",
                    Detail = $"找不到指定 RoleId: {roleId} 的權限配置資訊。",
                    Instance = HttpContext.Request.Path
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得角色 {RoleId} 的權限配置時發生異常。", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "無法取得角色權限資料。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 更新指定角色的權限關聯
    /// </summary>
    [HttpPost("role/update")]
    [Function("UpdateRolePermissions", "更新角色權限", Icon = "fa-solid fa-user-pen", Order = 3, Description = "更新指定角色的權限關聯配置與 Bitmask 設定", IsMenu = false)]
    [RequiresPermission("SYSTEM.PERMISSION.UPDATE", "更新權限")]
    [EndpointSummary("更新角色權限")]
    [EndpointDescription("更新指定角色的權限關聯配置與 Bitmask 設定")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateRolePermissions(
        [FromBody] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RoleId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "請求參數無效",
                Detail = "RoleId 不得為空。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var (succeeded, message) = await _permissionService.UpdateRolePermissionsAsync(request, cancellationToken);
            if (!succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "權限更新失敗",
                    Detail = message,
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogInformation("成功更新角色 {RoleId} 的權限配置。", request.RoleId);
            return Ok(new { message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新角色 {RoleId} 權限時發生未預期異常。", request.RoleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "更新角色權限程序執行失敗。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 取得指定使用者的所有權限總覽 (包含從 User_Global_Permissions Bitmask 解碼之直接權限)
    /// </summary>
    [HttpGet("user/{userId:guid}/audit-permissions")]
    [Function("GetUserAllPermissions", "檢視使用者權限", Icon = "fa-solid fa-user-shield", Order = 4, Description = "取得指定使用者的直接權限與透過角色繼承的有效權限總覽，供資安稽核使用。", IsMenu = false)]
    [RequiresPermission("SYSTEM.PERMISSION.READ")]
    [EndpointSummary("檢視使用者權限")]
    [EndpointDescription("取得指定使用者的直接權限與透過角色繼承的有效權限總覽，供資安稽核與彈出對話框回顯使用。")]
    [ProducesResponseType(typeof(UserAuditPermissionsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserAllPermissions(
        [FromRoute] Guid userId,
        [FromQuery] Guid? tenantLabId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
            if (user == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "使用者不存在",
                    Detail = $"找不到識別碼為 '{userId}' 的使用者。",
                    Instance = HttpContext.Request.Path
                });
            }

            // 1. 讀取使用者角色與 Claims
            var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
            var claims = await _userManager.GetClaimsAsync(user).ConfigureAwait(false);

            const string permissionClaimType = "Permission";
            var claimPermissions = claims
                .Where(c => c.Type == permissionClaimType)
                .Select(c => c.Value)
                .ToList();

            // 2. 從資料庫讀取使用者的直接 Bitmask 設定並進行還解碼
            Dictionary<string, long> dbBitmaskMap;
            if (tenantLabId.HasValue && tenantLabId.Value != Guid.Empty)
            {
                dbBitmaskMap = await _userPermissionRepository.GetPermissionsByLabAsync(
                    userId.ToString(),
                    tenantLabId.Value,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                dbBitmaskMap = await _userPermissionRepository.GetGlobalPermissionsAsync(
                    userId.ToString(),
                    cancellationToken).ConfigureAwait(false);
            }

            var decodedDirectPermissions = await _bitmaskService.DecodeBitmaskToPermissionsAsync(dbBitmaskMap, cancellationToken).ConfigureAwait(false);

            // 合併 Identity Claim Permissions 與資料庫 Bitmask 解碼出的權限
            var directPermissions = claimPermissions
                .Union(decodedDirectPermissions, StringComparer.OrdinalIgnoreCase)
                .Distinct()
                .ToList();

            // 3. 取得角色繼承權限
            var rolePermissionsList = new List<string>();
            foreach (var roleName in roles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var role = await _roleManager.FindByNameAsync(roleName).ConfigureAwait(false);
                if (role != null)
                {
                    var roleMatrix = await _permissionService.GetRolePermissionsAsync(role.Id.ToString(), cancellationToken).ConfigureAwait(false);
                    if (roleMatrix?.GrantedPermissionKeys is { Count: > 0 })
                    {
                        rolePermissionsList.AddRange(roleMatrix.GrantedPermissionKeys);
                    }
                }
            }

            // 4. 彙整有效權限 (Direct + Role)
            var effectivePermissions = directPermissions
                .Union(rolePermissionsList, StringComparer.OrdinalIgnoreCase)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            var response = new UserAuditPermissionsResponseDto
            {
                UserId = user.Id.ToString(),
                Username = user.UserName ?? string.Empty,
                Roles = roles.ToList(),
                DirectPermissions = directPermissions,
                EffectivePermissions = effectivePermissions
            };

            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("查詢使用者權限稽核資料作業已取消。UserId: {UserId}", userId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢使用者權限稽核資料時發生異常。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "查詢使用者權限稽核資料時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }


    /// <summary>
    /// 取得指定角色的所有成員與權限總覽
    /// </summary>
    [HttpGet("role/{roleId}/audit")]
    [Function("GetRoleMemberPermissions", "檢視角色的成員與權限", Icon = "fa-solid fa-users-gear", Order = 5, Description = "取得指定角色的所屬成員清單與對應權限配置，供資安稽核使用。", IsMenu = false)]
    [RequiresPermission("SYSTEM.PERMISSION.READ")]
    [EndpointSummary("檢視角色的成員與權限")]
    [EndpointDescription("取得指定角色的所屬成員清單與對應權限配置，供資安稽核使用。")]
    [ProducesResponseType(typeof(RoleAuditDetailsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRoleMemberPermissions(
        [FromRoute] string roleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(roleId);

        try
        {
            var rolePermissions = await _permissionService.GetRolePermissionsAsync(roleId, cancellationToken);
            if (rolePermissions == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "資源不存在",
                    Detail = $"找不到指定 RoleId: {roleId} 的權限配置資訊。",
                    Instance = HttpContext.Request.Path
                });
            }

            string targetRoleName = rolePermissions.RoleName ?? roleId;
            var members = new List<RoleMemberDto>();

            var users = await _userManager.Users.ToListAsync(cancellationToken);
            foreach (var user in users)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var userRoles = await _userManager.GetRolesAsync(user);

                if (userRoles.Contains(targetRoleName, StringComparer.OrdinalIgnoreCase) || userRoles.Contains(roleId))
                {
                    members.Add(new RoleMemberDto
                    {
                        UserId = user.Id.ToString(),
                        Username = user.UserName ?? string.Empty,
                        Email = user.Email ?? string.Empty
                    });
                }
            }

            var response = new RoleAuditDetailsResponseDto
            {
                RoleId = roleId,
                RoleName = targetRoleName,
                Members = members,
                Permissions = rolePermissions
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢角色 {RoleId} 的稽核成員與權限時發生異常。", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "查詢角色稽核資料時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 指派/更新指定使用者的直接 API 權限清單
    /// </summary>
    [HttpPut("user/{userId:guid}/permissions")]
    [Function("AssignUserPermissions", "指派使用者權限", Icon = "fa-solid fa-key", Order = 6, Description = "更新指定使用者的直接 API 權限", IsMenu = false)]
    [RequiresPermission("SYSTEM.PERMISSION.UPDATE")]
    [EndpointSummary("指派使用者權限")]
    [EndpointDescription("更新指定使用者的直接 API 權限。")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AssignUserPermissions(
        [FromRoute] Guid userId,
        [FromQuery] Guid? tenantLabId,
        [FromBody] AssignUserPermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
            if (user == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "使用者不存在",
                    Detail = $"找不到識別碼為 '{userId}' 的使用者。",
                    Instance = HttpContext.Request.Path
                });
            }

            var targetPermissions = request.Permissions?.Distinct().ToList() ?? new List<string>();

            // 呼叫獨立抽離的 Bitmask 計算服務
            var moduleBitmaskDict = await _bitmaskService.CalculateModuleBitmasksAsync(targetPermissions, cancellationToken).ConfigureAwait(false);

            bool success;
            if (tenantLabId.HasValue && tenantLabId.Value != Guid.Empty)
            {
                success = await _userPermissionRepository.SaveUserLabPermissionsAsync(
                    userId.ToString(),
                    tenantLabId.Value,
                    moduleBitmaskDict,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                success = await _userPermissionRepository.SaveUserGlobalPermissionsAsync(
                    userId.ToString(),
                    moduleBitmaskDict,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!success)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "權限指派失敗",
                    Detail = "將使用者權限寫入資料庫時發生錯誤，請稍後再試。",
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogInformation("成功更新使用者 [{UserId}] 的直接權限遮罩，影響模組數: [{Count}]", userId, moduleBitmaskDict.Count);
            return Ok(new { message = "使用者直接權限指派成功。" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("指派使用者權限作業已取消。UserId: {UserId}", userId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新使用者直接權限時發生未預期異常。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "更新使用者直接權限時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }
}