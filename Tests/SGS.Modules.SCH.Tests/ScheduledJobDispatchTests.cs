using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using SGS.Modules.SCH.Application.Abstractions;
using SGS.Modules.SCH.Application.Hosting;
using SGSFramework.Core.Scheduling;
using Xunit;

namespace SGS.Modules.SCH.Tests;

/// <summary>
/// 派送作業：Hangfire 只序列化 Core 的 IScheduledJobDispatcher，重試 / 防重複策略依作業 ID 從實際作業類別套用。
/// </summary>
public sealed class ScheduledJobDispatchTests
{
    private sealed class RetryingJob : IRecurringJob
    {
        [AutomaticRetry(Attempts = 2, DelaysInSeconds = [600, 1800])]
        [DisableConcurrentExecution(timeoutInSeconds: 60)]
        public Task<string?> ExecuteAsync(CancellationToken cancellationToken) => Task.FromResult<string?>("ok");
    }

    private sealed class PlainJob : IRecurringJob
    {
        public Task<string?> ExecuteAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }

    private static ScheduledJobCatalog Catalog() => new(
    [
        new RecurringJobDescriptor("job-a", typeof(RetryingJob), "作業 A", "0 2 * * *", true),
        new RecurringJobDescriptor("job-b", typeof(PlainJob), "作業 B", "0 3 * * *", true)
    ]);

    [Fact]
    public void CreateJob_SerializesOnlyCoreDispatcherAndJobId()
    {
        var job = ScheduledJobCatalog.CreateJob("job-a");

        job.Type.Should().Be(typeof(IScheduledJobDispatcher));
        job.Method.Name.Should().Be(nameof(IScheduledJobDispatcher.ExecuteAsync));
        job.Args[0].Should().Be("job-a");
    }

    [Fact]
    public void Describe_ResolvesDescriptorByJobId_CaseInsensitive()
    {
        var catalog = Catalog();

        catalog.Describe(ScheduledJobCatalog.CreateJob("JOB-A"))!.JobType.Should().Be(typeof(RetryingJob));
        catalog.Describe(ScheduledJobCatalog.CreateJob("unknown")).Should().BeNull();
        catalog.Describe(Job.FromExpression(() => Console.WriteLine())).Should().BeNull();
    }

    [Fact]
    public void FilterProvider_AppliesJobRetryAndPerJobLock_AtMethodScope()
    {
        var provider = new ScheduledJobFilterProvider(Catalog());

        var filters = provider.GetFilters(ScheduledJobCatalog.CreateJob("job-a")).ToList();

        filters.Should().OnlyContain(f => f.Scope == JobFilterScope.Method);
        filters.Select(f => f.Instance).OfType<AutomaticRetryAttribute>().Single().Attempts.Should().Be(2);
        filters.Select(f => f.Instance).OfType<ScheduledJobConcurrencyFilter>().Single().JobId.Should().Be("job-a");
        filters.Select(f => f.Instance).OfType<DisableConcurrentExecutionAttribute>().Should().BeEmpty(
            "原 Attribute 以方法名稱為鎖定資源，所有派送作業會互相排斥");
    }

    [Fact]
    public void FilterProvider_ReturnsNothing_ForJobsWithoutAttributesOrNonDispatcherJobs()
    {
        var provider = new ScheduledJobFilterProvider(Catalog());

        provider.GetFilters(ScheduledJobCatalog.CreateJob("job-b")).Should().BeEmpty();
        provider.GetFilters(Job.FromExpression(() => Console.WriteLine())).Should().BeEmpty();
    }
}
