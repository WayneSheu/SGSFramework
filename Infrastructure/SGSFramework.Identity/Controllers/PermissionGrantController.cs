using System;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs.PermissionGrants;

namespace SGSFramework.Identity.Controllers.v1;

/// <summary>
/// 跨實驗室角色 BitMask 權限設定控制器
/// </summary>
[ApiController]
[Route("api/v1/permission-grants")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
[Authorize]
[ControllerTitle("實驗室權限配置", Icon = "fa-solid fa-user-lock", Order = 21, Description = "提供管理員維護特定角色在特定實驗室下的動態 BitMask 權限位元組向量")]
[RequiresPermission("SYSTEM.PERMISSION_GRANT")]
public sealed class PermissionGrantController(
    IPermissionGrantService permissionGrantService,
    ILogger<PermissionGrantController> logger) : ApiControllerBase
{
    private readonly IPermissionGrantService _permissionGrantService = permissionGrantService ?? throw new ArgumentNullException(nameof(permissionGrantService));
    private readonly ILogger<PermissionGrantController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// 取得指定角色在指定實驗室的 BitMask 權限矩陣設定
    /// </summary>
    /// <param name="roleId">角色識別碼</param>
    /// <param name="labId">實驗室識別碼</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>角色實驗室權限配置數據集</returns>
    [HttpGet("role/{roleId:guid}/lab/{labId:guid}")]
    [RequiresPermission("SYSTEM.PERMISSION_GRANT.READ")]
    [Function("GetRoleLabPermissions", "檢視實驗室角色權限", Icon = "fa-solid fa-eye", Order = 1, Description = "取得指定角色在指定實驗室的 BitMask 權限矩陣設定")]
    [ProducesResponseType(typeof(RoleLabPermissionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRoleLabPermissions(
        [FromRoute] Guid roleId,
        [FromRoute] Guid labId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _permissionGrantService.GetRoleLabPermissionsAsync(roleId, labId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得角色實驗室權限矩陣時發生異常。RoleId: {RoleId}, LabId: {LabId}", roleId, labId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "取得角色實驗室權限矩陣時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 更新指定角色在特定實驗室的 BitMask 權限向量
    /// </summary>
    /// <param name="roleId">角色識別碼</param>
    /// <param name="labId">實驗室識別碼</param>
    /// <param name="request">更新權限向量請求內容</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>操作結果訊息</returns>
    [HttpPut("role/{roleId:guid}/lab/{labId:guid}")]
    [RequiresPermission("SYSTEM.PERMISSION_GRANT.UPDATE")]
    [Function("UpdateRoleLabPermissions", "更新實驗室角色權限", Icon = "fa-solid fa-floppy-disk", Order = 2, Description = "更新指定角色在特定實驗室的 BitMask 權限位元組向量")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateRoleLabPermissions(
        [FromRoute] Guid roleId,
        [FromRoute] Guid labId,
        [FromBody] UpdateRolePermissionsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var (succeeded, message) = await _permissionGrantService.UpdateRoleLabPermissionsAsync(roleId, labId, request, cancellationToken);
            if (!succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "權限配置更新失敗",
                    Detail = message,
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogInformation("成功更新角色 [{RoleId}] 於實驗室 [{LabId}] 的 BitMask 權限向量。", roleId, labId);
            return Ok(new { message = "權限配置更新成功。" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新角色實驗室權限發生異常。RoleId: {RoleId}, LabId: {LabId}", roleId, labId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "更新角色實驗室權限時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }
}