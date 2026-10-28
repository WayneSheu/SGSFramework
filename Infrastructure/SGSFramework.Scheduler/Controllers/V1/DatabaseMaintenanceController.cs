using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Scheduler.Maintenance;

namespace SGSFramework.Scheduler.Controllers.V1;

/// <summary>
/// 資料庫維護：備份、日誌清除。供排程服務以系統帳號 (scope: database.backup / logs.cleanup) 呼叫，亦開放具權限的管理者手動執行。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/database-maintenance")]
[ControllerTitle("資料庫維護", Icon = "fa-solid fa-database", Order = 26, Description = "資料庫備份、日誌清除等維護作業，可由排程服務以系統帳號定期執行")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class DatabaseMaintenanceController(IDatabaseBackupService backupService, ILogCleanupService logCleanupService) : ControllerBase
{
    public sealed record BackupRequest(bool Differential);

    /// <summary>執行資料庫備份（完整或差異），完成後回傳備份檔資訊</summary>
    [HttpPost("backups")]
    [Function("BackupDatabase", "資料庫備份", Icon = "fa-solid fa-floppy-disk", Order = 1, Description = "執行資料庫完整或差異備份並驗證備份檔")]
    [RequiresSystemScope("database.backup", Description = "執行資料庫備份", AllowUsers = true)]
    [RequiresPermission("SYSTEM.DATABASEMAINTENANCE.BACKUP", "執行資料庫備份")]
    [ProducesResponseType(typeof(DatabaseBackupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Backup([FromBody] BackupRequest? request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await backupService.BackupAsync(request?.Differential ?? false, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (DatabaseBackupException ex)
        {
            // 422：可預期的失敗（權限、設定、執行中），呼叫端 HTTP 層不重試，交由排程的重試策略處理
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "資料庫備份失敗",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 依保留天數清除過期日誌（Outbox、排程執行紀錄、管理異動軌跡）；Ledger 日誌資料表禁止刪除，僅回報筆數
    /// </summary>
    [HttpPost("log-cleanup")]
    [Function("CleanupLogs", "日誌清除", Icon = "fa-solid fa-broom", Order = 2, Description = "依保留天數清除過期的日誌與訊息紀錄")]
    [RequiresSystemScope("logs.cleanup", Description = "清除過期日誌", AllowUsers = true)]
    [RequiresPermission("SYSTEM.DATABASEMAINTENANCE.LOGCLEANUP", "清除過期日誌")]
    [ProducesResponseType(typeof(LogCleanupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CleanupLogs(CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await logCleanupService.CleanupAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (LogCleanupException ex)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "日誌清除失敗",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
    }
}
