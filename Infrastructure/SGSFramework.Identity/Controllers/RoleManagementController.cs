// 檔案路徑: SGSFramework.Identity/Controllers/RoleManagementController.cs

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
using System.ComponentModel;
using System.Net.Mime;

namespace SGSFramework.Identity.Controllers
{
    /// <summary>
    /// 系統角色與 AD 群組映射管理控制器
    /// </summary>
    [ApiController]
    [Route("api/v1/roles")]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    //[Authorize]
    [Menu("角色權限管理", "fa-solid fa-user-shield", order: 20, parent: null)]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT")]
    [Description("提供企業級角色 CRUD、AD 網域群組自動對應與使用者角色授權管理")]
    public sealed class RoleManagementController : ApiControllerBase
    {
        private readonly IRoleManagementService<ApplicationRole, Guid> _roleManagementService;
        private readonly ILogger<RoleManagementController> _logger;

        public RoleManagementController(
            IRoleManagementService<ApplicationRole, Guid> roleManagementService,
            ILogger<RoleManagementController> logger)
        {
            _roleManagementService = roleManagementService ?? throw new ArgumentNullException(nameof(roleManagementService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 取得系統所有角色清單
        /// </summary>
        [HttpGet]
        [Menu("查詢角色列表", "fa-solid fa-list", order: 1, parent: "角色權限管理")]
        [RequiresPermission("SYSTEM.ROLEMANAGEMENT.READ")]
        [Description("取得系統所有角色清單，包含角色名稱、描述、建立時間等資訊")]
        [ProducesResponseType(typeof(IEnumerable<ApplicationRole>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllRoles(CancellationToken cancellationToken)
        {
            try
            {
                var roles = await _roleManagementService.GetAllRolesAsync(cancellationToken);
                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢角色列表時發生未預期異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "系統內部錯誤，請聯繫管理員。" });
            }
        }

        /// <summary>
        /// 依 Role ID 取得單一角色詳細資訊
        /// </summary>
        [HttpGet("{roleId}")]
        [Menu("檢視角色細節", "fa-solid fa-circle-info", order: 2, parent: "角色權限管理")]
        [RequiresPermission("SYSTEM.ROLEMANAGEMENT.READ")]
        [Description("依 Role ID 取得單一角色詳細資訊，包含角色名稱、描述、建立時間、對應的 AD 群組等資訊")]
        [ProducesResponseType(typeof(ApplicationRole), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRoleById([FromRoute] string roleId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

            try
            {
                var role = await _roleManagementService.GetRoleByIdAsync(roleId, cancellationToken);
                if (role == null)
                {
                    return NotFound(new { message = $"找不到識別碼為 '{roleId}' 的角色。" });
                }

                return Ok(role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢角色細節時發生異常。RoleId: {RoleId}", roleId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "系統內部錯誤，請聯繫管理員。" });
            }
        }

        /// <summary>
        /// 建立新系統角色
        /// </summary>
        [HttpPost]
        [Menu("新增角色", "fa-solid fa-plus", order: 3, parent: "角色權限管理")]
        [Description("建立新系統角色，需提供角色名稱與描述")]
        [RequiresPermission("SYSTEM.ROLEMANAGEMENT.CREATE")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var (succeeded, errors) = await _roleManagementService.CreateRoleAsync(request, cancellationToken);
                if (!succeeded)
                {
                    return BadRequest(new { message = "建立角色失敗", errors });
                }

                _logger.LogInformation("成功建立系統角色: {RoleName}", request.RoleName);
                return CreatedAtAction(nameof(GetRoleById), new { roleId = request.RoleName }, new { message = "角色建立成功", roleName = request.RoleName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "建立角色時發生未預期異常。RoleName: {RoleName}", request.RoleName);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "系統內部錯誤，請聯繫管理員。" });
            }
        }

        /// <summary>
        /// 更新角色定義
        /// </summary>
        [HttpPut]
        [Menu("編輯角色", "fa-solid fa-pen-to-square", order: 4, parent: "角色權限管理")]
        [Description("更新角色定義，需提供角色 ID、角色名稱與描述")]
        [RequiresPermission("SYSTEM.ROLEMANAGEMENT.UPDATE")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateRole([FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var (succeeded, errors) = await _roleManagementService.UpdateRoleAsync(request, cancellationToken);
                if (!succeeded)
                {
                    return BadRequest(new { message = "更新角色失敗", errors });
                }

                _logger.LogInformation("角色資料更新成功。RoleId: {RoleId}", request.RoleId);
                return Ok(new { message = "角色資料更新成功。" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新角色時發生異常。RoleId: {RoleId}", request.RoleId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "系統內部錯誤，請聯繫管理員。" });
            }
        }

        /// <summary>
        /// 刪除角色
        /// </summary>
        [HttpDelete("{roleId}")]
        [Menu("刪除角色", "fa-solid fa-trash", order: 5, parent: "角色權限管理")]
        [RequiresPermission("SYSTEM.ROLEMANAGEMENT.DELETE")]
        [Description("刪除角色，需提供角色 ID，刪除後將無法復原")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteRole([FromRoute] string roleId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

            try
            {
                var (succeeded, errors) = await _roleManagementService.DeleteRoleAsync(roleId, cancellationToken);
                if (!succeeded)
                {
                    return BadRequest(new { message = "刪除角色失敗", errors });
                }

                _logger.LogWarning("角色已成功刪除。RoleId: {RoleId}", roleId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刪除角色時發生異常。RoleId: {RoleId}", roleId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "系統內部錯誤，請聯繫管理員。" });
            }
        }

        /// <summary>
        /// 建立 AD 群組與角色之對應關係
        /// </summary>
        [HttpPost("ad-group/map")]
        [Menu("映射 AD 群組", "fa-solid fa-network-wired", order: 6, parent: "角色權限管理")]
        [RequiresPermission("SYSTEM.ROLEMANAGEMENT.ADMAP")]
        [Description("建立 AD 群組與角色之對應關係，需提供角色 ID 與 AD 群組名稱")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> MapAdGroupToRole([FromBody] MapAdGroupToRoleRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var (succeeded, message) = await _roleManagementService.MapAdGroupToRoleAsync(request, cancellationToken);
                if (!succeeded)
                {
                    return BadRequest(new { message });
                }

                _logger.LogInformation("成功建立 AD 群組 [{AdGroup}] 與角色 [{RoleId}] 的對應關係。", request.AdGroupName, request.RoleId);
                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "映射 AD 群組時發生異常。AdGroup: {AdGroupName}", request.AdGroupName);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "系統內部錯誤，請聯繫管理員。" });
            }
        }

        /// <summary>
        /// 解除 AD 群組與角色之對應關係
        /// </summary>
        [HttpPost("ad-group/remove")]
        [Menu("解除 AD 群組映射", "fa-solid fa-link-slash", order: 7, parent: "角色權限管理")]
        [RequiresPermission("SYSTEM.ROLEMANAGEMENT.ADMAP")]
        [Description("解除 AD 群組與角色之對應關係，需提供角色 ID 與 AD 群組名稱")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RemoveAdGroupFromRole([FromBody] RemoveAdGroupFromRoleRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var (succeeded, message) = await _roleManagementService.RemoveAdGroupFromRoleAsync(request, cancellationToken);
                if (!succeeded)
                {
                    return BadRequest(new { message });
                }

                _logger.LogInformation("已移除 AD 群組 [{AdGroupName}] 與角色 [{RoleId}] 的對應關係。", request.AdGroupName, request.RoleId);
                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解除 AD 群組映射時發生異常。AdGroupName: {AdGroupName}", request.AdGroupName);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "系統內部錯誤，請聯繫管理員。" });
            }
        }

        /// <summary>
        /// 根據 AD 群組同步使用者角色
        /// </summary>
        [HttpPost("ad-group/sync")]
        [Menu("同步 AD 使用者角色", "fa-solid fa-rotate", order: 8, parent: "角色權限管理")]
        [RequiresPermission("SYSTEM.ROLEMANAGEMENT.SYNC")]
        [Description("根據使用者所屬的 AD 群組，同步其在系統中的角色，需提供使用者帳號與其 AD 群組清單")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SyncUserRolesFromAdGroups([FromBody] SyncUserAdRolesRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var (succeeded, syncedRoles, message) = await _roleManagementService.SyncUserRolesFromAdGroupsAsync(request, cancellationToken);
                if (!succeeded)
                {
                    return BadRequest(new { message });
                }

                _logger.LogInformation("使用者 [{UserId}] 依 AD 群組同步角色成功。", request.Username);
                return Ok(new { syncedRoles, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步 AD 群組角色時發生異常。UserId: {UserId}", request.Username);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "系統內部錯誤，請聯繫管理員。" });
            }
        }

        /// <summary>
        /// 手動批次指派使用者角色
        /// </summary>
        [HttpPost("user/assign-roles")]
        [Menu("指派使用者角色", "fa-solid fa-user-tag", order: 9, parent: "角色權限管理")]
        [RequiresPermission("SYSTEM.ROLEMANAGEMENT.ASSIGN")]
        [Description("手動批次指派使用者角色，需提供使用者 ID 與角色清單")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AssignUserRoles([FromBody] AssignUserRolesRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var (succeeded, message) = await _roleManagementService.AssignUserRolesAsync(request, cancellationToken);
                if (!succeeded)
                {
                    return BadRequest(new { message });
                }

                _logger.LogInformation("已成功指派角色予使用者 [{UserId}]。", request.UserId);
                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "指派使用者角色時發生異常。UserId: {UserId}", request.UserId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "系統內部錯誤，請聯繫管理員。" });
            }
        }
    }
}