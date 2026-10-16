// ==========================================
// 檔案路徑: src/Presentation/SGSFramework.Identity/Controllers/v1/RoleManagementController.cs
// 架構層級: Presentation / API Controller Layer
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Controllers.V1;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Paginations;
using SGSFramework.Core.Results;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;
using SGSFramework.Identity.DTOs.Roles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 企業級系統角色與 AD 群組映射管理控制器 (對齊 UserManagementController 標準與 ARBAC 檢核)
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/roles")]
[ControllerTitle("角色管理", Icon = "fa-solid fa-user-shield", Order = 20, Description = "提供企業級角色 CRUD、狀態管理、AD 網域群組自動對應與批次使用者角色授權管理")]
[RequiresPermission("SYSTEM.ROLEMANAGEMENT.READ", "角色管理")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public sealed class RoleManagementController(
    IRoleManagementService<ApplicationRole, Guid> roleManagementService,
    ILogger<RoleManagementController> logger) : ApiControllerBase
{
    private readonly IRoleManagementService<ApplicationRole, Guid> _roleManagementService = roleManagementService ?? throw new ArgumentNullException(nameof(roleManagementService));
    private readonly ILogger<RoleManagementController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// 取得系統所有角色清單（自動過濾軟刪除項目）
    /// </summary>
    [HttpGet]
    [Function("GetAllRoles", "查詢角色列表", Icon = "fa-solid fa-list", Order = 1, Description = "取得系統所有有效角色清單，包含角色名稱、代碼、描述、階層數值與系統角色標記等資訊", IsMenu = true)]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.READ")]
    [EndpointSummary("查詢角色列表")]
    [EndpointDescription("取得系統所有有效角色清單，包含角色名稱、代碼、描述、階層數值與系統角色標記等資訊。")]
    [ProducesResponseType(typeof(Result<List<RoleDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllRoles(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var roles = await _roleManagementService.GetAllRolesAsync(cancellationToken).ConfigureAwait(false);
            return HandleResult(Result.Success(roles));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[RoleManagementController] 查詢角色列表作業已被用戶端取消。");
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoleManagementController] 查詢角色列表時發生系統異常。");
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
    /// 依據 ID 取得單一角色詳細資訊
    /// </summary>
    [HttpGet("{id:guid}")]
    [Function("GetRoleById", "檢視角色細節", Icon = "fa-solid fa-circle-info", Order = 2, Description = "依據唯一識別碼讀取單一角色的詳細完整設定與權限清單")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.READ")]
    [EndpointSummary("檢視角色細節")]
    [EndpointDescription("依據唯一識別碼讀取單一角色的詳細完整設定與權限清單。")]
    [ProducesResponseType(typeof(Result<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRoleById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求參數",
                Detail = "必須提供有效的角色識別碼。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var role = await _roleManagementService.GetRoleByIdAsync(id.ToString(), cancellationToken).ConfigureAwait(false);
            if (role == null)
            {
                return HandleResult(Result.Failure<RoleDto>(Error.NotFound("Role.NotFound", $"找不到識別碼為 '{id}' 的角色。")));
            }

            return HandleResult(Result.Success(role));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得角色詳情時發生異常。RoleId: {RoleId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "讀取角色詳細資料時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 建立新系統角色
    /// </summary>
    [HttpPost]
    [Function("CreateRole", "新增角色", Icon = "fa-solid fa-plus", Order = 3, Description = "建立新系統角色，配置角色名稱、代碼、階層數值與說明")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.CREATE", "新增角色")]
    [EndpointSummary("新增角色")]
    [EndpointDescription("建立新系統角色，配置角色名稱、代碼、階層數值與說明。")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateRole(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var (succeeded, errors) = await _roleManagementService.CreateRoleAsync(request, cancellationToken).ConfigureAwait(false);
            if (!succeeded)
            {
                string errorDetail = string.Join("; ", errors ?? Enumerable.Empty<string>());
                return HandleResult(Result.Failure(Error.Validation("Role.CreateFailed", errorDetail)));
            }

            _logger.LogInformation("成功建立系統角色: {RoleName}", request.RoleName);
            return HandleResult(Result.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立角色時發生未預期異常。RoleName: {RoleName}", request.RoleName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "建立角色作業時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 更新角色定義
    /// </summary>
    [HttpPut("{id:guid}")]
    [Function("UpdateRole", "編輯角色", Icon = "fa-solid fa-pen-to-square", Order = 4, Description = "更新指定角色的定義與基本描述資料")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.UPDATE", "編輯角色")]
    [EndpointSummary("編輯角色")]
    [EndpointDescription("更新指定角色的定義與基本描述資料。")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateRole(
        [FromRoute] Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (id == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求參數",
                Detail = "必須提供有效的角色識別碼。",
                Instance = HttpContext.Request.Path
            });
        }

        request.RoleId = id.ToString();

        try
        {
            var (succeeded, errors) = await _roleManagementService.UpdateRoleAsync(request, cancellationToken).ConfigureAwait(false);
            if (!succeeded)
            {
                string errorDetail = string.Join("; ", errors ?? Enumerable.Empty<string>());
                return HandleResult(Result.Failure(Error.Validation("Role.UpdateFailed", errorDetail)));
            }

            _logger.LogInformation("角色資料更新成功。RoleId: {RoleId}", id);
            return HandleResult(Result.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新角色資料時發生異常。RoleId: {RoleId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "更新角色作業時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 變更角色啟用/停用/鎖定狀態
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Function("UpdateRoleStatus", "切換角色狀態", Icon = "fa-solid fa-user-lock", Order = 5, Description = "啟用、停用或鎖定指定的系統角色權限")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.UPDATE", "編輯角色")]
    [EndpointSummary("切換角色狀態")]
    [EndpointDescription("啟用、停用或鎖定指定的系統角色權限。")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateRoleStatus(
        [FromRoute] Guid id,
        [FromQuery] int status,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求參數",
                Detail = "必須提供有效的角色識別碼。",
                Instance = HttpContext.Request.Path
            });
        }

        if (!Enum.IsDefined(typeof(RoleStatus), status))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求參數",
                Detail = "無效的角色狀態數值。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var (succeeded, errors) = await _roleManagementService.UpdateRoleStatusAsync(id, (RoleStatus)status, cancellationToken).ConfigureAwait(false);
            if (!succeeded)
            {
                string errorDetail = string.Join("; ", errors ?? Enumerable.Empty<string>());
                return HandleResult(Result.Failure(Error.Validation("Role.StatusFailed", errorDetail)));
            }

            _logger.LogInformation("成功切換角色狀態。RoleId: {RoleId}, Status: {Status}", id, status);
            return HandleResult(Result.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切換角色狀態時發生異常。RoleId: {RoleId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "切換角色狀態作業時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 批次將使用者指派至指定角色（支援 ARBAC 驗證與分散式交易一致性）
    /// </summary>
    [HttpPost("{idOrName}/users/batch")]
    [Function("BatchAssignUsersToRole", "批次指派使用者角色", Icon = "fa-solid fa-users-gear", Order = 6, Description = "批次將多位使用者加入至指定的系統角色中")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.UPDATE", "編輯角色")]
    [EndpointSummary("批次指派使用者角色")]
    [EndpointDescription("批次將多位使用者加入至指定的系統角色中。")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> BatchAssignUsersToRole(
        [FromRoute] string idOrName,
        [FromBody] BatchAssignUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrName);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (succeeded, message, errors) = await _roleManagementService.BatchAssignUsersToRoleAsync(idOrName, request, cancellationToken).ConfigureAwait(false);
            if (!succeeded)
            {
                string errorDetail = errors != null && errors.Any() ? string.Join("; ", errors) : message;
                return HandleResult(Result.Failure(Error.Validation("Role.BatchAssignFailed", errorDetail)));
            }

            _logger.LogInformation("成功批次指派使用者至角色: {RoleIdentifier}", idOrName);
            return HandleResult(Result.Success(true));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[RoleManagementController] 批次指派使用者角色作業已被用戶端取消。");
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批次指派使用者角色時發生異常。RoleIdentifier: {RoleIdentifier}", idOrName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "批次指派使用者角色時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 軟刪除指定角色
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Function("DeleteRole", "刪除角色", Icon = "fa-solid fa-trash", Order = 7, Description = "根據 Role ID 進行軟刪除指定角色並進行防呆保護")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.DELETE", "刪除角色")]
    [EndpointSummary("刪除角色")]
    [EndpointDescription("根據 Role ID 進行軟刪除指定角色並進行防呆保護。")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteRole(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求參數",
                Detail = "必須提供有效的角色識別碼。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var (succeeded, errors) = await _roleManagementService.DeleteRoleAsync(id.ToString(), cancellationToken).ConfigureAwait(false);
            if (!succeeded)
            {
                string errorDetail = string.Join("; ", errors ?? Enumerable.Empty<string>());
                return HandleResult(Result.Failure(Error.Validation("Role.DeleteFailed", errorDetail)));
            }

            _logger.LogWarning("角色已成功軟刪除。RoleId: {RoleId}", id);
            return HandleResult(Result.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除角色時發生異常。RoleId: {RoleId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "執行刪除角色作業時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }
}