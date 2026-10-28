using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SGSFramework.Core.Abstractions.Entities.Scheduler;
using SGSFramework.Scheduler.Abstractions;
using SGSFramework.Scheduler.Services;

namespace SGSFramework.Scheduler.Hosting;

/// <summary>
/// 記錄排程作業執行歷程 (core.ScheduledJobRuns) 並於最終失敗時告警：
/// <list type="bullet">
/// <item>開始執行 → 新增一筆 Running（每次嘗試一筆，含觸發方式與觸發人員）</item>
/// <item>狀態轉為 Succeeded / Scheduled（重試）/ Failed / Deleted → 更新結果與耗時</item>
/// <item>Failed（不再重試）→ 寫入 ModuleName = Scheduler 的 Error 日誌，經告警規則（同系統日誌）寄送 Email</item>
/// </list>
/// 紀錄失敗不影響作業本身。
/// </summary>
internal sealed class JobRunRecorderFilter(
    IServiceScopeFactory scopeFactory,
    IEnumerable<RecurringJobDescriptor> descriptors,
    TimeProvider timeProvider,
    ILogger<JobRunRecorderFilter> logger) : JobFilterAttribute, IServerFilter, IApplyStateFilter
{
    /// <summary>告警規則可用「模組包含 Scheduler」篩選排程失敗</summary>
    public const string AlertModuleName = "Scheduler";

    private readonly Dictionary<Type, RecurringJobDescriptor> _byType =
        descriptors.GroupBy(d => d.JobType).ToDictionary(g => g.Key, g => g.First());

    public void OnPerforming(PerformingContext context)
    {
        var job = context.BackgroundJob;
        if (Describe(job.Job?.Type) is not { } descriptor) return;

        try
        {
            var triggeredBy = context.GetJobParameter<string>(ScheduledJobManager.TriggeredByParameter);
            var run = new ScheduledJobRun
            {
                JobId = context.GetJobParameter<string>(ScheduledJobManager.JobIdParameter)
                        ?? context.GetJobParameter<string>("RecurringJobId")
                        ?? descriptor.Id,
                JobName = descriptor.DisplayName,
                HangfireJobId = job.Id,
                Attempt = context.GetJobParameter<int>("RetryCount") + 1,
                Trigger = triggeredBy is null ? ScheduledJobTriggers.Schedule : ScheduledJobTriggers.Manual,
                TriggeredBy = triggeredBy,
                Status = ScheduledJobRunStatuses.Running,
                StartedAt = timeProvider.GetUtcNow(),
                ServerName = context.ServerId
            };

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            db.Set<ScheduledJobRun>().Add(run);
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Scheduler] 寫入執行紀錄失敗（Hangfire 作業 {HangfireJobId}）。", job.Id);
        }
    }

    public void OnPerformed(PerformedContext context)
    {
        // 結果於狀態轉換時記錄（此時才知道是否會重試）
    }

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.OldStateName != ProcessingState.StateName) return;
        if (Describe(context.BackgroundJob.Job?.Type) is not { } descriptor) return;

        var (status, message) = context.NewState switch
        {
            SucceededState succeeded => (ScheduledJobRunStatuses.Succeeded, ReadResult(succeeded.Result)),
            FailedState failed => (ScheduledJobRunStatuses.Failed, failed.Exception?.Message ?? failed.Reason),
            ScheduledState scheduled => (ScheduledJobRunStatuses.Retrying, scheduled.Reason),
            DeletedState deleted => (ScheduledJobRunStatuses.Failed, deleted.Reason ?? "作業已被取消或刪除。"),
            _ => (null, null)
        };
        if (status is null) return;

        var hangfireJobId = context.BackgroundJob.Id;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            var now = timeProvider.GetUtcNow();

            var run = db.Set<ScheduledJobRun>()
                .Where(r => r.HangfireJobId == hangfireJobId && r.Status == ScheduledJobRunStatuses.Running)
                .OrderByDescending(r => r.Attempt)
                .FirstOrDefault();

            if (run is null)
            {
                // 開始時未寫入（例如資料庫暫時中斷），補一筆避免遺失結果
                run = new ScheduledJobRun
                {
                    JobId = ReadParameter(context.Connection, hangfireJobId, ScheduledJobManager.JobIdParameter)
                            ?? ReadParameter(context.Connection, hangfireJobId, "RecurringJobId")
                            ?? descriptor.Id,
                    JobName = descriptor.DisplayName,
                    HangfireJobId = hangfireJobId,
                    Attempt = int.TryParse(ReadParameter(context.Connection, hangfireJobId, "RetryCount"), out var retry) ? retry + 1 : 1,
                    TriggeredBy = ReadParameter(context.Connection, hangfireJobId, ScheduledJobManager.TriggeredByParameter),
                    StartedAt = now
                };
                run.Trigger = run.TriggeredBy is null ? ScheduledJobTriggers.Schedule : ScheduledJobTriggers.Manual;
                db.Set<ScheduledJobRun>().Add(run);
            }

            run.Status = status;
            run.FinishedAt = now;
            run.DurationMs = (long)(now - run.StartedAt).TotalMilliseconds;
            run.Message = Truncate(message, 2000);
            db.SaveChanges();

            Report(run, descriptor);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Scheduler] 更新執行紀錄失敗（Hangfire 作業 {HangfireJobId}）。", hangfireJobId);
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }

    /// <summary>
    /// 最終失敗寫 Error（觸發告警規則寄送 Email，同系統日誌）；等待重試僅 Warning，避免每次嘗試都寄信
    /// </summary>
    private void Report(ScheduledJobRun run, RecurringJobDescriptor descriptor)
    {
        using var module = LogContext.PushProperty("ModuleName", AlertModuleName);
        using var operation = LogContext.PushProperty("Operation", run.JobId);

        switch (run.Status)
        {
            case ScheduledJobRunStatuses.Failed:
                logger.LogError("[Scheduler] 排程作業「{JobName}」({JobId}) 執行失敗（第 {Attempt} 次，{Trigger}）：{Reason}",
                    descriptor.DisplayName, run.JobId, run.Attempt, TriggerText(run), run.Message);
                break;
            case ScheduledJobRunStatuses.Retrying:
                logger.LogWarning("[Scheduler] 排程作業「{JobName}」({JobId}) 第 {Attempt} 次執行失敗，已排定重試：{Reason}",
                    descriptor.DisplayName, run.JobId, run.Attempt, run.Message);
                break;
            case ScheduledJobRunStatuses.Succeeded:
                logger.LogInformation("[Scheduler] 排程作業「{JobName}」({JobId}) 執行成功，耗時 {DurationMs} 毫秒（{Trigger}）。",
                    descriptor.DisplayName, run.JobId, run.DurationMs, TriggerText(run));
                break;
        }
    }

    private static string TriggerText(ScheduledJobRun run) =>
        run.Trigger == ScheduledJobTriggers.Manual ? $"{run.TriggeredBy} 手動執行" : "排程觸發";

    private RecurringJobDescriptor? Describe(Type? jobType) =>
        jobType is not null && typeof(IRecurringJob).IsAssignableFrom(jobType) && _byType.TryGetValue(jobType, out var d) ? d : null;

    private static string? ReadParameter(IStorageConnection connection, string jobId, string name)
    {
        var raw = connection.GetJobParameter(jobId, name);
        return string.IsNullOrEmpty(raw) ? null : SerializationHelper.Deserialize<string>(raw);
    }

    /// <summary>作業回傳的摘要字串（Hangfire 以 JSON 序列化存放於 SucceededState.Result）</summary>
    private static string? ReadResult(object? result)
    {
        if (result is not string raw || string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            return SerializationHelper.Deserialize<string>(raw);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return raw;
        }
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
