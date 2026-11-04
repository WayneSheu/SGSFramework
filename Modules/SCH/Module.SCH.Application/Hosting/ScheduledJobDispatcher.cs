using System.Reflection;
using Hangfire;
using Hangfire.Common;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.SCH.Application.Abstractions;
using SGS.Modules.SCH.Application.Jobs;
using SGSFramework.Core.Scheduling;

namespace SGS.Modules.SCH.Application.Hosting;

/// <summary>
/// <see cref="IScheduledJobDispatcher"/> 實作：依作業 ID 找到註冊的 <see cref="RecurringJobDescriptor"/>，
/// 由目前的 DI Scope（Hangfire 每個作業一個 Scope）建立作業類別並執行。
/// </summary>
internal sealed class ScheduledJobDispatcher(
    IServiceProvider serviceProvider,
    ScheduledJobCatalog catalog) : IScheduledJobDispatcher
{
    public Task<string?> ExecuteAsync(string jobId, CancellationToken cancellationToken)
    {
        var descriptor = catalog.Find(jobId)
            ?? throw new InvalidOperationException($"找不到排程作業 {jobId}（模組未註冊此作業，或組態已移除）。");

        var job = descriptor.Factory is { } factory
            ? factory(serviceProvider)
            : (IRecurringJob)serviceProvider.GetRequiredService(descriptor.JobType);
        return job.ExecuteAsync(cancellationToken);
    }
}

/// <summary>
/// 已註冊作業的查詢表（依作業 ID，不分大小寫）：程式註冊的作業，加上各外掛宣告的 <see cref="ModuleDatabaseBackupTarget"/>
/// 所產生的模組資料庫備份作業。於第一次使用時建立（此時所有外掛皆已完成 DI 註冊，與模組載入順序無關）。
/// </summary>
public sealed class ScheduledJobCatalog(
    IEnumerable<RecurringJobDescriptor> descriptors,
    IEnumerable<ModuleDatabaseBackupTarget>? backupTargets = null)
{
    private readonly Dictionary<string, RecurringJobDescriptor> _byId =
        descriptors.Concat(ModuleDatabaseBackupJobs.Describe(backupTargets ?? []))
            .GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<RecurringJobDescriptor> All => _byId.Values;

    public RecurringJobDescriptor? Find(string? jobId) =>
        jobId is not null && _byId.TryGetValue(jobId, out var descriptor) ? descriptor : null;

    /// <summary>派送作業（IScheduledJobDispatcher.ExecuteAsync(jobId, ...)）對應的作業描述；非派送作業回傳 null</summary>
    public RecurringJobDescriptor? Describe(Job? job) =>
        job is not null && job.Type == typeof(IScheduledJobDispatcher) && job.Args.Count > 0
            ? Find(job.Args[0] as string)
            : null;

    /// <summary>建立派送作業：Hangfire 只序列化 Core 的介面與作業 ID，不涉及外掛 ALC 型別</summary>
    public static Job CreateJob(string jobId) =>
        Job.FromExpression<IScheduledJobDispatcher>(d => d.ExecuteAsync(jobId, CancellationToken.None));
}

/// <summary>
/// 派送作業的篩選器來源：所有作業共用同一個介面方法，無法以方法上的 Attribute 區分，
/// 因此改讀取實際作業類別 ExecuteAsync 上的 <see cref="AutomaticRetryAttribute"/> 與
/// <see cref="DisableConcurrentExecutionAttribute"/>，以 Method 範圍套用（覆蓋 Hangfire 全域預設的 10 次重試）。
/// 防止重複執行改以作業 ID 為鎖定資源（原 Attribute 以方法名稱為資源，會讓所有作業互斥）。
/// </summary>
public sealed class ScheduledJobFilterProvider(ScheduledJobCatalog catalog) : IJobFilterProvider
{
    private readonly Dictionary<Type, JobFilter[]> _cache = new();
    private readonly Lock _lock = new();

    public IEnumerable<JobFilter> GetFilters(Job job)
    {
        if (catalog.Describe(job) is not { } descriptor) return [];

        lock (_lock)
        {
            if (!_cache.TryGetValue(descriptor.JobType, out var filters))
            {
                filters = Build(descriptor);
                _cache[descriptor.JobType] = filters;
            }
            return filters;
        }
    }

    private static JobFilter[] Build(RecurringJobDescriptor descriptor)
    {
        var method = descriptor.JobType.GetMethod(nameof(IRecurringJob.ExecuteAsync), [typeof(CancellationToken)]);
        if (method is null) return [];

        var filters = new List<JobFilter>();
        if (method.GetCustomAttribute<AutomaticRetryAttribute>(inherit: true) is { } retry)
        {
            filters.Add(new JobFilter(retry, JobFilterScope.Method, null));
        }

        if (method.GetCustomAttribute<DisableConcurrentExecutionAttribute>(inherit: true) is not null)
        {
            // 原 Attribute 不公開等待秒數，沿用各作業一致的 60 秒
            filters.Add(new JobFilter(new ScheduledJobConcurrencyFilter(descriptor.Id, TimeSpan.FromSeconds(60)), JobFilterScope.Method, null));
        }

        return [.. filters];
    }
}

/// <summary>同一作業 ID 不可同時執行（以 Hangfire 分散式鎖實作，多節點有效）</summary>
public sealed class ScheduledJobConcurrencyFilter(string jobId, TimeSpan timeout) : JobFilterAttribute, IServerFilter
{
    private const string LockKey = "sgs-scheduled-job-lock";

    /// <summary>鎖定資源對應的作業 ID</summary>
    public string JobId => jobId;

    public void OnPerforming(PerformingContext context)
    {
        var handle = context.Connection.AcquireDistributedLock($"sgs:scheduled-job:{jobId}", timeout);
        context.Items[LockKey] = handle;
    }

    public void OnPerformed(PerformedContext context)
    {
        if (context.Items.TryGetValue(LockKey, out var handle) && handle is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
