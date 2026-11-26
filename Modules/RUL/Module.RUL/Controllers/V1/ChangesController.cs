using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.RUL.Application.Contracts;
using SGS.Modules.RUL.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;

namespace SGS.Modules.RUL.Controllers.V1;

/// <summary>規則變更申請與審批（Maker-Checker）：申請者不可審核自己的申請。</summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/rul/changes")]
[ControllerTitle("規則審批", Icon = "fa-solid fa-clipboard-check", Order = 2, Description = "規則變更申請與審批")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ChangesController(RuleAdminService rules) : RulControllerBase
{
    /// <summary>查詢變更申請</summary>
    [HttpPost("search")]
    [Function("SearchRuleChanges", "規則審批", Icon = "fa-solid fa-clipboard-check", Order = 1, Description = "規則變更申請與審批匣", IsMenu = true, Path = "/sgs.modules.rul/changes")]
    [RequiresPermission("RUL.CHANGE.READ", "檢視規則變更申請")]
    [ProducesResponseType(typeof(PagedList<ChangeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedList<ChangeDto>>> Search([FromBody] ChangeSearchRequest request, CancellationToken ct = default) =>
        Ok(await rules.SearchChangesAsync(request ?? new ChangeSearchRequest(), ct).ConfigureAwait(false));

    /// <summary>送出變更申請（新增／修改／停用／啟用）</summary>
    [HttpPost("submit")]
    [Function("SubmitRuleChange", "送出規則變更", Order = 2, Description = "提出規則新增、修改、停用或啟用申請")]
    [RequiresPermission("RUL.RULE.MANAGE", "維護動態規則", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ChangeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChangeDto>> Submit([FromBody] SubmitChangeRequest request, CancellationToken ct = default) =>
        FromResult(await rules.SubmitChangeAsync(Actor, request, ct).ConfigureAwait(false));

    /// <summary>以歷史版本內容提出回滾申請</summary>
    [HttpPost("rollback")]
    [Function("RollbackRule", "回滾規則版本", Order = 3, Description = "以歷史版本提出回滾申請")]
    [RequiresPermission("RUL.RULE.MANAGE", "維護動態規則", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ChangeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChangeDto>> Rollback([FromBody] RollbackRequest request, CancellationToken ct = default) =>
        FromResult(await rules.RollbackAsync(Actor, request, ct).ConfigureAwait(false));

    /// <summary>撤回自己的待審申請</summary>
    [HttpPost("{id:long}/cancel")]
    [Function("CancelRuleChange", "撤回規則申請", Order = 4, Description = "申請者撤回待審申請")]
    [RequiresPermission("RUL.RULE.MANAGE", "維護動態規則", ActionCategory.Administrative)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Cancel(long id, CancellationToken ct = default) =>
        NoContentFrom(await rules.CancelAsync(Actor, id, ct).ConfigureAwait(false));

    /// <summary>核准（規則立即生效）</summary>
    [HttpPost("{id:long}/approve")]
    [Function("ApproveRuleChange", "核准規則變更", Order = 5, Description = "核准規則變更，核准後立即生效")]
    [RequiresPermission("RUL.CHANGE.APPROVE", "核准規則變更", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ChangeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChangeDto>> Approve(long id, [FromBody] ReviewRequest request, CancellationToken ct = default) =>
        FromResult(await rules.ApproveAsync(Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>駁回（須填理由）</summary>
    [HttpPost("{id:long}/reject")]
    [Function("RejectRuleChange", "駁回規則變更", Order = 6, Description = "駁回規則變更申請")]
    [RequiresPermission("RUL.CHANGE.APPROVE", "核准規則變更", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ChangeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChangeDto>> Reject(long id, [FromBody] ReviewRequest request, CancellationToken ct = default) =>
        FromResult(await rules.RejectAsync(Actor, id, request, ct).ConfigureAwait(false));
}
