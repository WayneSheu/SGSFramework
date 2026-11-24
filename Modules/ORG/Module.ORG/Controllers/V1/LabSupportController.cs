using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Features.Laboratories.Command;
using SGS.Modules.ORG.Application.Features.Laboratories.Queries;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Paginations;
using SGSFramework.Core.Results;
using System.Security.Claims;

namespace SGS.Modules.ORG.Controllers.V1;

/// <summary>
/// 實驗室支援（使用者於期間內支援其他實驗室，歸屬類別 = Support）
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/org/lab-supports")]
[ControllerTitle("實驗室支援", Icon = "fa-solid fa-handshake-angle", Order = 12, Description = "管理使用者於指定期間內支援其他實驗室")]
[RequiresPermission("ORG.LABSUPPORT.READ")]
public class LabSupportController : ApiControllerBase
{
    private readonly ILogger<LabSupportController> _logger;
    private readonly IMediator _mediator;

    public LabSupportController(ILogger<LabSupportController> logger, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>分頁查詢實驗室支援</summary>
    [HttpPost("search")]
    [Function("SearchLabSupports", "實驗室支援", Icon = "fa-solid fa-handshake-angle", Order = 1, IsMenu = true, Path = "/sgsframework.org/labsupport", Description = "查詢使用者支援其他實驗室之清單")]
    [RequiresPermission("ORG.LABSUPPORT.READ", "查詢實驗室支援")]
    [EndpointSummary("分頁查詢實驗室支援")]
    [ProducesResponseType(typeof(Result<PagedResult<LabSupportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] LabSupportSearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _mediator.Send(new SearchLabSupportsQuery(request), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>搜尋可指派支援的人員（姓名或員工編號關鍵字）</summary>
    [HttpGet("candidates")]
    [Function("SearchLabSupportCandidates", "查詢可支援人員", Icon = "fa-solid fa-user-magnifying-glass", Order = 4, Description = "以姓名或員工編號搜尋可指派支援的人員")]
    [RequiresPermission("ORG.LABSUPPORT.READ", "查詢實驗室支援")]
    [EndpointSummary("查詢可支援人員")]
    [ProducesResponseType(typeof(Result<List<LabSupportCandidateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Candidates([FromQuery] string? keyword, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SearchLabSupportCandidatesQuery(keyword), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>新增或更新使用者支援實驗室</summary>
    [HttpPost]
    [Function("SaveLabSupport", "維護實驗室支援", Icon = "fa-solid fa-user-plus", Order = 2, Description = "新增或更新使用者支援實驗室（須設定支援期間）")]
    [RequiresPermission("ORG.LABSUPPORT.MANAGE", "維護實驗室支援")]
    [EndpointSummary("新增或更新實驗室支援")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Save([FromBody] SaveLabSupportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserId == Guid.Empty || request.LabId <= 0 || request.TenantLabId == Guid.Empty)
        {
            return BadRequest(Result.Failure<bool>(
                Error.Validation("LABSUPPORT_INVALID_PARAMS", "使用者與支援實驗室為必填。")));
        }

        var operatorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var command = new AssignUserLabCommand(
            request.UserId, request.LabId, request.TenantLabId, LabAssignmentType.Support,
            request.JobTitle, request.EffectiveDate, request.ExpiryDate, operatorId);
        var result = await _mediator.Send(command, cancellationToken);
        if (!result.IsSuccess) _logger.LogWarning("儲存實驗室支援失敗: {Error}", result.Error);
        return HandleResult(result);
    }

    /// <summary>撤銷（停用）實驗室支援</summary>
    [HttpDelete("users/{userId:guid}/labs/{labId:int}")]
    [Function("RevokeLabSupport", "撤銷實驗室支援", Icon = "fa-solid fa-user-slash", Order = 3, Description = "停用使用者對指定實驗室之支援")]
    [RequiresPermission("ORG.LABSUPPORT.MANAGE", "維護實驗室支援")]
    [EndpointSummary("撤銷實驗室支援")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Revoke([FromRoute] Guid userId, [FromRoute] int labId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || labId <= 0)
        {
            return BadRequest(Result.Failure<bool>(
                Error.Validation("LABSUPPORT_INVALID_PARAMS", "參數無效：UserId 與 LabId 必須為合法數值。")));
        }
        var operatorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _mediator.Send(new DeactivateUserLabCommand(userId, labId, operatorId), cancellationToken);
        return HandleResult(result);
    }
}

/// <summary>新增/更新實驗室支援請求</summary>
public sealed record SaveLabSupportRequest
{
    public Guid UserId { get; init; }
    public int LabId { get; init; }
    public Guid TenantLabId { get; init; }
    public string? JobTitle { get; init; }
    public DateTime? EffectiveDate { get; init; }
    public DateTime ExpiryDate { get; init; }
}
