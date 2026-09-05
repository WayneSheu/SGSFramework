using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;

namespace SGSFramework.Identity.Controllers.v1;

/// <summary>
/// 系統角色與 AD 群組映射管理控制器
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/roles")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
[ControllerTitle("角色管理", Icon = "fa-solid fa-user-shield", Order = 20, Description = "提供企業級角色 CRUD、AD 網域群組自動對應與使用者角色授權管理")]
[RequiresPermission("SYSTEM.ROLEMANAGEMENT.READ")]
public sealed class RoleManagementController(
    IRoleManagementService<ApplicationRole, Guid> roleManagementService,
    ILogger<RoleManagementController> logger) : ApiControllerBase
{
    private readonly IRoleManagementService<ApplicationRole, Guid> _roleManagementService = roleManagementService ?? throw new ArgumentNullException(nameof(roleManagementService));
    private readonly ILogger<RoleManagementController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// 取得系統所有角色清單
    /// </summary>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>系統角色清單集合</returns>
    [HttpGet]
    [Function("GetAllRoles", "查詢角色列表", Icon = "fa-solid fa-list", Order = 1, Description = "取得系統所有角色清單，包含角色名稱、描述、建立時間等資訊")]
    [ProducesResponseType(typeof(IEnumerable<ApplicationRole>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.GETALLROLES")]
    public async Task<IActionResult> GetAllRoles(CancellationToken cancellationToken = default)
    {
        try
        {
            var roles = await _roleManagementService.GetAllRolesAsync(cancellationToken);
            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢角色列表時發生未預期異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "查詢角色列表時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 依 Role ID 取得單一角色詳細資訊
    /// </summary>
    /// <param name="roleId">角色識別碼</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>指定角色詳細資料</returns>
    [HttpGet("{roleId}")]
    [Function("GetRoleById", "檢視角色細節", Icon = "fa-solid fa-circle-info", Order = 2, Description = "依 Role ID 取得單一角色詳細資訊，包含角色名稱、描述、建立時間、對應的 AD 群組等資訊")]
    [ProducesResponseType(typeof(ApplicationRole), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.GETROLEBYID")]
    public async Task<IActionResult> GetRoleById([FromRoute] string roleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

        try
        {
            var role = await _roleManagementService.GetRoleByIdAsync(roleId, cancellationToken);
            if (role == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "資源不存在",
                    Detail = $"找不到識別碼為 '{roleId}' 的角色。",
                    Instance = HttpContext.Request.Path
                });
            }

            return Ok(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢角色細節時發生異常。RoleId: {RoleId}", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "查詢角色細節時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 建立新系統角色
    /// </summary>
    /// <param name="request">建立角色請求內容</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>新角色建立結果</returns>
    [HttpPost]
    [Function("CreateRole", "新增角色", Icon = "fa-solid fa-plus", Order = 3, Description = "建立新系統角色，需提供角色名稱與描述")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.CREATEROLE")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var (succeeded, errors) = await _roleManagementService.CreateRoleAsync(request, cancellationToken);
            if (!succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "建立角色失敗",
                    Detail = string.Join("; ", errors ?? Array.Empty<string>()),
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogInformation("成功建立系統角色: {RoleName}", request.RoleName);
            return CreatedAtAction(
                nameof(GetRoleById),
                new { roleId = request.RoleName },
                new { message = "角色建立成功", roleName = request.RoleName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立角色時發生未預期異常。RoleName: {RoleName}", request.RoleName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "建立角色時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 更新角色定義
    /// </summary>
    /// <param name="request">更新角色請求內容</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>操作結果訊息</returns>
    [HttpPut]
    [Function("UpdateRole", "編輯角色", Icon = "fa-solid fa-pen-to-square", Order = 4, Description = "更新角色定義，需提供角色 ID、角色名稱與描述")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.UPDATEROLE")]
    public async Task<IActionResult> UpdateRole([FromBody] UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var (succeeded, errors) = await _roleManagementService.UpdateRoleAsync(request, cancellationToken);
            if (!succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "更新角色失敗",
                    Detail = string.Join("; ", errors ?? Array.Empty<string>()),
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogInformation("角色資料更新成功。RoleId: {RoleId}", request.RoleId);
            return Ok(new { message = "角色資料更新成功。" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新角色時發生異常。RoleId: {RoleId}", request.RoleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "更新角色時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 刪除角色
    /// </summary>
    /// <param name="roleId">角色識別碼</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>無內容結果</returns>
    [HttpDelete("{roleId}")]
    [Function("DeleteRole", "刪除角色", Icon = "fa-solid fa-trash", Order = 5, Description = "刪除角色，需提供角色 ID，刪除後將無法復原")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.DELETEROLE")]
    public async Task<IActionResult> DeleteRole([FromRoute] string roleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

        try
        {
            var (succeeded, errors) = await _roleManagementService.DeleteRoleAsync(roleId, cancellationToken);
            if (!succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "刪除角色失敗",
                    Detail = string.Join("; ", errors ?? Array.Empty<string>()),
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogWarning("角色已成功刪除。RoleId: {RoleId}", roleId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除角色時發生異常。RoleId: {RoleId}", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "刪除角色時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 建立 AD 群組與角色之對應關係
    /// </summary>
    /// <param name="request">AD 群組映射請求內容</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>操作結果訊息</returns>
    [HttpPost("ad-group/map")]
    [Function("MapAdGroupToRole", "映射 AD 群組", Icon = "fa-solid fa-network-wired", Order = 6, Description = "建立 AD 群組與角色之對應關係，需提供角色 ID 與 AD 群組名稱")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.MAPADGROUPTOROLE")]
    public async Task<IActionResult> MapAdGroupToRole([FromBody] MapAdGroupToRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var (succeeded, message) = await _roleManagementService.MapAdGroupToRoleAsync(request, cancellationToken);
            if (!succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "AD 群組映射失敗",
                    Detail = message,
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogInformation("成功建立 AD 群組 [{AdGroup}] 與角色 [{RoleId}] 的對應關係。", request.AdGroupName, request.RoleId);
            return Ok(new { message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "映射 AD 群組時發生異常。AdGroup: {AdGroupName}", request.AdGroupName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "映射 AD 群組時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 解除 AD 群組與角色之對應關係
    /// </summary>
    /// <param name="request">解除 AD 群組映射請求內容</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>操作結果訊息</returns>
    [HttpPost("ad-group/remove")]
    [Function("RemoveAdGroupFromRole", "解除 AD 群組映射", Icon = "fa-solid fa-link-slash", Order = 7, Description = "解除 AD 群組與角色之對應關係，需提供角色 ID 與 AD 群組名稱")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.REMOVEADGROUPTOROLE")]
    public async Task<IActionResult> RemoveAdGroupFromRole([FromBody] RemoveAdGroupFromRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var (succeeded, message) = await _roleManagementService.RemoveAdGroupFromRoleAsync(request, cancellationToken);
            if (!succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "解除 AD 群組映射失敗",
                    Detail = message,
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogInformation("已移除 AD 群組 [{AdGroupName}] 與角色 [{RoleId}] 的對應關係。", request.AdGroupName, request.RoleId);
            return Ok(new { message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解除 AD 群組映射時發生異常。AdGroupName: {AdGroupName}", request.AdGroupName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "解除 AD 群組映射時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 根據 AD 群組同步使用者角色
    /// </summary>
    /// <param name="request">同步使用者 AD 角色請求內容</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>已同步之角色清單與訊息</returns>
    [HttpPost("ad-group/sync")]
    [Function("SyncUserRolesFromAdGroups", "同步 AD 使用者角色", Icon = "fa-solid fa-rotate", Order = 8, Description = "根據使用者所屬的 AD 群組，同步其在系統中的角色，需提供使用者帳號與其 AD 群組清單")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.SYNCUSERROLESFROMADGROUPS")]
    public async Task<IActionResult> SyncUserRolesFromAdGroups([FromBody] SyncUserAdRolesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var (succeeded, syncedRoles, message) = await _roleManagementService.SyncUserRolesFromAdGroupsAsync(request, cancellationToken);
            if (!succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "AD 使用者角色同步失敗",
                    Detail = message,
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogInformation("使用者 [{UserId}] 依 AD 群組同步角色成功。", request.Username);
            return Ok(new { syncedRoles, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步 AD 群組角色時發生異常。UserId: {UserId}", request.Username);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "同步 AD 群組角色時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 手動批次指派使用者角色
    /// </summary>
    /// <param name="request">指派使用者角色請求內容</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>操作結果訊息</returns>
    [HttpPost("user/assign-roles")]
    [Function("AssignUserRoles", "指派使用者角色", Icon = "fa-solid fa-user-tag", Order = 9, Description = "手動批次指派使用者角色，需提供使用者 ID 與角色清單")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.ASSIGNUSERROLES")]
    public async Task<IActionResult> AssignUserRoles([FromBody] AssignUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var (succeeded, message) = await _roleManagementService.AssignUserRolesAsync(request, cancellationToken);
            if (!succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "指派角色失敗",
                    Detail = message,
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogInformation("已成功指派角色予使用者 [{UserId}]。", request.UserId);
            return Ok(new { message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "指派使用者角色時發生異常。UserId: {UserId}", request.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "指派使用者角色時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 依指定角色批次指派多位使用者
    /// </summary>
    [HttpPost("{roleId}/users/batch")]
    [Function("BatchAssignUsersToRole", "批次指派角色使用者", Icon = "fa-solid fa-users-gear", Order = 10, Description = "針對指定角色批次將多位使用者加入或指派關聯")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.BATCHASSIGNUSERSTOROLE")]
    public async Task<IActionResult> BatchAssignUsersToRole(
        [FromRoute] string roleId,
        [FromBody] BatchAssignUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var (succeeded, message, errors) = await _roleManagementService.BatchAssignUsersToRoleAsync(roleId, request, cancellationToken);
            if (!succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "批次指派角色使用者失敗",
                    Detail = message,
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogInformation("批次指派執行完畢。目標角色識別碼: {RoleId}", roleId);
            return Ok(new { message, errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批次指派角色使用者時發生異常。RoleId: {RoleId}", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "批次指派角色使用者時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

}