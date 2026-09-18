using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.SystemLog.DTOs;
using SGSFramework.SystemLog.Services;
using System.Net.Mime;

namespace SGSFramework.SystemLog.Controllers;

/// <summary>
/// 系統與資安總帳日誌管理控制器 (資料庫版)
/// </summary>
[ApiController]
[ApiVersion("v1")]
[Route("api/system/log-manager")]
[ControllerTitle("系統日誌管理", Icon = "fa-solid fa-receipt", Order = 90, Description = "動態調整 Serilog 紀錄層級與線上檢視 core.SystemLogs / core.SecurityLog 資料庫紀錄")]
[RequiresPermission("SYSTEM.LOGMANAGER.READ", "系統日誌管理")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public class LogManagerController : ControllerBase
{
    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly ISystemLogQueryService _logQueryService;
    private readonly ILogger<LogManagerController> _logger;

    public LogManagerController(
        LoggingLevelSwitch levelSwitch,
        ISystemLogQueryService logQueryService,
        ILogger<LogManagerController> logger)
    {
        _levelSwitch = levelSwitch ?? throw new ArgumentNullException(nameof(levelSwitch));
        _logQueryService = logQueryService ?? throw new ArgumentNullException(nameof(logQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 取得當前全系統的 Serilog 最小日誌輸出層級
    /// </summary>
    [HttpGet("current-level")]
    [Function("GetLevel", "取得日誌層級", Icon = "fa-solid fa-gauge-high", Order = 1, Description = "查詢系統當前動態生效中的日誌輸出層級")]
    [RequiresPermission("SYSTEM.LOGMANAGER.READ")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetLevel()
    {
        return Ok(new
        {
            MinimumLevel = _levelSwitch.MinimumLevel.ToString(),
            CheckedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// 動態設定全系統日誌輸出層級 (立即生效，無需重啟服務)
    /// </summary>
    [HttpPost("set-level")]
    [Function("SetLevel", "設定日誌層級", Icon = "fa-solid fa-sliders", Order = 2, Description = "即時修改 Serilog 最小日誌輸出層級 (Verbose, Debug, Information, Warning, Error, Fatal)")]
    [RequiresPermission("SYSTEM.LOGMANAGER.SETLEVE", "設定日誌輸出層級")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult SetLevel([FromBody] LogEventLevel level)
    {
        if (!Enum.IsDefined(typeof(LogEventLevel), level))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求參數",
                Detail = "傳入的日誌層級數值無效。",
                Instance = HttpContext.Request.Path
            });
        }

        _levelSwitch.MinimumLevel = level;
        return Ok(new
        {
            Message = $"系統日誌層級已成功變更為: {level}",
            NewLevel = level.ToString(),
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// 分頁條件查詢系統日誌 (core.SystemLogs)
    /// </summary>
    [HttpPost("system-logs/search")]
    [Function("QuerySystemLogs", "查詢系統日誌", Icon = "fa-solid fa-database", Order = 3, Description = "檢索儲存於 core.SystemLogs 表之系統營運日誌")]
    [RequiresPermission("SYSTEM.LOGMANAGER.READ")]
    [ProducesResponseType(typeof(PagedResult<SystemLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> QuerySystemLogs([FromBody] SystemLogQueryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _logQueryService.GetSystemLogsAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢系統日誌清單時發生例外");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "查詢系統日誌資料庫時發生錯誤。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 分頁條件查詢安全性總帳日誌 (core.SecurityLog)
    /// </summary>
    [HttpPost("security-logs/search")]
    [Function("QuerySecurityLogs", "查詢資安總帳日誌", Icon = "fa-solid fa-shield-halved", Order = 4, Description = "檢索儲存於 core.SecurityLog 防篡改總帳表之資安稽核紀錄")]
    [RequiresPermission("SYSTEM.LOGMANAGER.READ")]
    [ProducesResponseType(typeof(PagedResult<SecurityLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> QuerySecurityLogs([FromBody] SecurityLogQueryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _logQueryService.GetSecurityLogsAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢安全性總帳日誌時發生例外");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "查詢資安總帳日誌資料庫時發生錯誤。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 依 ID 取得系統日誌單筆詳細資訊
    /// </summary>
    [HttpGet("system-logs/{id:long}")]
    [Function("GetSystemLogById", "取得系統日誌明細", Icon = "fa-solid fa-file-lines", Order = 5, Description = "依據主鍵 ID 讀取系統日誌完整 Payload 與例外堆疊")]
    [RequiresPermission("SYSTEM.LOGMANAGER.READ")]
    [ProducesResponseType(typeof(SystemLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSystemLogById([FromRoute] long id, CancellationToken cancellationToken = default)
    {
        var log = await _logQueryService.GetSystemLogByIdAsync(id, cancellationToken);
        if (log == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "資源不存在",
                Detail = $"找不到 ID 為 {id} 的系統日誌。",
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(log);
    }
}