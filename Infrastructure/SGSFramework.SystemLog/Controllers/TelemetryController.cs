#nullable enable
namespace SGSFramework.SystemLog.Controllers.v1;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.DTOs;
using SGSFramework.SystemLog.DTOs;

/// <summary>
/// 前端遙測與例外監控控制器
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/telemetry")]
[Produces("application/json")]
[ControllerTitle("遙測與監控", Icon = "fa-solid fa-chart-line", Order = 90, Description = "提供前端 UI/RCL 異常監控、未捕捉 Exception 與遙測日誌上報服務")]
public sealed class TelemetryController(
    ILogger<TelemetryController> logger,
    ISecurityLogger securityLogger) : ApiControllerBase
{
    private readonly ILogger<TelemetryController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ISecurityLogger _securityLogger = securityLogger ?? throw new ArgumentNullException(nameof(securityLogger));

    /// <summary>
    /// 接收並記錄前端上報之例外與遙測日誌
    /// </summary>
    [HttpPost("logs")]
    [AllowAnonymous] // 允許未驗證或 Token 過期請求上報例外，避免日誌遺失
    [Function("ReceiveClientLog", "上報前端遙測日誌", Icon = "fa-solid fa-bug", Order = 1, Description = "接收並記錄前端未捕捉 Exception 與應用程式崩潰資訊")]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ReceiveClientLogAsync(
        [FromBody] ClientLogPayloadDto payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (string.IsNullOrWhiteSpace(payload.Message) && string.IsNullOrWhiteSpace(payload.ExceptionType))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求數據",
                Detail = "例外訊息 (Message) 與型別 (ExceptionType) 不可同時為空。",
                Instance = HttpContext.Request.Path
            });
        }

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";
        string userAgent = Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown User-Agent";

        try
        {
            // 寫入 Structured Logging 供 Enterprise Serilog / SEQ / ELK 解析
            _logger.LogError(
                "[ClientTelemetry] Context: {Context} | Policy: {Policy} | DeviceId: {DeviceId} | IP: {ClientIp} | UserAgent: {UserAgent}\nExceptionType: {ExceptionType}\nMessage: {Message}\nStackTrace: {StackTrace}",
                payload.Context,
                payload.Policy,
                deviceId,
                clientIp,
                userAgent,
                payload.ExceptionType,
                payload.Message,
                payload.StackTrace ?? "N/A");

            // 記錄安全性與稽核軌跡
            _securityLogger.LogSecurity(
                eventCode: "SEC-500-CLIENT-EXCEPTION",
                eventCategory: "Telemetry.Exception",
                userId: UserInfo.UserId ?? "Anonymous",
                clientIp: clientIp,
                messageTemplate: "前端捕捉到未處理例外。元件: {Context}, 策略: {Policy}, 類型: {ExceptionType}",
                payload.Context,
                payload.Policy,
                payload.ExceptionType
            );

            return Ok(new MessageResponseDto
            {
                Message = "遙測日誌上報成功。"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理前端遙測日誌時發生伺服器內部異常。");

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "處理遙測日誌時發生非預期錯誤。",
                Instance = HttpContext.Request.Path
            });
        }
    }
}