using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System.Security.Claims;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Application.Features.Laboratories.Queries;
using SGS.Modules.ORG.Application.Features.Laboratories.Command;

namespace SGS.Modules.ORG.Controllers;

/// <summary>
/// 使用者與實驗室歸屬對應控制器
/// </summary>
[ApiController]
[ApiVersion("v1")]
[Route("api/org/user-labs")]
[ControllerTitle("使用者實驗室權限對應", Icon = "fa-solid fa-user-gear", Order = 11, Description = "管理使用者於各實驗室之主要/兼任歸屬與職位標題")]
[RequiresPermission("ORG_USERLAB_READ")]
public class UserLabMappingController : ApiControllerBase
{
    private readonly ILogger<UserLabMappingController> _logger;
    private readonly IMediator _mediator;

    public UserLabMappingController(ILogger<UserLabMappingController> logger, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// 取得指定使用者的所有實驗室歸屬清單
    /// </summary>
    [HttpGet("users/{userId:guid}")]
    [Function("GetUserLabMappings", "取得使用者實驗室對應", Icon = "fa-solid fa-id-card", Order = 1, Description = "查詢特定使用者之主要與兼任實驗室清單")]
    [RequiresPermission("ORG_USERLAB_READ")]
    [ProducesResponseType(typeof(Result<List<UserLabMappingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserLabMappings([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest(Result.Failure<List<UserLabMappingDto>>(
                Error.Validation("USERLAB_INVALID_USERID", "使用者識別碼不能為 Empty Guid。")));
        }

        try
        {
            var query = new GetUserLabMappingsByUserIdQuery(userId);
            var result = await _mediator.Send(query, cancellationToken);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lab mappings for UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// 設定或新增使用者的實驗室歸屬 (主要/次要)
    /// </summary>
    [HttpPost]
    [Function("AssignUserLab", "指派使用者實驗室", Icon = "fa-solid fa-user-plus", Order = 2, Description = "為使用者建立新的實驗室歸屬關係")]
    [RequiresPermission("ORG_USERLAB_CREATE")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignUserLab([FromBody] AssignUserLabCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            var operatorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var commandWithOperator = command with { OperatorId = operatorId };

            var result = await _mediator.Send(commandWithOperator, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogError("指派使用者實驗室失敗: {Error}", result.Error);
            }
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing AssignUserLab for UserId: {UserId}, LabId: {LabId}", command.UserId, command.LabId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// 切換/設定使用者之主要實驗室 (Primary Lab)
    /// </summary>
    [HttpPut("users/{userId:guid}/primary/{labId:int}")]
    [Function("SetPrimaryLab", "設定主要實驗室", Icon = "fa-solid fa-star", Order = 3, Description = "將指定實驗室升級為主要歸屬，並自動將舊主要實驗室降級")]
    [RequiresPermission("ORG_USERLAB_PUT")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetPrimaryLab([FromRoute] Guid userId, [FromRoute] int labId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || labId <= 0)
        {
            return BadRequest(Result.Failure<bool>(
                Error.Validation("USERLAB_INVALID_PARAMS", "參數無效：UserId 與 LabId 必須為合法數值。")));
        }

        try
        {
            var operatorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var command = new SetPrimaryLabCommand(userId, labId, operatorId);

            var result = await _mediator.Send(command, cancellationToken);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting primary lab for UserId: {UserId}, LabId: {LabId}", userId, labId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// 停用使用者與實驗室之對應關係
    /// </summary>
    [HttpDelete("users/{userId:guid}/labs/{labId:int}")]
    [Function("DeactivateUserLab", "停用使用者實驗室關聯", Icon = "fa-solid fa-user-slash", Order = 4, Description = "停用使用者於指定實驗室之存取權限")]
    [RequiresPermission("ORG_USERLAB_DELETE")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeactivateUserLab([FromRoute] Guid userId, [FromRoute] int labId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || labId <= 0)
        {
            return BadRequest(Result.Failure<bool>(
                Error.Validation("USERLAB_INVALID_PARAMS", "參數無效：UserId 與 LabId 必須為合法數值。")));
        }

        try
        {
            var operatorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var command = new DeactivateUserLabCommand(userId, labId, operatorId);

            var result = await _mediator.Send(command, cancellationToken);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user lab for UserId: {UserId}, LabId: {LabId}", userId, labId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred.");
        }
    }
}