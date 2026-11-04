using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using SGS.Modules.SCH.Application.Abstractions;
using SGS.Modules.SCH.Application.Hosting;
using SGS.Modules.SCH.Application.Jobs;
using SGSFramework.Core.Scheduling;
using Xunit;

namespace SGS.Modules.SCH.Tests;

/// <summary>外掛模組宣告的資料庫備份目標 → 每個目標一組完整 / 差異備份作業</summary>
public sealed class ModuleDatabaseBackupJobTests
{
    private static readonly ModuleDatabaseBackupTarget LogTarget =
        new("log", "日誌資料庫", "PersistentSettings:ConnectionStrings:LogConnection");

    [Fact]
    public void Describe_CreatesFullAndDifferentialJobs_PerTarget()
    {
        var jobs = ModuleDatabaseBackupJobs.Describe([LogTarget]).ToList();

        jobs.Should().HaveCount(2);

        var full = jobs.Single(j => j.Id == "database-backup-full-log");
        full.JobType.Should().Be(typeof(ModuleDatabaseFullBackupJob));
        full.DisplayName.Should().Be("日誌資料庫完整備份");
        full.DefaultCron.Should().Be("30 2 * * *");
        full.EnabledByDefault.Should().BeTrue();
        full.Factory.Should().NotBeNull();

        var differential = jobs.Single(j => j.Id == "database-backup-differential-log");
        differential.JobType.Should().Be(typeof(ModuleDatabaseDifferentialBackupJob));
        differential.EnabledByDefault.Should().BeFalse("差異備份與主庫相同，預設停用");
    }

    [Theory]
    [InlineData("Log")]
    [InlineData("log:db")]
    [InlineData("")]
    [InlineData("-log")]
    public void Describe_SkipsInvalidKeys(string key)
    {
        ModuleDatabaseBackupJobs.Describe([LogTarget with { Key = key }]).Should().BeEmpty();
    }

    [Fact]
    public void Describe_IgnoresDuplicateKeys()
    {
        ModuleDatabaseBackupJobs.Describe([LogTarget, LogTarget with { DisplayName = "重複" }]).Should().HaveCount(2);
    }

    [Fact]
    public void Catalog_IncludesModuleBackupJobs_AndAppliesTheirRetryPolicy()
    {
        var catalog = new ScheduledJobCatalog(
            [new RecurringJobDescriptor("database-backup-full", typeof(object), "資料庫完整備份", "0 2 * * *", true)],
            [LogTarget]);

        catalog.All.Select(d => d.Id).Should().BeEquivalentTo(
            ["database-backup-full", "database-backup-full-log", "database-backup-differential-log"]);

        var filters = new ScheduledJobFilterProvider(catalog)
            .GetFilters(ScheduledJobCatalog.CreateJob("database-backup-full-log")).ToList();

        filters.Select(f => f.Instance).OfType<AutomaticRetryAttribute>().Single().Attempts.Should().Be(2);
        filters.Select(f => f.Instance).OfType<ScheduledJobConcurrencyFilter>().Single().JobId
            .Should().Be("database-backup-full-log", "各資料庫的備份作業各自鎖定，可與主庫備份並行");
    }

    [Fact]
    public void Catalog_WithoutTargets_HasOnlyRegisteredJobs()
    {
        var catalog = new ScheduledJobCatalog([new RecurringJobDescriptor("job", typeof(object), "作業", "0 2 * * *", true)]);

        catalog.All.Should().ContainSingle();
        catalog.Describe(Job.FromExpression(() => Console.WriteLine())).Should().BeNull();
    }
}
