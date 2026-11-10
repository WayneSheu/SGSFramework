using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.LOG.Infrastructure.Services;
using SGSFramework.Core.Abstractions.Attributes;
using System.Net.Mime;

namespace SGS.Modules.LOG.Controllers.V1;

/// <summary>
/// 日誌庫年度換庫（半自動）：排程於年初自動「準備」（備份並還原為待核准的冷庫），管理者核准後才「執行換庫」
/// （凍結熱庫寫入、收尾冷庫、核對與驗證、轉唯讀、刪除舊熱庫、建立新熱庫）。
/// 準備階段可由排程服務以系統帳號 (scope: logs.rotation.prepare) 呼叫；核准與取消只開放具核准權限的管理者（不接受系統帳號）。
/// </summary>
[Authorize]
[ApiController]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/log-rotation")]
[ControllerTitle("日誌換庫管理", Icon = "fa-solid fa-arrows-rotate", Order = 98, Description = "日誌庫年度換庫（冷熱分層）：準備、核准執行、取消")]
[RequiresPermission("LOG.LOGROTATION.READ", "日誌換庫管理")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class LogRotationController(LogRotationService rotationService) : ControllerBase
{
    /// <param name="Period">要封存的期間：2026（年度）、2026H1（上半年 1–6 月）、2026H2（下半年 7–12 月），冷庫名稱 {熱庫}_{期間}；未指定時使用建議期間（依 LogRotation:Granularity，並跳過已有冷庫的期間）</param>
    public sealed record PrepareRequest(string? Period);

    /// <summary>目前換庫狀態：換庫帳號是否就緒、熱庫與待核准冷庫、既有冷庫清單</summary>
    [HttpGet("status")]
    [Function("GetLogRotationStatus", "換庫狀態", Icon = "fa-solid fa-arrows-rotate", Order = 1, Description = "檢視年度換庫的目前狀態與待核准項目", IsMenu = true, Path = "/sgsframework.system/logrotation")]
    [RequiresPermission("LOG.LOGROTATION.READ")]
    [ProducesResponseType(typeof(LogRotationStatus), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await rotationService.GetStatusAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
    }

    /// <summary>準備換庫：完整備份熱庫並還原為「還原中」的冷庫，等待管理者核准。熱庫全程可寫，不影響服務。</summary>
    [HttpPost("prepare")]
    [Function("PrepareLogRotation", "準備換庫", Icon = "fa-solid fa-box-archive", Order = 2, Description = "備份熱庫並還原為待核准的冷庫（可由排程於年初自動執行）")]
    [RequiresSystemScope("logs.rotation.prepare", Description = "準備日誌庫年度換庫（備份並還原冷庫）", AllowUsers = true)]
    [RequiresPermission("LOG.LOGROTATION.PREPARE", "準備日誌庫年度換庫")]
    [ProducesResponseType(typeof(LogRotationPrepareResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Prepare([FromBody] PrepareRequest? request, CancellationToken cancellationToken = default) =>
        RunAsync("準備換庫失敗", async () => Ok(await rotationService.PrepareAsync(request?.Period, Caller(), cancellationToken)));

    /// <summary>核准並執行換庫（不可逆）：凍結熱庫寫入 → 收尾冷庫並核對 / 驗證 → 冷庫轉唯讀 → 刪除舊熱庫 → 建立新熱庫。</summary>
    [HttpPost("finalize")]
    [Function("FinalizeLogRotation", "核准並執行換庫", Icon = "fa-solid fa-circle-check", Order = 3, Description = "核准待處理的換庫並執行（不可逆：刪除舊熱庫、建立新熱庫）")]
    [RequiresPermission("LOG.LOGROTATION.APPROVE", "核准並執行日誌庫換庫")]
    [ProducesResponseType(typeof(LogRotationFinalizeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Finalize() =>
        // 不可逆且包含凍結 / 刪庫步驟：不因用戶端斷線（關閉瀏覽器、逾時）而中途取消，避免停在半完成狀態
        RunAsync("執行換庫失敗", async () => Ok(await rotationService.FinalizeAsync(Caller(), CancellationToken.None)));

    /// <summary>取消待核准的換庫：刪除還原中的冷庫（備份檔保留）。</summary>
    [HttpPost("cancel")]
    [Function("CancelLogRotation", "取消換庫", Icon = "fa-solid fa-ban", Order = 4, Description = "取消待核准的換庫並刪除還原中的冷庫")]
    [RequiresPermission("LOG.LOGROTATION.APPROVE", "取消日誌庫換庫")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Cancel() =>
        RunAsync("取消換庫失敗", async () => Ok(new { Message = await rotationService.CancelAsync(Caller(), CancellationToken.None) }));

    private string Caller() => User.Identity?.Name ?? User.FindFirst("sub")?.Value ?? "unknown";

    private async Task<IActionResult> RunAsync(string title, Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (LogRotationException ex)
        {
            // 422：可預期的失敗（未設定、權限、前置條件、核對不一致），排程 HTTP 層不重試
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = title,
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(499);
        }
    }
}
