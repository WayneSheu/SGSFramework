using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Entities;

namespace SGSFramework.AuthTokenBucket.Controllers.V1;

/// <summary>
/// 角色治理：權限變更雙重審批 (Maker-Checker) 與職能分立規則 (SoD)。
/// 權限依動作個別設定（檢視 / 審核 / 維護規則），以支援職責分離。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/role-governance")]
[ControllerTitle("角色治理", Icon = "fa-solid fa-scale-balanced", Order = 15, Description = "角色權限變更審核與職能分立 (SoD) 規則管理")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class RoleGovernanceController(IRoleGovernanceService governanceService) : ControllerBase
{
    private readonly IRoleGovernanceService _governance = governanceService ?? throw new ArgumentNullException(nameof(governanceService));

    public sealed record ReviewRequest(string? Comment);
    public sealed record SodCheckRequest(List<string> PermissionKeys);

    /// <summary>查詢權限變更申請（預設僅待審核）</summary>
    [HttpGet("change-requests")]
    [Function("GetRoleChangeRequests", "查詢權限變更申請", Icon = "fa-solid fa-clipboard-check", Order = 1,
        Description = "查詢角色權限變更申請（選單入口位於角色管理 → 權限變更審核）")]
    [RequiresPermission("SYSTEM.ROLEGOVERNANCE.READ", "檢視權限變更申請")]
    [ProducesResponseType(typeof(List<RoleChangeRequest>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChangeRequests([FromQuery] RoleChangeRequestStatus? status,
        [FromQuery] bool all = false, CancellationToken cancellationToken = default)
    {
        // 不可在參數上給 nullable enum 預設值：Microsoft.AspNetCore.OpenApi 產生文件時會 InvalidCastException（Int32 → Nullable<enum>）
        RoleChangeRequestStatus? effectiveStatus = all ? null : status ?? RoleChangeRequestStatus.Pending;
        var list = await _governance.GetChangeRequestViewsAsync(effectiveStatus, cancellationToken).ConfigureAwait(false);
        return Ok(list);
    }

    /// <summary>核准申請並套用權限（提出者不得自行核准）</summary>
    [HttpPost("change-requests/{id:guid}/approve")]
    [Function("ApproveRoleChangeRequest", "核准權限變更", Icon = "fa-solid fa-check", Order = 2, Description = "核准角色權限變更申請並套用")]
    [RequiresPermission("SYSTEM.ROLEGOVERNANCE.APPROVE", "審核權限變更申請")]
    public async Task<IActionResult> Approve([FromRoute] Guid id, [FromBody] ReviewRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _governance.ApproveAsync(id, request?.Comment, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? Ok(new { result.Succeeded, result.Message }) : Problem400("核准失敗", result.Message, result.Violations);
    }

    /// <summary>駁回申請（須填寫原因）</summary>
    [HttpPost("change-requests/{id:guid}/reject")]
    [Function("RejectRoleChangeRequest", "駁回權限變更", Icon = "fa-solid fa-xmark", Order = 3, Description = "駁回角色權限變更申請")]
    [RequiresPermission("SYSTEM.ROLEGOVERNANCE.APPROVE", "審核權限變更申請")]
    public async Task<IActionResult> Reject([FromRoute] Guid id, [FromBody] ReviewRequest request, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _governance.RejectAsync(id, request?.Comment, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("駁回失敗", message);
    }

    /// <summary>提出者撤回自己的申請</summary>
    [HttpPost("change-requests/{id:guid}/cancel")]
    [Function("CancelRoleChangeRequest", "撤回權限變更申請", Icon = "fa-solid fa-rotate-left", Order = 4, Description = "提出者撤回待審核的權限變更申請")]
    [RequiresPermission("SYSTEM.ROLEGOVERNANCE.READ", "檢視權限變更申請")]
    public async Task<IActionResult> Cancel([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _governance.CancelAsync(id, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("撤回失敗", message);
    }

    /// <summary>查詢職能分立規則</summary>
    [HttpGet("sod-rules")]
    [Function("GetSodRules", "職能分立規則", Icon = "fa-solid fa-code-compare", Order = 5, Description = "檢視職能分立 (SoD) 互斥權限規則")]
    [RequiresPermission("SYSTEM.ROLEGOVERNANCE.READ", "檢視權限變更申請")]
    public async Task<IActionResult> GetSodRules(CancellationToken cancellationToken = default) =>
        Ok(await _governance.GetSodRulesAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>新增或更新職能分立規則</summary>
    [HttpPost("sod-rules")]
    [Function("SaveSodRule", "維護職能分立規則", Icon = "fa-solid fa-pen", Order = 6, Description = "新增或更新職能分立 (SoD) 互斥權限規則")]
    [RequiresPermission("SYSTEM.ROLEGOVERNANCE.SOD", "維護職能分立規則")]
    public async Task<IActionResult> SaveSodRule([FromBody] SodRule rule, CancellationToken cancellationToken = default)
    {
        var (ok, message, saved) = await _governance.SaveSodRuleAsync(rule, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message, Rule = saved }) : Problem400("儲存規則失敗", message);
    }

    /// <summary>刪除職能分立規則</summary>
    [HttpDelete("sod-rules/{id:guid}")]
    [Function("DeleteSodRule", "刪除職能分立規則", Icon = "fa-solid fa-trash", Order = 7, Description = "刪除職能分立 (SoD) 互斥權限規則")]
    [RequiresPermission("SYSTEM.ROLEGOVERNANCE.SOD", "維護職能分立規則")]
    public async Task<IActionResult> DeleteSodRule([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _governance.DeleteSodRuleAsync(id, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("刪除規則失敗", message);
    }

    /// <summary>檢查權限組合是否違反 SoD（供前端於送出前即時提示）</summary>
    [HttpPost("sod-rules/check")]
    [Function("CheckSodRules", "檢查職能分立", Icon = "fa-solid fa-magnifying-glass", Order = 8, Description = "檢查權限組合是否違反職能分立規則")]
    [RequiresPermission("SYSTEM.ROLEGOVERNANCE.READ", "檢視權限變更申請")]
    public async Task<IActionResult> CheckSod([FromBody] SodCheckRequest request, CancellationToken cancellationToken = default) =>
        Ok(await _governance.CheckSodAsync(request?.PermissionKeys ?? [], cancellationToken).ConfigureAwait(false));

    private BadRequestObjectResult Problem400(string title, string detail, IReadOnlyList<SodViolation>? violations = null)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path
        };
        if (violations is { Count: > 0 }) problem.Extensions["sodViolations"] = violations;
        return BadRequest(problem);
    }
}
