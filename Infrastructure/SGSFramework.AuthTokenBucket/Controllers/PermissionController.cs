using GSFramework.AuthTokenBucket.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Controllers.Base;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SGSFramework.AuthTokenBucket.Controllers.v1;



/// <summary>
/// 權限管理控制器
/// </summary>
[ApiController]
[Route("api/v1/permissions")]
[Produces("application/json")]
[Authorize]
[ControllerTitle("權限管理", Icon = "fa-solid fa-shield-halved", Order = 20, Description = "提供系統權限樹狀圖查詢、角色權限矩陣讀取與更新服務")]
public sealed class PermissionController(
    IPermissionManagementService permissionService,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    ILogger<PermissionController> logger) : ApiControllerBase
{
    private readonly IPermissionManagementService _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
    private readonly RoleManager<ApplicationRole> _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
    private readonly UserManager<ApplicationUser> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    private readonly ILogger<PermissionController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// 取得完整系統與動態模組權限清單 (階層式：Module -> Controller -> Permissions)
    /// </summary>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>模組權限樹狀結構清單</returns>
    [HttpGet("tree")]
    [RequiresPermission("SYSTEM_PERMISSION_READ")]
    [Function("GetPermissionTree", "取得模組權限清單", Icon = "fa-solid fa-sitemap", Order = 1, Description = "取得完整系統與動態模組權限清單 (階層式：Module -> Controller -> Permissions)")]
    [ProducesResponseType(typeof(List<PermissionModuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<PermissionModuleDto>>> GetPermissionTree(CancellationToken cancellationToken = default)
    {
        try
        {
            var tree = await _permissionService.GetPermissionTreeAsync(cancellationToken);
            return Ok(tree);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得權限樹狀結構時發生未預期異常。");
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
    /// <param name="roleId">角色識別碼</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>指定角色的權限設定矩陣</returns>
    [HttpGet("role/{roleId}")]
    [RequiresPermission("SYSTEM_PERMISSION_READ")]
    [Function("GetRolePermissions", "取得角色權限清單", Icon = "fa-solid fa-user-shield", Order = 2, Description = "取得指定角色的權限設定清單與 Bitmask 映射矩陣")]
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
    /// <param name="request">角色權限更新請求</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>操作結果訊息</returns>
    [HttpPost("role/update")]
    [RequiresPermission("SYSTEM_PERMISSION_WRITE")]
    [Function("UpdateRolePermissions", "更新角色權限", Icon = "fa-solid fa-user-pen", Order = 3, Description = "更新指定角色的權限關聯配置與 Bitmask 設定")]
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
    /// 取得指定使用者的所有權限總覽（含直接權限與透過角色繼承的有效權限，供資安稽核時察看）
    /// </summary>
    [HttpGet("user/{userId:guid}/audit-permissions")]
    [RequiresPermission("SYSTEM_PERMISSION_AUDIT.READ")]
    [Function("GetUserAuditPermissions", "檢視使用者權限", Icon = "fa-solid fa-user-shield", Order = 3, Description = "取得指定使用者的直接權限與透過角色繼承的有效權限總覽，供資安稽核使用。")]
    [ProducesResponseType(typeof(UserAuditPermissionsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserAuditPermissions(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
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

            var roles = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);

            // 1. 取得使用者個人直接指派的權限
            const string permissionClaimType = "Permission";
            var directPermissions = claims
                .Where(c => c.Type == permissionClaimType)
                .Select(c => c.Value)
                .Distinct()
                .ToList();

            // 2. 透過角色名稱查詢 Role ID，再取得該角色的權限矩陣並進行聯集
            var rolePermissionsList = new List<string>();
            foreach (var roleName in roles)
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role != null)
                {
                    var roleMatrix = await _permissionService.GetRolePermissionsAsync(role.Id.ToString(), cancellationToken);
                    if (roleMatrix?.GrantedPermissionKeys != null)
                    {
                        rolePermissionsList.AddRange(roleMatrix.GrantedPermissionKeys);
                    }
                }
            }

            // 3. 結合直接權限與角色繼承權限成為最終的有效權限清單
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
    /// 取得指定角色的所有成員與權限總覽（供資安稽核時察看）
    /// </summary>
    /// <param name="roleId">角色識別碼或名稱</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>角色成員與權限稽核資料集</returns>
    [HttpGet("role/{roleId}/audit")]
    [RequiresPermission("SYSTEM_PERMISSION_AUDIT.READ")]
    [Function("GetRoleAuditDetails", "檢視角色成員與權限稽核", Icon = "fa-solid fa-users-gear", Order = 4, Description = "取得指定角色的所屬成員清單與對應權限配置，供資安稽核使用。")]
    [ProducesResponseType(typeof(RoleAuditDetailsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRoleAuditDetails(
        [FromRoute] string roleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(roleId);

        try
        {
            // 透過服務取得角色權限設定矩陣
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

            // 取得所有使用者並篩選出屬於該角色的成員清單
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
}