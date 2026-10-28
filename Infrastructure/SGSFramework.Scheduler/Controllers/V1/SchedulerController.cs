using System.Net.Mime;
using System.Security.Claims;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Scheduler;
using SGSFramework.Scheduler.Services;

namespace SGSFramework.Scheduler.Controllers.V1;

/// <summary>週期作業狀態</summary>
public sealed record RecurringJobStatusDto(
    string Id,
    string DisplayName,
    bool ConfigEnabled,
    bool Paused,
    string? PauseReason,
    string? PausedBy,
    DateTimeOffset? PausedAt,
    bool Registered,
    string DefaultCron,
    string Cron,
    string TimeZone,
    string CronSource,
    string BaseCron,
    string BaseTimeZone,
    DateTime? NextExecution,
    ScheduledJobRunDto? LastRun,
    string? Error);

/// <summary>Cron 預覽：是否有效與接下來的執行時間 (UTC)</summary>
public sealed record CronPreviewDto(bool Valid, string? Error, IReadOnlyList<DateTimeOffset> NextOccurrences);

/// <summary>執行紀錄（每次嘗試一筆）</summary>
public sealed record ScheduledJobRunDto(
    Guid Id,
    string JobId,
    string JobName,
    string HangfireJobId,
    int Attempt,
    string Trigger,
    string? TriggeredBy,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    long? DurationMs,
    string? Message,
    string? ServerName);

