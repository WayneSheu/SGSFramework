// 檔案路徑: SGSFramework.Identity/Controllers/RoleManagementController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;

namespace SGSFramework.Identity.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RoleManagementController<TRole, TKey> : ControllerBase
        where TRole : IdentityRole<TKey>
        where TKey : IEquatable<TKey>
    {
        // 🔑 修正點 1: 宣告泛型介面並傳入 2 個型態引數 (解決 CS0305)
        private readonly IRoleManagementService<TRole, TKey> _roleManagementService;

        public RoleManagementController(IRoleManagementService<TRole, TKey> roleManagementService)
        {
            _roleManagementService = roleManagementService ?? throw new ArgumentNullException(nameof(roleManagementService));
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetAllRoles(CancellationToken cancellationToken)
        {
            var roles = await _roleManagementService.GetAllRolesAsync(cancellationToken);
            return Ok(roles);
        }

        [HttpGet("roles/{roleId}")]
        public async Task<IActionResult> GetRoleById(string roleId, CancellationToken cancellationToken)
        {
            var role = await _roleManagementService.GetRoleByIdAsync(roleId, cancellationToken);
            if (role == null) return NotFound();
            return Ok(role);
        }

        [HttpPost("roles")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
        {
            // 🔑 修正點 2: 當 IRoleManagementService 轉型正確，(succeeded, errors) 即可正確解構 (解決 CS8130)
            var (succeeded, errors) = await _roleManagementService.CreateRoleAsync(request, cancellationToken);
            if (!succeeded) return BadRequest(errors);
            return Ok();
        }

        [HttpPut("roles")]
        public async Task<IActionResult> UpdateRole([FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
        {
            var (succeeded, errors) = await _roleManagementService.UpdateRoleAsync(request, cancellationToken);
            if (!succeeded) return BadRequest(errors);
            return Ok();
        }

        [HttpDelete("roles/{roleId}")]
        public async Task<IActionResult> DeleteRole(string roleId, CancellationToken cancellationToken)
        {
            var (succeeded, errors) = await _roleManagementService.DeleteRoleAsync(roleId, cancellationToken);
            if (!succeeded) return BadRequest(errors);
            return Ok();
        }

        [HttpPost("ad-group/map")]
        public async Task<IActionResult> MapAdGroupToRole([FromBody] MapAdGroupToRoleRequest request, CancellationToken cancellationToken)
        {
            var (succeeded, message) = await _roleManagementService.MapAdGroupToRoleAsync(request, cancellationToken);
            if (!succeeded) return BadRequest(message);
            return Ok(message);
        }

        [HttpPost("ad-group/remove")]
        public async Task<IActionResult> RemoveAdGroupFromRole([FromBody] RemoveAdGroupFromRoleRequest request, CancellationToken cancellationToken)
        {
            var (succeeded, message) = await _roleManagementService.RemoveAdGroupFromRoleAsync(request, cancellationToken);
            if (!succeeded) return BadRequest(message);
            return Ok(message);
        }

        [HttpPost("ad-group/sync")]
        public async Task<IActionResult> SyncUserRolesFromAdGroups([FromBody] SyncUserAdRolesRequest request, CancellationToken cancellationToken)
        {
            var (succeeded, syncedRoles, message) = await _roleManagementService.SyncUserRolesFromAdGroupsAsync(request, cancellationToken);
            if (!succeeded) return BadRequest(message);
            return Ok(new { SyncedRoles = syncedRoles, Message = message });
        }

        [HttpPost("user/assign-roles")]
        public async Task<IActionResult> AssignUserRoles([FromBody] AssignUserRolesRequest request, CancellationToken cancellationToken)
        {
            var (succeeded, message) = await _roleManagementService.AssignUserRolesAsync(request, cancellationToken);
            if (!succeeded) return BadRequest(message);
            return Ok(message);
        }
    }
}