using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Abstractions.Entities.Scheduler;
using SGSFramework.Scheduler.Abstractions;
using SGSFramework.Scheduler.Hosting;
using SGSFramework.Scheduler.Options;

namespace SGSFramework.Scheduler.Services;

/// <summary>週期來源：程式預設、組態 Scheduler:Jobs、排程管理頁自訂</summary>
public enum CronSource
{
    Default,
    Config,
    User
}

/// <summary>作業的有效排程：程式預設 → Scheduler:Jobs 組態覆寫 → 排程管理頁自訂週期 / 暫停狀態</summary>
/// <param name="BaseCron">不含使用者自訂的週期（還原預設後的值）</param>
/// <param name="BaseTimeZone">不含使用者自訂的時區</param>
public sealed record EffectiveJobSchedule(
    RecurringJobDescriptor Descriptor,
    bool ConfigEnabled,
    string Cron,
    TimeZoneInfo TimeZone,
    CronSource CronSource,
    string BaseCron,
    TimeZoneInfo BaseTimeZone,
    ScheduledJobSetting? Setting)
{
    public bool IsPaused => Setting?.IsPaused == true;

    /// <summary>實際交由 Hangfire 自動排程（組態啟用且未暫停）</summary>
    public bool IsScheduled => ConfigEnabled && !IsPaused;
}

/// <summary>
/// 排程作業管理：同步 Hangfire 週期作業、暫停 / 啟動、手動執行。
/// 暫停狀態存於 core.ScheduledJobSettings（IAuditable，變更寫入稽核），重啟後仍維持。
/// </summary>
public interface IScheduledJobManager
{
    Task<IReadOnlyList<EffectiveJobSchedule>> GetSchedulesAsync(CancellationToken cancellationToken = default);