/// <summary>
/// 排程管理：檢視週期作業、暫停 / 啟動自動排程、立即執行與執行紀錄（Hangfire Dashboard 的 API 版本，套用系統權限控管）。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/scheduler")]
[ControllerTitle("排程管理", Icon = "fa-solid fa-clock", Order = 25, Description = "檢視與管理系統週期排程作業（資料庫備份、定期權限覆核等）")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class SchedulerController(IServiceProvider serviceProvider, DbContext dbContext, TimeProvider timeProvider) : ControllerBase
{
    public sealed record PauseRequest(string? Reason);
    public sealed record ScheduleRequest(string Cron, string? TimeZone);

    /// <summary>列出週期作業、暫停狀態與最近一次執行結果</summary>
    [HttpGet("jobs")]
    [Function("GetScheduledJobs", "排程管理", Icon = "fa-solid fa-clock", Order = 1, Description = "檢視週期排程作業的排程、下次執行時間與最近結果", IsMenu = true, Path = "/sgsframework.system/scheduler")]
    [RequiresPermission("SYSTEM.SCHEDULER.READ", "檢視排程作業")]
    [ProducesResponseType(typeof(IReadOnlyList<RecurringJobStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJobs(CancellationToken cancellationToken = default)
    {
        if (serviceProvider.GetService<IScheduledJobManager>() is not { } manager ||
            serviceProvider.GetService<JobStorage>() is not { } storage)
        {
            return SchedulerDisabled();
        }

        var schedules = await manager.GetSchedulesAsync(cancellationToken).ConfigureAwait(false);
        var jobIds = schedules.Select(s => s.Descriptor.Id).ToList();

        var lastRuns = await dbContext.Set<ScheduledJobRun>().AsNoTracking()
            .Where(r => jobIds.Contains(r.JobId))
            .GroupBy(r => r.JobId)
            .Select(g => g.OrderByDescending(r => r.StartedAt).First())
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var lastRunByJob = lastRuns.ToDictionary(r => r.JobId, StringComparer.OrdinalIgnoreCase);

        Dictionary<string, RecurringJobDto> registered;
        using (var connection = storage.GetConnection())
        {
            registered = connection.GetRecurringJobs(jobIds).Where(j => !j.Removed)
                .ToDictionary(j => j.Id, StringComparer.OrdinalIgnoreCase);
        }

        var result = schedules.Select(s =>
        {
            registered.TryGetValue(s.Descriptor.Id, out var job);
            lastRunByJob.TryGetValue(s.Descriptor.Id, out var lastRun);
            return new RecurringJobStatusDto(
                s.Descriptor.Id, s.Descriptor.DisplayName, s.ConfigEnabled, s.IsPaused,
                s.Setting?.PauseReason, s.IsPaused ? s.Setting?.UpdatedBy : null, s.IsPaused ? s.Setting?.UpdatedAtUtc : null,
                job is not null, s.Descriptor.DefaultCron, s.Cron, s.TimeZone.Id,
                s.CronSource.ToString(), s.BaseCron, s.BaseTimeZone.Id,
                job?.NextExecution, lastRun is null ? null : ToDto(lastRun), job?.Error);
        }).ToList();

        return Ok(result);
    }

    /// <summary>查詢執行紀錄（最新在前）；可依作業與狀態篩選</summary>
    [HttpGet("runs")]
    [Function("GetScheduledJobRuns", "排程執行紀錄", Icon = "fa-solid fa-clock-rotate-left", Order = 2, Description = "檢視排程作業每次執行的結果、耗時與失敗原因")]
    [RequiresPermission("SYSTEM.SCHEDULER.READ", "檢視排程作業")]
    [ProducesResponseType(typeof(IReadOnlyList<ScheduledJobRunDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRuns(
        [FromQuery] string? jobId = null, [FromQuery] string? status = null, [FromQuery] int take = 200,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<ScheduledJobRun>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(jobId)) query = query.Where(r => r.JobId == jobId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);

        var runs = await query.OrderByDescending(r => r.StartedAt).Take(Math.Clamp(take, 1, 1000))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return Ok(runs.Select(ToDto).ToList());
    }

    /// <summary>立即執行一次（不影響原排程；暫停中也可執行）</summary>
    [HttpPost("jobs/{id}/trigger")]
    [Function("TriggerScheduledJob", "立即執行排程作業", Icon = "fa-solid fa-play", Order = 3, Description = "立即執行指定的週期排程作業")]
    [RequiresPermission("SYSTEM.SCHEDULER.EXECUTE", "執行排程作業")]
    public async Task<IActionResult> Trigger([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        if (serviceProvider.GetService<IScheduledJobManager>() is not { } manager) return SchedulerDisabled();

        var (ok, message, hangfireJobId) = await manager.TriggerAsync(id, Actor(), cancellationToken).ConfigureAwait(false);
        return ok
            ? Accepted(new { Succeeded = true, Message = message, HangfireJobId = hangfireJobId })
            : Conflict(ProblemOf(StatusCodes.Status409Conflict, "無法執行排程作業", message));
    }

    /// <summary>驗證 Cron 並預覽接下來 5 次執行時間（設定週期前使用）</summary>
    [HttpGet("cron-preview")]
    [RequiresPermission("SYSTEM.SCHEDULER.READ", "檢視排程作業")]
    [ProducesResponseType(typeof(CronPreviewDto), StatusCodes.Status200OK)]
    public IActionResult PreviewCron([FromQuery] string? cron, [FromQuery] string? timeZone)
    {
        var (valid, error, next) = CronSchedule.Validate(cron, timeZone, timeProvider.GetUtcNow());
        return Ok(new CronPreviewDto(valid, error, next));
    }

    /// <summary>設定自訂週期（優先於組態 Scheduler:Jobs 與程式預設，重啟後仍維持）</summary>
    [HttpPut("jobs/{id}/schedule")]
    [Function("UpdateScheduledJobSchedule", "設定排程週期", Icon = "fa-solid fa-calendar-days", Order = 6, Description = "自訂排程作業的執行週期與時區")]
    [RequiresPermission("SYSTEM.SCHEDULER.MANAGE", "管理排程作業")]
    public async Task<IActionResult> UpdateSchedule([FromRoute] string id, [FromBody] ScheduleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (serviceProvider.GetService<IScheduledJobManager>() is not { } manager) return SchedulerDisabled();

        var (ok, message) = await manager.UpdateScheduleAsync(id, request.Cron, request.TimeZone, Actor(), cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : BadRequest(ProblemOf(StatusCodes.Status400BadRequest, "無法設定排程週期", message));
    }

    /// <summary>還原為組態 / 程式預設週期</summary>
    [HttpDelete("jobs/{id}/schedule")]
    [Function("ResetScheduledJobSchedule", "還原排程週期", Icon = "fa-solid fa-rotate-left", Order = 7, Description = "將排程作業的週期還原為預設值")]
    [RequiresPermission("SYSTEM.SCHEDULER.MANAGE", "管理排程作業")]
    public async Task<IActionResult> ResetSchedule([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        if (serviceProvider.GetService<IScheduledJobManager>() is not { } manager) return SchedulerDisabled();

        var (ok, message) = await manager.UpdateScheduleAsync(id, null, null, Actor(), cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Conflict(ProblemOf(StatusCodes.Status409Conflict, "無法還原排程週期", message));
    }

    /// <summary>暫停自動排程（重啟後仍維持；仍可手動立即執行）</summary>
    [HttpPost("jobs/{id}/pause")]
    [Function("PauseScheduledJob", "暫停排程作業", Icon = "fa-solid fa-pause", Order = 4, Description = "暫停指定作業的自動排程")]
    [RequiresPermission("SYSTEM.SCHEDULER.MANAGE", "管理排程作業")]
    public async Task<IActionResult> Pause([FromRoute] string id, [FromBody] PauseRequest? request, CancellationToken cancellationToken = default)
    {
        if (serviceProvider.GetService<IScheduledJobManager>() is not { } manager) return SchedulerDisabled();

        var (ok, message) = await manager.PauseAsync(id, request?.Reason, Actor(), cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Conflict(ProblemOf(StatusCodes.Status409Conflict, "無法暫停排程作業", message));
    }

    /// <summary>啟動（恢復）自動排程</summary>
    [HttpPost("jobs/{id}/resume")]
    [Function("ResumeScheduledJob", "啟動排程作業", Icon = "fa-solid fa-play", Order = 5, Description = "恢復指定作業的自動排程")]
    [RequiresPermission("SYSTEM.SCHEDULER.MANAGE", "管理排程作業")]
    public async Task<IActionResult> Resume([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        if (serviceProvider.GetService<IScheduledJobManager>() is not { } manager) return SchedulerDisabled();

        var (ok, message) = await manager.ResumeAsync(id, Actor(), cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Conflict(ProblemOf(StatusCodes.Status409Conflict, "無法啟動排程作業", message));
    }

    private static ScheduledJobRunDto ToDto(ScheduledJobRun r) => new(
        r.Id, r.JobId, r.JobName, r.HangfireJobId, r.Attempt, r.Trigger, r.TriggeredBy, r.Status,
        r.StartedAt, r.FinishedAt, r.DurationMs, r.Message, r.ServerName);

    private string Actor() =>
        User.Identity?.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "未知使用者";

    private ObjectResult SchedulerDisabled() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable,
            ProblemOf(StatusCodes.Status503ServiceUnavailable, "排程服務未啟用", "請於組態 Scheduler:Enabled 啟用排程服務。"));

    private ProblemDetails ProblemOf(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path
    };
}
