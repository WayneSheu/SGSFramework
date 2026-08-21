using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;

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
    ILogger<PermissionController> logger) : ApiControllerBase
{
    private readonly IPermissionManagementService _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
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
}