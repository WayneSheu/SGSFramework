using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Paginations;
using SGSFramework.SystemLog.DTOs;
using SGSFramework.SystemLog.Services;
using System.Net.Mime;

namespace SGSFramework.SystemLog.Controllers.V1;

/// <summary>
/// 資料日誌管理控制器：供稽核人員查閱資料表異動的新 / 舊值紀錄 (core.AuditLogs)
/// </summary>
[Authorize]
[ApiController]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/system/audit-logs")]
[ControllerTitle("資料日誌管理", Icon = "fa-solid fa-clock-rotate-left", Order = 95, Description = "檢視資料異動稽核日誌（新 / 舊值比對）")]
[RequiresPermission("SYSTEM.AUDITLOG.READ", "資料日誌管理")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public class AuditLogManagementController : ControllerBase
{
    private readonly IAuditLogQueryService _queryService;
    private readonly ILogger<AuditLogManagementController> _logger;

    public AuditLogManagementController(IAuditLogQueryService queryService, ILogger<AuditLogManagementController> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>分頁條件查詢資料日誌</summary>
    [HttpPost("search")]
    [Function("QueryAuditLogs", "查詢資料日誌", Icon = "fa-solid fa-clock-rotate-left", Order = 1, Description = "檢索 core.AuditLogs 資料異動稽核紀錄", IsMenu = true, Path = "/sgsframework.system/auditlog")]
    [RequiresPermission("SYSTEM.AUDITLOG.READ")]
    [ProducesResponseType(typeof(PagedResult<AuditLogListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Search([FromBody] AuditLogQueryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _queryService.GetAuditLogsAsync(request, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (ArgumentException ex)
        {
            return BadSource(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢資料日誌清單時發生例外");
            return Problem500("查詢資料日誌時發生錯誤。");
        }
    }

    /// <summary>依 ID 取得資料日誌完整明細（含新 / 舊值）</summary>
    [HttpGet("{id:long}")]
    [Function("GetAuditLogById", "取得資料日誌明細", Icon = "fa-solid fa-file-lines", Order = 2, Description = "依主鍵讀取資料異動的完整新 / 舊值與防篡改雜湊鏈路")]
    [RequiresPermission("SYSTEM.AUDITLOG.READ")]
    [ProducesResponseType(typeof(AuditLogDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] long id, [FromQuery] string? source = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var log = await _queryService.GetAuditLogByIdAsync(id, source, cancellationToken);
            return log is null
                ? NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "資源不存在",
                    Detail = $"找不到 ID 為 {id} 的資料日誌。",
                    Instance = HttpContext.Request.Path
                })
                : Ok(log);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (ArgumentException ex)
        {
            return BadSource(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得資料日誌明細時發生例外 Id: {Id}", id);
            return Problem500("取得資料日誌明細時發生錯誤。");
        }
    }

    /// <summary>取得資料日誌中出現過的資料表名稱（供篩選下拉選單）</summary>
    [HttpGet("tables")]
    [Function("GetAuditLogTables", "取得資料日誌資料表清單", Icon = "fa-solid fa-table-list", Order = 3, Description = "列出資料日誌涵蓋的資料表名稱")]
    [RequiresPermission("SYSTEM.AUDITLOG.READ")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTables([FromQuery] string? source = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _queryService.GetTableNamesAsync(source, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (ArgumentException ex)
        {
            return BadSource(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得資料日誌資料表清單時發生例外");
            return Problem500("取得資料表清單時發生錯誤。");
        }
    }

    /// <summary>列出可查詢的資料日誌來源（主系統與各已載入、含 AuditLogs 的外掛模組）</summary>
    [HttpGet("sources")]
    [Function("GetAuditLogSources", "取得資料日誌來源", Icon = "fa-solid fa-layer-group", Order = 4, Description = "列出主系統與各外掛模組的資料日誌來源")]
    [RequiresPermission("SYSTEM.AUDITLOG.READ")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogSourceDto>), StatusCodes.Status200OK)]
    public IActionResult GetSources() => Ok(_queryService.GetSources());

    private ObjectResult BadSource(string detail) =>
        StatusCode(StatusCodes.Status400BadRequest, new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "無效的資料日誌來源",
            Detail = detail,
            Instance = HttpContext.Request.Path
        });

    private ObjectResult Problem500(string detail) =>
        StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "伺服器內部錯誤",
            Detail = detail,
            Instance = HttpContext.Request.Path
        });
}
