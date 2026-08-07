using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    //[Authorize]
    [Menu("權限管理", "fa-solid fa-lock", order: 20, parent: null)]
    [RequiresPermission("SYSTEM_PERMISSION_READ")]
    [Description("權限管理")]
    public class PermissionController : ApiControllerBase
    {
        private readonly IPermissionManagementService _permissionService;

        public PermissionController(IPermissionManagementService permissionService)
        {
            _permissionService = permissionService;
        }

        /// <summary>
        /// 取得完整系統與動態模組權限清單 (階層式：Module -> Controller -> Permissions)
        /// </summary>
        [HttpGet("tree")]
        [Menu("取得模組權限清單", "fa-solid fa-lock", order: 20, parent: null)]
        [RequiresPermission("SYSTEM_PERMISSION_READ")]
        [Description("取得完整系統與動態模組權限清單 (階層式：Module -> Controller -> Permissions)")]
        public async Task<ActionResult<List<PermissionModuleDto>>> GetPermissionTree(CancellationToken cancellationToken)
        {
            try
            {
                var tree = await _permissionService.GetPermissionTreeAsync(cancellationToken);
                return Ok(tree);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "無法取得權限樹狀結構", detail = ex.Message });
            }
        }

        /// <summary>
        /// 取得指定角色的權限設定清單
        /// </summary>
        [HttpGet("role/{roleId}")]
        [Menu("取得角色權限清單", "fa-solid fa-lock", order: 20, parent: null)]
        [RequiresPermission("SYSTEM_PERMISSION_READ")]
        [Description("取得指定角色的權限設定清單")]
        public async Task<ActionResult<RolePermissionMatrixDto>> GetRolePermissions([FromRoute] string roleId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _permissionService.GetRolePermissionsAsync(roleId, cancellationToken);
                if (result == null)
                {
                    return NotFound(new { message = $"找不到指定 RoleId: {roleId}" });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "無法取得角色權限", detail = ex.Message });
            }
        }

        /// <summary>
        /// 更新指定角色的權限關聯
        /// </summary>
        [HttpPost("role/update")]
        [Menu("更新角色權限", "fa-solid fa-lock", order: 20, parent: null)]
        [RequiresPermission("SYSTEM_PERMISSION_UPDATE")]
        [Description("更新指定角色的權限關聯")]
        public async Task<IActionResult> UpdateRolePermissions([FromBody] UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrEmpty(request.RoleId))
            {
                return BadRequest(new { message = "請求參數無效" });
            }

            try
            {
                var success = await _permissionService.UpdateRolePermissionsAsync(request, cancellationToken);
                if (!success)
                {
                    return NotFound(new { message = $"更新失敗，找不到指定 RoleId: {request.RoleId}" });
                }

                return Ok(new { message = "角色權限更新成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "更新角色權限時發生內部錯誤", detail = ex.Message });
            }
        }
    }
}