    /// <summary>依有效排程新增 / 更新 / 移除所有 Hangfire 週期作業（啟動時呼叫）</summary>
    Task SyncAllAsync(CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> PauseAsync(string jobId, string? reason, string actor, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> ResumeAsync(string jobId, string actor, CancellationToken cancellationToken = default);

    /// <summary>設定自訂週期；cron 為 null 時還原為組態 / 程式預設</summary>
    Task<(bool Succeeded, string Message)> UpdateScheduleAsync(string jobId, string? cron, string? timeZone, string actor, CancellationToken cancellationToken = default);

    /// <summary>立即執行一次（暫停中也可執行；組態未啟用則拒絕）</summary>
    Task<(bool Succeeded, string Message, string? HangfireJobId)> TriggerAsync(string jobId, string actor, CancellationToken cancellationToken = default);
}

public sealed class ScheduledJobManager(
    IEnumerable<RecurringJobDescriptor> descriptors,
    IRecurringJobManager recurringJobManager,
    IBackgroundJobClient backgroundJobClient,
    IOptions<SchedulerOptions> options,
    DbContext dbContext,
    TimeProvider timeProvider,
    ILogger<ScheduledJobManager> logger) : IScheduledJobManager
{
    /// <summary>手動執行時附加於 Hangfire 作業的參數（排程觸發由 Hangfire 自帶 RecurringJobId）</summary>
    public const string JobIdParameter = "SgsScheduledJobId";
    public const string TriggeredByParameter = "SgsTriggeredBy";

    public async Task<IReadOnlyList<EffectiveJobSchedule>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.Set<ScheduledJobSetting>().AsNoTracking()
            .ToDictionaryAsync(s => s.JobId, StringComparer.OrdinalIgnoreCase, cancellationToken).ConfigureAwait(false);

        var scheduler = options.Value;
        return descriptors.Select(d =>
        {
            var overrides = scheduler.Jobs.GetValueOrDefault(d.Id);
            var setting = settings.GetValueOrDefault(d.Id);
            var configCron = !string.IsNullOrWhiteSpace(overrides?.Cron);
            var baseCron = configCron ? overrides!.Cron!.Trim() : d.DefaultCron;
            var baseZone = SchedulerTimeZones.Resolve(overrides?.TimeZone ?? scheduler.TimeZone);
            var userCron = !string.IsNullOrWhiteSpace(setting?.Cron);

            return new EffectiveJobSchedule(
                d,
                overrides?.Enabled ?? d.EnabledByDefault,
                userCron ? setting!.Cron! : baseCron,
                string.IsNullOrWhiteSpace(setting?.TimeZone) ? baseZone : SchedulerTimeZones.Resolve(setting.TimeZone),
                userCron ? CronSource.User : configCron ? CronSource.Config : CronSource.Default,
                baseCron,
                baseZone,
                setting);
        }).ToList();
    }

    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var schedule in await GetSchedulesAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                Apply(schedule);
            }
            catch (Exception ex)
            {
                // 單一作業設定錯誤（例如 Cron 格式）不影響其他作業與 API 啟動
                logger.LogError(ex, "[Scheduler] 註冊週期作業 {JobId} 失敗。", schedule.Descriptor.Id);
            }
        }
    }

    public async Task<(bool Succeeded, string Message)> PauseAsync(string jobId, string? reason, string actor, CancellationToken cancellationToken = default)
    {
        var schedule = await FindAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (schedule is null) return (false, $"找不到排程作業：{jobId}");
        if (!schedule.ConfigEnabled) return (false, "此作業於組態 Scheduler:Jobs 未啟用，無需暫停。");
        if (schedule.IsPaused) return (false, "此作業已是暫停狀態。");

        await SaveSettingAsync(schedule.Descriptor.Id, s =>
        {
            s.IsPaused = true;
            s.PauseReason = reason?.Trim();
        }, actor, cancellationToken).ConfigureAwait(false);
        recurringJobManager.RemoveIfExists(schedule.Descriptor.Id);

        logger.LogInformation("[Scheduler] {Actor} 暫停週期作業 {JobId}（{JobName}），原因：{Reason}",
            actor, schedule.Descriptor.Id, schedule.Descriptor.DisplayName, reason ?? "（未填）");
        return (true, $"已暫停「{schedule.Descriptor.DisplayName}」的自動排程，仍可手動立即執行。");
    }

    public async Task<(bool Succeeded, string Message)> ResumeAsync(string jobId, string actor, CancellationToken cancellationToken = default)
    {
        var schedule = await FindAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (schedule is null) return (false, $"找不到排程作業：{jobId}");
        if (!schedule.ConfigEnabled) return (false, "此作業於組態 Scheduler:Jobs 未啟用，請先於組態啟用。");
        if (!schedule.IsPaused) return (false, "此作業已在排程中。");

        await SaveSettingAsync(schedule.Descriptor.Id, s =>
        {
            s.IsPaused = false;
            s.PauseReason = null;
        }, actor, cancellationToken).ConfigureAwait(false);
        Apply((await FindAsync(jobId, cancellationToken).ConfigureAwait(false))!);

        logger.LogInformation("[Scheduler] {Actor} 啟動週期作業 {JobId}（{JobName}）：{Cron}",
            actor, schedule.Descriptor.Id, schedule.Descriptor.DisplayName, schedule.Cron);
        return (true, $"已啟動「{schedule.Descriptor.DisplayName}」的自動排程。");
    }

    public async Task<(bool Succeeded, string Message)> UpdateScheduleAsync(
        string jobId, string? cron, string? timeZone, string actor, CancellationToken cancellationToken = default)
    {
        var schedule = await FindAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (schedule is null) return (false, $"找不到排程作業：{jobId}");

        var reset = string.IsNullOrWhiteSpace(cron);
        if (!reset)
        {
            var (valid, error, _) = CronSchedule.Validate(cron, timeZone, timeProvider.GetUtcNow());
            if (!valid) return (false, error!);
        }

        await SaveSettingAsync(schedule.Descriptor.Id, s =>
        {
            s.Cron = reset ? null : cron!.Trim();
            s.TimeZone = reset || string.IsNullOrWhiteSpace(timeZone) ? null : timeZone.Trim();
        }, actor, cancellationToken).ConfigureAwait(false);

        // 暫停中或組態未啟用的作業只儲存設定，恢復 / 啟用時才套用至 Hangfire
        var updated = (await FindAsync(jobId, cancellationToken).ConfigureAwait(false))!;
        Apply(updated);

        logger.LogInformation("[Scheduler] {Actor} 將週期作業 {JobId}（{JobName}）的週期設為 {Cron} ({TimeZone}){Reset}",
            actor, updated.Descriptor.Id, updated.Descriptor.DisplayName, updated.Cron, updated.TimeZone.Id, reset ? "（還原預設）" : null);
        return (true, reset
            ? $"已將「{updated.Descriptor.DisplayName}」的週期還原為預設：{updated.Cron}。"
            : $"已更新「{updated.Descriptor.DisplayName}」的週期：{updated.Cron}。");
    }

    public async Task<(bool Succeeded, string Message, string? HangfireJobId)> TriggerAsync(string jobId, string actor, CancellationToken cancellationToken = default)
    {
        var schedule = await FindAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (schedule is null) return (false, $"找不到排程作業：{jobId}", null);
        if (!schedule.ConfigEnabled) return (false, $"作業 {jobId} 於組態 Scheduler:Jobs 未啟用，無法執行。", null);

        // 以背景作業建立（而非 RecurringJobManager.Trigger），才能附帶觸發人員並在暫停中執行
        var parameters = new Dictionary<string, object>
        {
            [JobIdParameter] = schedule.Descriptor.Id,
            [TriggeredByParameter] = actor
        };
        var hangfireJobId = backgroundJobClient is IBackgroundJobClientV2 client
            ? client.Create(CreateJob(schedule.Descriptor), new EnqueuedState(), parameters)
            : throw new InvalidOperationException("Hangfire 背景作業用戶端不支援附帶參數。");

        logger.LogInformation("[Scheduler] {Actor} 手動執行週期作業 {JobId}（Hangfire 作業 {HangfireJobId}）",
            actor, schedule.Descriptor.Id, hangfireJobId);
        return (true, $"已排入「{schedule.Descriptor.DisplayName}」，請稍後查看執行結果。", hangfireJobId);
    }

    private void Apply(EffectiveJobSchedule schedule)
    {
        var descriptor = schedule.Descriptor;
        if (!schedule.IsScheduled)
        {
            recurringJobManager.RemoveIfExists(descriptor.Id);
            logger.LogInformation("[Scheduler] 週期作業 {JobId}（{JobName}）{State}。",
                descriptor.Id, descriptor.DisplayName, schedule.ConfigEnabled ? "已暫停" : "未啟用");
            return;
        }

        recurringJobManager.AddOrUpdate(descriptor.Id, CreateJob(descriptor), schedule.Cron,
            new RecurringJobOptions { TimeZone = schedule.TimeZone });
        logger.LogInformation("[Scheduler] 已註冊週期作業 {JobId}（{JobName}）：{Cron} ({TimeZone})",
            descriptor.Id, descriptor.DisplayName, schedule.Cron, schedule.TimeZone.Id);
    }

    private static Job CreateJob(RecurringJobDescriptor descriptor)
    {
        var method = descriptor.JobType.GetMethod(nameof(IRecurringJob.ExecuteAsync), [typeof(CancellationToken)])
            ?? throw new InvalidOperationException($"{descriptor.JobType.Name} 缺少 ExecuteAsync(CancellationToken)。");
        return new Job(descriptor.JobType, method, CancellationToken.None);
    }

    private async Task<EffectiveJobSchedule?> FindAsync(string jobId, CancellationToken cancellationToken) =>
        (await GetSchedulesAsync(cancellationToken).ConfigureAwait(false))
        .FirstOrDefault(s => string.Equals(s.Descriptor.Id, jobId, StringComparison.OrdinalIgnoreCase));

    private async Task SaveSettingAsync(string jobId, Action<ScheduledJobSetting> change, string actor, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var setting = await dbContext.Set<ScheduledJobSetting>()
            .FirstOrDefaultAsync(s => s.JobId == jobId, cancellationToken).ConfigureAwait(false);

        if (setting is null)
        {
            setting = new ScheduledJobSetting { JobId = jobId, CreatedBy = actor, CreatedAtUtc = now };
            dbContext.Set<ScheduledJobSetting>().Add(setting);
        }

        change(setting);
        setting.UpdatedBy = actor;
        setting.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
