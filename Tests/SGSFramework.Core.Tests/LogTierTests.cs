using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SGSFramework.Core.Logging;
using Xunit;

namespace SGSFramework.Core.Tests;

/// <summary>日誌冷熱分層：冷庫目錄解析、連線字串衍生、多來源合併分頁。</summary>
public sealed class LogTierTests
{
    private const string Hot = "Server=db01;Database=PhysLIMSLogDB;User Id=app_sgs_user;Password=x;TrustServerCertificate=True;";

    private static LogTierCatalog Catalog(params (string Key, string Database, string? Display)[] colds)
    {
        var data = new Dictionary<string, string?> { [LogTierCatalog.RuntimeConnectionKey] = Hot };
        for (var i = 0; i < colds.Length; i++)
        {
            data[$"LogArchive:Databases:{i}:Key"] = colds[i].Key;
            data[$"LogArchive:Databases:{i}:Database"] = colds[i].Database;
            if (colds[i].Display is not null) data[$"LogArchive:Databases:{i}:DisplayName"] = colds[i].Display;
        }

        return new LogTierCatalog(new ConfigurationBuilder().AddInMemoryCollection(data).Build());
    }

    [Fact]
    public void Hot_tier_is_always_first_and_uses_log_connection_database()
    {
        var catalog = Catalog(("2026", "PhysLIMSLogDB_2026", null));

        catalog.Hot.Key.Should().Be("hot");
        catalog.Hot.Database.Should().Be("PhysLIMSLogDB");
        catalog.All.Select(t => t.Key).Should().Equal("hot", "cold-2026");
        catalog.Colds.Single().IsHot.Should().BeFalse();
    }

    [Fact]
    public void Cold_connection_swaps_only_the_database_name()
    {
        var catalog = Catalog(("2026", "PhysLIMSLogDB_2026", "冷資料 2026"));

        var cold = new SqlConnectionStringBuilder(catalog.GetConnectionString("cold-2026"));
        cold.InitialCatalog.Should().Be("PhysLIMSLogDB_2026");
        cold.DataSource.Should().Be("db01");
        cold.UserID.Should().Be("app_sgs_user");
        catalog.Colds.Single().DisplayName.Should().Be("冷資料 2026");

        catalog.GetConnectionString(null).Should().Be(Hot);
        catalog.GetConnectionString("hot").Should().Be(Hot);
    }

    [Fact]
    public void Invalid_or_duplicate_entries_are_skipped_and_reported()
    {
        var catalog = Catalog(
            ("Bad Key", "PhysLIMSLogDB_x", null),          // Key 含空白 / 大寫
            ("2025", "PhysLIMSLogDB;DROP", null),          // 資料庫名稱不合法
            ("2024", "PhysLIMSLogDB", null),               // 與熱庫相同
            ("2023", "PhysLIMSLogDB_2023", null),          // 合法
            ("2023", "PhysLIMSLogDB_2023b", null));        // Key 重複

        catalog.Colds.Select(c => c.Key).Should().Equal("cold-2023");
        catalog.Problems.Should().HaveCount(4);
    }

    [Fact]
    public void Unknown_source_is_rejected_and_blank_means_hot()
    {
        var catalog = Catalog();

        catalog.Find("  ")!.IsHot.Should().BeTrue();
        catalog.Find("cold-nope").Should().BeNull();
        var act = () => catalog.GetConnectionString("cold-nope");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Missing_log_connection_throws()
    {
        var act = () => new LogTierCatalog(new ConfigurationBuilder().Build());
        act.Should().Throw<InvalidOperationException>();
    }

    private sealed record Row(long Id, DateTimeOffset Time);

    [Fact]
    public void Merge_orders_by_time_across_sources_and_slices_the_page()
    {
        var t0 = DateTimeOffset.Parse("2026-11-10T10:00:00+08:00");
        var rows = new List<(Row, string?)>
        {
            (new Row(1, t0.AddMinutes(1)), "hot"),
            (new Row(2, t0.AddMinutes(5)), "hot"),
            (new Row(1, t0.AddMinutes(3)), "cold-2025"),
            (new Row(2, t0.AddMinutes(4)), "cold-2025"),
            (new Row(3, t0.AddMinutes(2)), "cold-2025")
        };

        var firstPage = LogPageMerger.MergePage(rows, r => r.Time, r => r.Id, pageIndex: 1, pageSize: 2, ascending: false);
        firstPage.Select(p => p.Item.Time).Should().Equal(t0.AddMinutes(5), t0.AddMinutes(4));
        firstPage.Select(p => p.Source).Should().Equal("hot", "cold-2025");

        var secondPage = LogPageMerger.MergePage(rows, r => r.Time, r => r.Id, pageIndex: 2, pageSize: 2, ascending: false);
        secondPage.Select(p => p.Item.Time).Should().Equal(t0.AddMinutes(3), t0.AddMinutes(2));

        var asc = LogPageMerger.MergePage(rows, r => r.Time, r => r.Id, pageIndex: 1, pageSize: 2, ascending: true);
        asc.Select(p => p.Item.Time).Should().Equal(t0.AddMinutes(1), t0.AddMinutes(2));
    }

    [Fact]
    public void Merge_uses_id_as_tiebreaker_for_identical_timestamps()
    {
        var t = DateTimeOffset.Parse("2026-11-10T10:00:00+08:00");
        var rows = new List<(Row, string?)> { (new Row(1, t), "a"), (new Row(9, t), "b"), (new Row(5, t), "c") };

        LogPageMerger.MergePage(rows, r => r.Time, r => r.Id, 1, 10, ascending: false)
            .Select(p => p.Item.Id).Should().Equal(9, 5, 1);
    }

    [Fact]
    public void Take_per_source_is_page_times_size_and_bounded()
    {
        LogPageMerger.TakePerSource(1, 100).Should().Be(100);
        LogPageMerger.TakePerSource(3, 100).Should().Be(300);
        LogPageMerger.TakePerSource(50, 100).Should().Be(LogPageMerger.MaxMergeDepth);

        var tooDeep = () => LogPageMerger.TakePerSource(51, 100);
        tooDeep.Should().Throw<ArgumentException>();
    }
}
