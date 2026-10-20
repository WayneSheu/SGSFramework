#nullable enable

namespace SGSFramework.Identity.Controllers.V1;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.Services;
using System;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 區域實驗室名錄：主管、成員與實驗室專屬角色指派（實驗室管理頁使用）
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/lab-directory")]
[ControllerTitle("實驗室名錄", Icon = "fa-solid fa-address-book", Order = 25, Description = "區域實驗室主管、成員與實驗室專屬角色指派")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class LabDirectoryController(
    ILabDirectoryService labDirectoryService,
    IRoleManagementService<ApplicationRole, Guid> roleManagementService) : ControllerBase
{
    private readonly ILabDirectoryService _directory = labDirectoryService ?? throw new ArgumentNullException(nameof(labDirectoryService));
    private readonly IRoleManagementService<ApplicationRole, Guid> _roles = roleManagementService ?? throw new ArgumentNullException(nameof(roleManagementService));

    public sealed record SetManagerRequest(Guid? UserId);

    /// <summary>取得區域實驗室主管與成員</summary>
    [HttpGet("{tenantLabId:guid}")]
    [Function("GetLabDirectory", "檢視實驗室成員", Icon = "fa-solid fa-users", Order = 1, Description = "檢視區域實驗室主管與成員清單")]
    [RequiresPermission("ORG.LABORATORY.READ")]
    [ProducesResponseType(typeof(LabDirectoryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromRoute] Guid tenantLabId, CancellationToken cancellationToken = default) =>
        Ok(await _directory.GetAsync(tenantLabId, cancellationToken).ConfigureAwait(false));

    /// <summary>設定或清除區域實驗室主管（主管須為實驗室有效成員）</summary>
    [HttpPut("{tenantLabId:guid}/manager")]
    [Function("SetLabManager", "設定實驗室主管", Icon = "fa-solid fa-user-tie", Order = 2, Description = "設定或清除區域實驗室主管")]
    [RequiresPermission("ORG.LABORATORY.EDIT", "編輯實驗室")]
    public async Task<IActionResult> SetManager([FromRoute] Guid tenantLabId, [FromBody] SetManagerRequest request, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _directory.SetManagerAsync(tenantLabId, request?.UserId, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("設定主管失敗", message);
    }

    /// <summary>將實驗室專屬角色指派至此區域實驗室</summary>
    [HttpPost("{tenantLabId:guid}/roles/{roleId}")]
    [Function("AssignLabRole", "指派實驗室專屬角色", Icon = "fa-solid fa-link", Order = 3, Description = "將實驗室專屬角色的適用範圍加入此區域實驗室")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.UPDATE", "編輯角色")]
    public Task<IActionResult> AssignRole([FromRoute] Guid tenantLabId, [FromRoute] string roleId, CancellationToken cancellationToken = default) =>
        SetRoleScopeAsync(tenantLabId, roleId, assign: true, cancellationToken);

    /// <summary>自此區域實驗室移除實驗室專屬角色</summary>
    [HttpDelete("{tenantLabId:guid}/roles/{roleId}")]
    [Function("RemoveLabRole", "移除實驗室專屬角色", Icon = "fa-solid fa-link-slash", Order = 4, Description = "將此區域實驗室自實驗室專屬角色的適用範圍移除")]
    [RequiresPermission("SYSTEM.ROLEMANAGEMENT.UPDATE", "編輯角色")]
    public Task<IActionResult> RemoveRole([FromRoute] Guid tenantLabId, [FromRoute] string roleId, CancellationToken cancellationToken = default) =>
        SetRoleScopeAsync(tenantLabId, roleId, assign: false, cancellationToken);

    private async Task<IActionResult> SetRoleScopeAsync(Guid tenantLabId, string roleId, bool assign, CancellationToken cancellationToken)
    {
        var (ok, message) = await _roles.SetRoleLabScopeAsync(roleId, tenantLabId, assign, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400(assign ? "指派角色失敗" : "移除角色失敗", message);
    }

    private BadRequestObjectResult Problem400(string title, string detail) =>
        BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = title, Detail = detail });
}
