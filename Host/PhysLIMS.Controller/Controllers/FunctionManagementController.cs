using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.ApiInfrastructure.DTOs;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;

namespace SGSFramework.ApiInfrastructure.Controllers;

/// <summary>
/// 系統功能與控制器中繼資料管理控制器
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/function-managements")]
[ControllerTitle("系統功能管理", Icon = "fa-solid fa-gears", Order = 22, Description = "提供查詢與管理系統註冊之所有 Controller 與 Function 中繼資料清單與狀態維護")]
[RequiresPermission("SYSTEM.FUNCTIONMANAGEMENT.READ", "系統功能管理")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public sealed class FunctionManagementController : ApiControllerBase
{
    private readonly IControllerMetadataService _controllerMetadataService;
    private readonly ILogger<FunctionManagementController> _logger;

    public FunctionManagementController(
        IControllerMetadataService controllerMetadataService,
        ILogger<FunctionManagementController> logger)
    {
        ArgumentNullException.ThrowIfNull(controllerMetadataService);
        ArgumentNullException.ThrowIfNull(logger);

        _controllerMetadataService = controllerMetadataService;
        _logger = logger;
    }

    /// <summary>
    /// 取得系統所有功能中繼資料清單
    /// </summary>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>控制器與功能中繼資料清單集合</returns>
    [HttpGet]
    [Function("GetAllFunctionMetadatas", "系統功能列表", Icon = "fa-solid fa-list", Order = 1, Description = "取得系統所有 Controller 與 Function 中繼資料清單")]
    [RequiresPermission("SYSTEM.FUNCTIONMANAGEMENT.READ")]
    [EndpointSummary("系統功能列表")]
    [EndpointDescription("取得系統內所有已加載的功能相關資料。")]
    [ProducesResponseType(typeof(IEnumerable<ControllerMetadataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllFunctionMetadatas(CancellationToken cancellationToken = default)
    {
        try
        {
            var metadatas = await _controllerMetadataService.GetAllControllerMetadatasAsync(cancellationToken);
            int recordCount = metadatas is ICollection<ControllerMetadataDto> coll ? coll.Count : metadatas.Count();

            _logger.LogInformation("成功查詢系統功能中繼資料列表，總筆數: {Count}", recordCount);
            return Ok(metadatas);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("查詢功能中繼資料列表之請求已被使用者取消。");
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢功能中繼資料列表時發生非預期異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "查詢功能中繼資料列表時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 變更指定功能之停用/啟用狀態
    /// </summary>
    /// <param name="id">功能識別碼 (Guid)</param>
    /// <param name="request">狀態更新請求內容</param>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>更新後的功能中繼資料</returns>
    [HttpPatch("functions/{id:guid}/status")]
    [Function("UpdateFunctionStatus", "更新功能狀態", Icon = "fa-solid fa-toggle-on", Order = 2, Description = "指定系統功能動態停用或啟用。")]
    [RequiresPermission("SYSTEM.FUNCTIONMANAGEMENT.UPDATESTATUS","更新功能狀態")]
    [EndpointSummary("更新功能狀態")]
    [EndpointDescription("指定系統功能停用或啟用。")]
    [ProducesResponseType(typeof(ControllerMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateFunctionStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateFunctionStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (id == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求參數",
                Detail = "功能識別碼 (id) 不可為空 Guid。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var updatedMetadata = await _controllerMetadataService.UpdateFunctionStatusAsync(id, request.IsActive, request.Reason, cancellationToken);

            _logger.LogInformation("功能狀態成功變更。ID: {Id}, IsActive: {IsActive}", id, request.IsActive);
            return Ok(updatedMetadata);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "欲更新狀態的功能項目不存在。ID: {Id}", id);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "資源不存在",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("變更功能狀態之請求已被使用者取消。ID: {Id}", id);
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新功能狀態時發生非預期錯誤。ID: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "變更功能啟用狀態時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }
}