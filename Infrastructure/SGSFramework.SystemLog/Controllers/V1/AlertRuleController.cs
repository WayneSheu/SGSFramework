using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.SystemLog.DTOs;
using SGSFramework.SystemLog.Services;
using System.Net.Mime;

namespace SGSFramework.SystemLog.Controllers.V1;

/// <summary>
/// 告警規則管理控制器：於執行期動態調整系統日誌 / 資安日誌的告警條件（不需重啟）
/// </summary>
[Authorize]
[ApiController]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/system/alert-rules")]
[ControllerTitle("告警規則管理", Icon = "fa-solid fa-bell", Order = 96, Description = "動態設定系統日誌、資安日誌的告警規則")]
[RequiresPermission("SYSTEM.ALERTRULE.READ", "告警規則管理")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public class AlertRuleController : ControllerBase
{
    private readonly IAlertRuleService _service;
    private readonly ILogger<AlertRuleController> _logger;

    public AlertRuleController(IAlertRuleService service, ILogger<AlertRuleController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>列出所有告警規則（含停用）</summary>
    [HttpGet]
    [Function("GetAlertRules", "查詢告警規則", Icon = "fa-solid fa-bell", Order = 1, Description = "列出所有告警規則", IsMenu = true, Path = "/sgsframework.system/alertrules")]
    [RequiresPermission("SYSTEM.ALERTRULE.READ")]
    [ProducesResponseType(typeof(IReadOnlyList<AlertRuleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.GetAllAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢告警規則時發生例外");
            return Problem500("查詢告警規則時發生錯誤。");
        }
    }

    /// <summary>新增告警規則</summary>
    [HttpPost]
    [Function("CreateAlertRule", "新增告警規則", Icon = "fa-solid fa-plus", Order = 2, Description = "新增一條告警規則，儲存後立即生效")]
    [RequiresPermission("SYSTEM.ALERTRULE.MANAGE", "管理告警規則")]
    [ProducesResponseType(typeof(AlertRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Create([FromBody] AlertRuleSaveRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => Ok(await _service.CreateAsync(request, cancellationToken)), "新增告警規則", cancellationToken);

    /// <summary>修改告警規則</summary>
    [HttpPut("{id:guid}")]
    [Function("UpdateAlertRule", "修改告警規則", Icon = "fa-solid fa-pen", Order = 3, Description = "修改一條告警規則，儲存後立即生效")]
    [RequiresPermission("SYSTEM.ALERTRULE.MANAGE")]
    [ProducesResponseType(typeof(AlertRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> Update([FromRoute] Guid id, [FromBody] AlertRuleSaveRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            var updated = await _service.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound(Problem(404, "資源不存在", $"找不到告警規則 {id}。")) : Ok(updated);
        }, "修改告警規則", cancellationToken);

    /// <summary>刪除告警規則</summary>
    [HttpDelete("{id:guid}")]
    [Function("DeleteAlertRule", "刪除告警規則", Icon = "fa-solid fa-trash", Order = 4, Description = "刪除一條告警規則，立即生效")]
    [RequiresPermission("SYSTEM.ALERTRULE.MANAGE")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
            await _service.DeleteAsync(id, cancellationToken)
                ? NoContent()
                : NotFound(Problem(404, "資源不存在", $"找不到告警規則 {id}。")),
            "刪除告警規則", cancellationToken);

    /// <summary>規則測試：模擬一筆日誌，檢視會命中哪條規則（不會實際發送告警）</summary>
    [HttpPost("test")]
    [Function("TestAlertRule", "測試告警規則", Icon = "fa-solid fa-vial", Order = 5, Description = "模擬一筆日誌，檢視會命中哪條告警規則")]
    [RequiresPermission("SYSTEM.ALERTRULE.READ")]
    [ProducesResponseType(typeof(AlertRuleTestResult), StatusCodes.Status200OK)]
    public Task<IActionResult> Test([FromBody] AlertRuleTestRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => Task.FromResult<IActionResult>(Ok(_service.Test(request))), "測試告警規則", cancellationToken);

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action, string operation, CancellationToken cancellationToken)
    {
        try
        {
            return await action();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(Problem(400, "參數無效", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.InnerException is null)
        {
            return Conflict(Problem(409, "資料衝突", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation}時發生例外", operation);
            return Problem500($"{operation}時發生錯誤。");
        }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path
    };

    private ObjectResult Problem500(string detail) =>
        StatusCode(StatusCodes.Status500InternalServerError, Problem(500, "伺服器內部錯誤", detail));
}
