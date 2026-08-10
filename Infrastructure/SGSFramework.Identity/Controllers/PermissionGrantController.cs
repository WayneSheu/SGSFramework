using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs.PermissionGrants;
using System.ComponentModel;
using System.Net.Mime;

namespace SGSFramework.Identity.Controllers
{
    /// <summary>
    /// 跨實驗室角色 BitMask 權限設定控制器
    /// </summary>
    [ApiController]
    [Route("api/v1/permission-grants")]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    //[Authorize]
    [Menu("實驗室權限配置", "fa-solid fa-user-lock", order: 21, parent: null)]
    [RequiresPermission("SYSTEM.PERMISSION_GRANT")]
    [Description("提供管理員維護特定角色在特定實驗室下的動態 BitMask 權限位元組向量")]
    public sealed class PermissionGrantController : ApiControllerBase
    {
        private readonly IPermissionGrantService _permissionGrantService;
        private readonly ILogger<PermissionGrantController> _logger;

        public PermissionGrantController(
            IPermissionGrantService permissionGrantService,
            ILogger<PermissionGrantController> logger)
        {
            _permissionGrantService = permissionGrantService ?? throw new ArgumentNullException(nameof(permissionGrantService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 取得指定角色在指定實驗室的 BitMask 權限矩陣設定
        /// </summary>
        [HttpGet("role/{roleId:guid}/lab/{labId:Guid}")]
        [Menu("檢視實驗室角色權限", "fa-solid fa-eye", order: 1, parent: "實驗室權限配置")]
        [RequiresPermission("SYSTEM.PERMISSION_GRANT.READ")]
        [ProducesResponseType(typeof(RoleLabPermissionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRoleLabPermissions(
            [FromRoute] Guid roleId,
            [FromRoute] Guid labId,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _permissionGrantService.GetRoleLabPermissionsAsync(roleId, labId, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得角色實驗室權限矩陣時發生異常。RoleId: {RoleId}, LabId: {LabId}", roleId, labId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "系統內部錯誤，請聯繫管理員。" });
            }
        }

        /// <summary>
        /// 更新指定角色在特定實驗室的 BitMask 權限向量
        /// </summary>
        [HttpPut("role/{roleId:guid}/lab/{labId:Guid}")]
        [Menu("更新實驗室角色權限", "fa-solid fa-floppy-disk", order: 2, parent: "實驗室權限配置")]
        [RequiresPermission("SYSTEM.PERMISSION_GRANT.UPDATE")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateRoleLabPermissions(
            [FromRoute] Guid roleId,
            [FromRoute] Guid labId,
            [FromBody] UpdateRolePermissionsRequestDto request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var (succeeded, message) = await _permissionGrantService.UpdateRoleLabPermissionsAsync(roleId, labId, request, cancellationToken);
                if (!succeeded)
                {
                    return BadRequest(new { message });
                }

                _logger.LogInformation("成功更新角色 [{RoleId}] 於實驗室 [{LabId}] 的 BitMask 權限向量。", roleId, labId);
                return Ok(new { message = "權限配置更新成功。" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新角色實驗室權限發生異常。RoleId: {RoleId}, LabId: {LabId}", roleId, labId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "系統內部錯誤，請聯繫管理員。" });
            }
        }
    }
}