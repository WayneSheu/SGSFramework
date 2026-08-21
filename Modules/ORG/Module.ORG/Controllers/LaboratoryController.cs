using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Features.Laboratories.Command;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Application.Features.Laboratories.Queries;
using SGS.Modules.ORG.Application.Features.Laboratories.Query;
using SGS.Modules.ORG.Controllers.Requests;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;

namespace SGS.Modules.ORG.Controllers;

/// <summary>
/// 實驗室維護控制器
/// </summary>
[ApiController]
[ApiVersion("v1")]
[Route("api/org/laboratories")]
[ControllerTitle("實驗室管理", Icon = "fa-solid fa-flask", Order = 10, Description = "維護組織樹狀結構下的各級實驗室資訊")]
[RequiresPermission("ORG_LAB_READ")]
public class LaboratoryController : ApiControllerBase
{
    private readonly ILogger<LaboratoryController> _logger;
    private readonly IMediator _mediator;

    public LaboratoryController(ILogger<LaboratoryController> logger, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// 取得目前登入使用者可存取的所有實驗室清單 (含繼承之子節點)
    /// </summary>
    [HttpGet("accessible")]
    [Authorize]
    [RequiresPermission("")]
    [Function("GetMyAccessibleLaboratories", "取得可存取實驗室", Icon = "fa-solid fa-user-shield", Order = 0, Description = "取得登入者權限範圍內包含繼承節點的可存取實驗室清單")]
    [ProducesResponseType(typeof(Result<List<AccessibleLaboratoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyAccessibleLaboratories(CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var query = new GetAccessibleLaboratoriesQuery(userId);
            var result = await _mediator.Send(query, cancellationToken);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred in GetMyAccessibleLaboratories.");
            return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// 取得實驗室清單
    /// </summary>
    [HttpGet]
    [Function("GetLaboratories", "取得實驗室清單", Icon = "fa-solid fa-list", Order = 1, Description = "取得所有實驗室基本資訊清單")]
    [RequiresPermission("ORG_LAB_READ")]
    [ProducesResponseType(typeof(Result<List<LaboratoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLaboratories(CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetLaboratoriesQuery();
            var result = await _mediator.Send(query, cancellationToken);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred in GetLaboratories.");
            return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// 依據 Id 取得單一實驗室基本資訊
    /// </summary>
    [HttpGet("{id:int}")]
    [Function("GetLaboratory", "取得特定實驗室資訊", Icon = "fa-solid fa-flask-vial", Order = 2, Description = "依據實驗室識別碼讀取詳細資料")]
    [RequiresPermission("ORG_LAB_READ")]
    [ProducesResponseType(typeof(Result<LaboratoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLaboratory([FromRoute] int id, CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetLaboratoryQuery(id);
            var result = await _mediator.Send(query, cancellationToken);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving laboratory ID: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// 依據 Id 取得特定實驗室及其完整下階層樹狀結構
    /// </summary>
    [HttpGet("{id:int}/tree")]
    [Function("GetLaboratoryTree", "取得特定實驗室子樹", Icon = "fa-solid fa-sitemap", Order = 3, Description = "取得特定實驗室之完整下階層樹狀階層結構")]
    [RequiresPermission("ORG_LAB_READ")]
    [ProducesResponseType(typeof(Result<LaboratoryTreeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLaboratoryTree([FromRoute] int id, CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetLaboratoryTreeQuery(id);
            var result = await _mediator.Send(query, cancellationToken);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving laboratory tree for ID: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// 新增實驗室
    /// </summary>
    [HttpPost]
    [Function("CreateLaboratory", "新增實驗室", Icon = "fa-solid fa-plus", Order = 4, Description = "建立新實驗室節點及其屬性資料")]
    [RequiresPermission("ORG_LAB_CREATE")]
    [ProducesResponseType(typeof(Result<LaboratoryDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateLaboratory([FromBody] AddLaboratoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            if (result.IsSuccess)
            {
                _logger.LogInformation("新增實驗室成功: {@Lab}", result.Value);
            }
            else
            {
                _logger.LogError("新增實驗室失敗: {Error}", result.Error);
            }
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing CreateLaboratory.");
            return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// 搬移組織/實驗室樹狀節點（及其完整子樹）
    /// </summary>
    [HttpPut("{id:int}/move")]
    [Function("MoveOrganizationNode", "搬移實驗室節點", Icon = "fa-solid fa-arrows-up-down-left-right", Order = 5, Description = "變更實驗室節點之父階層並搬移完整子樹關聯")]
    [RequiresPermission("ORG_LAB_PUT")]
    [ProducesResponseType(typeof(Result<LaboratoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<LaboratoryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<LaboratoryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveOrganizationNode(
        [FromRoute] int id,
        [FromBody] MoveLaboratoryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (id <= 0)
        {
            return BadRequest(Result.Failure<LaboratoryDto>(
                Error.Validation("ORG_INVALID_ID", "節點識別碼必須大於 0。")
            ));
        }

        try
        {
            var command = new MoveLaboratoryCommand(id, request.NewParentId);
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return result.Error.Code switch
                {
                    "Laboratory.NodeNotFound" or "Laboratory.ParentNotFound" => NotFound(result),
                    "Laboratory.InvalidMove" or "Laboratory.CircularDependency" or "Laboratory.MoveValidationFailed" => BadRequest(result),
                    _ => StatusCode(StatusCodes.Status500InternalServerError, result)
                };
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing MoveOrganizationNode for ID: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// 編輯實驗室
    /// </summary>
    [HttpPut("{id:int}")]
    [Function("EditLaboratory", "編輯實驗室", Icon = "fa-solid fa-pen-to-square", Order = 6, Description = "更新既有實驗室之基本屬性資訊")]
    [RequiresPermission("ORG_LAB_PUT")]
    [ProducesResponseType(typeof(Result<LaboratoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> EditLaboratory([FromRoute] int id, [FromBody] EditLaboratoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (id != command.Id)
        {
            return BadRequest("Route ID does not match Payload ID.");
        }

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogError("Edit 實驗室失敗: {Error}", result.Error);
            }
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing EditLaboratory for ID: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// 刪除實驗室
    /// </summary>
    [HttpDelete("{id:int}")]
    [Function("DeleteLaboratory", "刪除實驗室", Icon = "fa-solid fa-trash", Order = 7, Description = "刪除指定識別碼之實驗室節點")]
    [RequiresPermission("ORG_LAB_DELETE")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteLaboratory([FromRoute] int id, CancellationToken cancellationToken)
    {
        try
        {
            var command = new DeleteLaboratoryCommand(id);
            var result = await _mediator.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogError("Delete 實驗室失敗, ID: {Id}, Error: {Error}", id, result.Error);
            }
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing DeleteLaboratory for ID: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred.");
        }
    }
}