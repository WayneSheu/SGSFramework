#nullable enable

namespace SGSFramework.Identity.Controllers.v1;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;

/// <summary>
/// 系統角色與 AD 群組映射管理控制器
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/roles")]
[ControllerTitle("角色管理", Icon = "fa-solid fa-user-shield", Order = 20, Description = "提供企業級角色 CRUD、AD 網域群組自動對應與使用者角色授權管理")]
[RequiresPermission("SYSTEM.ROLEMANAGEMENT.READ", "角色管理")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public sealed class RoleManagementController : ApiControllerBase
{
    private readonly IRoleManagementService<ApplicationRole, Guid> _roleManagementService;
    private readonly ILogger<RoleManagementController> _logger;

    public RoleManagementController(
        IRoleManagementService<ApplicationRole, Guid> roleManagementService,
        ILogger<RoleManagementController> logger)
    {
        ArgumentNullException.ThrowIfNull(roleManagementService);
        ArgumentNullException.ThrowIfNull(logger);

        _roleManagementService = roleManagementService;
        _logger = logger;
    }

    /// <summary>
    /// 取得系統所有角色清單
    /// </summary>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>系統角色清單集合</returns>
    [HttpGet]
    [Function("GetAllRoles", "查詢角色列表", Icon = "fa-solid fa-list", Order = 1, Description = "取得系統所有角色清單，包含角色名稱、描述、建立時間等資訊", IsMenu = true)]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.READ")]
    [EndpointSummary("查詢角色列表")]
    [EndpointDescription("取得系統所有角色清單，包含角色名稱、描述、建立時間等資訊。")]
    [ProducesResponseType(typeof(IEnumerable<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllRoles(CancellationToken cancellationToken = default)
    {
        var roles = await _roleManagementService.GetAllRolesAsync(cancellationToken);

        var roleDtos = roles.Select(r => new RoleDto
        {
            Id = r.Id.ToString(),
            Name = r.Name ?? string.Empty,
            Description = r.Description ?? string.Empty,
            MappedAdGroups = r.MappedAdGroups ?? new List<string>()
        }).ToList();

        return HandleResult(Result.Success(roleDtos));
    }

    /// <summary>
    /// 依 Role ID 取得單一角色詳細資訊
    /// </summary>
    /// <param name="roleId">角色識別碼</param>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>指定角色詳細資料</returns>
    [HttpGet("{roleId}")]
    [Function("GetRoleById", "檢視角色細節", Icon = "fa-solid fa-circle-info", Order = 2, Description = "依 Role ID 取得單一角色詳細資訊，包含角色名稱、描述、建立時間、對應的 AD 群組等資訊")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.READ")]
    [EndpointSummary("檢視角色細節")]
    [EndpointDescription("依據 Role ID 取得單一角色的詳細完整設定。")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRoleById([FromRoute] string roleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return HandleResult(Result.Failure(Error.Validation("Role.InvalidId", "角色識別碼不得為空。")));
        }

        var role = await _roleManagementService.GetRoleByIdAsync(roleId, cancellationToken);
        if (role == null)
        {
            return HandleResult(Result.Failure(Error.NotFound("Role.NotFound", $"找不到識別碼為 '{roleId}' 的角色。")));
        }

        var dto = new RoleDto
        {
            Id = role.Id.ToString(),
            Name = role.Name ?? string.Empty,
            Description = role.Description ?? string.Empty,
            MappedAdGroups = role.MappedAdGroups ?? new List<string>()
        };

        return HandleResult(Result.Success(dto));
    }

    /// <summary>
    /// 建立新系統角色
    /// </summary>
    /// <param name="request">建立角色請求內容</param>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>新角色建立結果</returns>
    [HttpPost]
    [Function("CreateRole", "新增角色", Icon = "fa-solid fa-plus", Order = 3, Description = "建立新系統角色，需提供角色名稱與描述")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.CREATE", "新增角色")]
    [EndpointSummary("新增角色")]
    [EndpointDescription("建立新系統角色，配置角色名稱與說明。")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (succeeded, errors) = await _roleManagementService.CreateRoleAsync(request, cancellationToken);
        if (!succeeded)
        {
            var errorDetail = string.Join("; ", errors ?? Array.Empty<string>());
            return HandleResult(Result.Failure(Error.Validation("Role.CreateFailed", errorDetail)));
        }

        _logger.LogInformation("成功建立系統角色: {RoleName}", request.RoleName);

        return CreatedAtAction(
            nameof(GetRoleById),
            new { roleId = request.RoleName },
            new { message = "角色建立成功", roleName = request.RoleName });
    }

    /// <summary>
    /// 更新角色定義
    /// </summary>
    /// <param name="request">更新角色請求內容</param>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>操作結果訊息</returns>
    [HttpPut]
    [Function("UpdateRole", "編輯角色", Icon = "fa-solid fa-pen-to-square", Order = 4, Description = "更新角色定義，需提供角色 ID、角色名稱與描述")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.UPDATE", "編輯角色")]
    [EndpointSummary("編輯角色")]
    [EndpointDescription("更新指定角色的定義與基本描述資料。")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateRole([FromBody] UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (succeeded, errors) = await _roleManagementService.UpdateRoleAsync(request, cancellationToken);
        if (!succeeded)
        {
            var errorDetail = string.Join("; ", errors ?? Array.Empty<string>());
            return HandleResult(Result.Failure(Error.Validation("Role.UpdateFailed", errorDetail)));
        }

        _logger.LogInformation("角色資料更新成功。RoleId: {RoleId}", request.RoleId);
        return HandleResult(Result.Success(new { message = "角色資料更新成功。" }));
    }

    /// <summary>
    /// 刪除角色
    /// </summary>
    /// <param name="roleId">角色識別碼</param>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>無內容結果</returns>
    [HttpDelete("{roleId}")]
    [Function("DeleteRole", "刪除角色", Icon = "fa-solid fa-trash", Order = 5, Description = "刪除角色，需提供角色 ID，刪除後將無法復原")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.DELETE", "刪除角色")]
    [EndpointSummary("刪除角色")]
    [EndpointDescription("根據 Role ID 刪除指定角色，此操作不可逆。")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteRole([FromRoute] string roleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return HandleResult(Result.Failure(Error.Validation("Role.InvalidId", "角色識別碼不得為空。")));
        }

        var (succeeded, errors) = await _roleManagementService.DeleteRoleAsync(roleId, cancellationToken);
        if (!succeeded)
        {
            var errorDetail = string.Join("; ", errors ?? Array.Empty<string>());
            return HandleResult(Result.Failure(Error.Validation("Role.DeleteFailed", errorDetail)));
        }

        _logger.LogWarning("角色已成功刪除。RoleId: {RoleId}", roleId);
        return NoContent();
    }

    /// <summary>
    /// 建立 AD 群組與角色之對應關係
    /// </summary>
    /// <param name="request">AD 群組映射請求內容</param>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>操作結果訊息</returns>
    [HttpPost("ad-group/map")]
    [Function("MapAdGroupToRole", "映射 AD 群組", Icon = "fa-solid fa-network-wired", Order = 6, Description = "建立 AD 群組與角色之對應關係，需提供角色 ID 與 AD 群組名稱")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.UPDATE", "編輯角色")]
    [EndpointSummary("映射 AD 群組")]
    [EndpointDescription("設定指定 Active Directory 群組與系統角色的自動對應關聯。")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MapAdGroupToRole([FromBody] MapAdGroupToRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (succeeded, message) = await _roleManagementService.MapAdGroupToRoleAsync(request, cancellationToken);
        if (!succeeded)
        {
            return HandleResult(Result.Failure(Error.Validation("Role.AdMapFailed", message)));
        }

        _logger.LogInformation("成功建立 AD 群組 [{AdGroup}] 與角色 [{RoleId}] 的對應關係。", request.AdGroupName, request.RoleId);
        return HandleResult(Result.Success(new { message }));
    }

    /// <summary>
    /// 解除 AD 群組與角色之對應關係
    /// </summary>
    /// <param name="request">解除 AD 群組映射請求內容</param>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>操作結果訊息</returns>
    [HttpPost("ad-group/remove")]
    [Function("RemoveAdGroupFromRole", "解除 AD 群組映射", Icon = "fa-solid fa-link-slash", Order = 7, Description = "解除 AD 群組與角色之對應關係，需提供角色 ID 與 AD 群組名稱")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.UPDATE", "編輯角色")]
    [EndpointSummary("解除 AD 群組映射")]
    [EndpointDescription("移除指定 AD 群組與系統角色之間的綁定關係。")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveAdGroupFromRole([FromBody] RemoveAdGroupFromRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (succeeded, message) = await _roleManagementService.RemoveAdGroupFromRoleAsync(request, cancellationToken);
        if (!succeeded)
        {
            return HandleResult(Result.Failure(Error.Validation("Role.AdRemoveFailed", message)));
        }

        _logger.LogInformation("已移除 AD 群組 [{AdGroupName}] 與角色 [{RoleId}] 的對應關係。", request.AdGroupName, request.RoleId);
        return HandleResult(Result.Success(new { message }));
    }

    /// <summary>
    /// 根據 AD 群組同步使用者角色
    /// </summary>
    /// <param name="request">同步使用者 AD 角色請求內容</param>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>已同步之角色清單與訊息</returns>
    [HttpPost("ad-group/sync")]
    [Function("SyncUserRolesFromAdGroups", "同步 AD 使用者角色", Icon = "fa-solid fa-rotate", Order = 8, Description = "根據使用者所屬的 AD 群組，同步其在系統中的角色，需提供使用者帳號與其 AD 群組清單")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.UPDATE", "編輯角色")]
    [EndpointSummary("同步 AD 使用者角色")]
    [EndpointDescription("根據使用者傳入的 AD 群組權限，自動計算並更新系統中的角色配置。")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncUserRolesFromAdGroups([FromBody] SyncUserAdRolesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (succeeded, syncedRoles, message) = await _roleManagementService.SyncUserRolesFromAdGroupsAsync(request, cancellationToken);
        if (!succeeded)
        {
            return HandleResult(Result.Failure(Error.Validation("Role.AdSyncFailed", message)));
        }

        _logger.LogInformation("使用者 [{Username}] 依 AD 群組同步角色成功。", request.Username);
        return HandleResult(Result.Success(new { syncedRoles, message }));
    }

    /// <summary>
    /// 手動指派使用者角色
    /// </summary>
    /// <param name="userId">使用者識別碼（從路由取得）</param>
    /// <param name="request">指派角色請求內容</param>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>操作結果訊息</returns>
    [HttpPut("users/{userId}/roles")]
    [Function("AssignUserRoles", "使用者歸屬角色", Icon = "fa-solid fa-user-tag", Order = 9, Description = "手動指派指定使用者的系統角色清單")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.UPDATE", "編輯角色")]
    [EndpointSummary("使用者歸屬角色")]
    [EndpointDescription("針對單一指定使用者進行多角色歸屬。")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AssignUserRoles(
        [FromRoute] string userId,
        [FromBody] AssignUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return HandleResult(Result.Failure(Error.Validation("User.InvalidId", "使用者識別碼不得為空。")));
        }
        ArgumentNullException.ThrowIfNull(request);

        var (succeeded, message) = await _roleManagementService.AssignUserRolesAsync(userId, request, cancellationToken);
        if (!succeeded)
        {
            return HandleResult(Result.Failure(Error.Validation("Role.AssignFailed", message)));
        }

        _logger.LogInformation("已成功指派角色予使用者 [{UserId}]。", userId);
        return HandleResult(Result.Success(new { message }));
    }

    /// <summary>
    /// 依指定角色批次指派多位使用者
    /// </summary>
    /// <param name="roleId">角色識別碼</param>
    /// <param name="request">批次指派使用者請求內容</param>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>批次指派結果與錯誤細節</returns>
    [HttpPost("{roleId}/users/batch")]
    [Function("BatchAssignUsersToRole", "角色指派使用者", Icon = "fa-solid fa-users-gear", Order = 10, Description = "針對指定角色批次將多位使用者加入或指派關聯")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.UPDATE", "編輯角色")]
    [EndpointSummary("角色指派使用者")]
    [EndpointDescription("將指定的角色指派多個使用者帳號。")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> BatchAssignUsersToRole(
        [FromRoute] string roleId,
        [FromBody] BatchAssignUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return HandleResult(Result.Failure(Error.Validation("Role.InvalidId", "角色識別碼不得為空。")));
        }
        ArgumentNullException.ThrowIfNull(request);

        var (succeeded, message, errors) = await _roleManagementService.BatchAssignUsersToRoleAsync(roleId, request, cancellationToken);
        if (!succeeded)
        {
            return HandleResult(Result.Failure(Error.Validation("Role.BatchAssignFailed", message)));
        }

        _logger.LogInformation("批次指派執行完畢。目標角色識別碼: {RoleId}", roleId);
        return HandleResult(Result.Success(new { message, errors }));
    }
}