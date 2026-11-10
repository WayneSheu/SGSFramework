using FluentAssertions;
using SGSFramework.Core.Logging;
using Xunit;

namespace SGSFramework.Core.Tests;

/// <summary>日誌庫年度 / 半年度換庫的期間、命名與路徑規則（純函式）。</summary>
public sealed class LogRotationNamingTests
{
    private static LogRotationPeriod P(string text)
    {
        LogRotationPeriod.TryParse(text, out var period).Should().BeTrue($"'{text}' 應可解析");
        return period;
    }

    [Theory]
    [InlineData("2026", 2026, 0, "2026", "2026", "2026 年度")]
    [InlineData("2026H1", 2026, 1, "2026H1", "2026h1", "2026 上半年（1–6 月）")]
    [InlineData("2026h2", 2026, 2, "2026H2", "2026h2", "2026 下半年（7–12 月）")]
    public void Period_parsing_and_labels(string text, int year, int half, string suffix, string key, string display)
    {
        var period = P(text);

        period.Year.Should().Be(year);
        period.Half.Should().Be(half);
        period.Suffix.Should().Be(suffix);
        period.Key.Should().Be(key);
        period.DisplayName.Should().Be(display);
    }

    [Theory]
    [InlineData("")]
    [InlineData("26")]
    [InlineData("2026H3")]
    [InlineData("2026H")]
    [InlineData("2026x")]
    [InlineData("H12026")]
    [InlineData("1999")]
    [InlineData("3000H1")]
    [InlineData("2026/1-6")]
    public void Invalid_periods_are_rejected(string text)
    {
        LogRotationPeriod.TryParse(text, out _).Should().BeFalse();
    }

    [Fact]
    public void Overlap_rules()
    {
        P("2026").Overlaps(P("2026H1")).Should().BeTrue();     // 整年度涵蓋上半年
        P("2026H2").Overlaps(P("2026")).Should().BeTrue();
        P("2026H1").Overlaps(P("2026H2")).Should().BeFalse();  // 上 / 下半年互不重疊
        P("2026H1").Overlaps(P("2026H1")).Should().BeTrue();
        P("2026").Overlaps(P("2027")).Should().BeFalse();
        P("2026H2").Overlaps(P("2027H1")).Should().BeFalse();
    }

    [Fact]
    public void Next_period_follows_granularity()
    {
        P("2026").Next().Should().Be(P("2027"));
        P("2026H1").Next().Should().Be(P("2026H2"));
        P("2026H2").Next().Should().Be(P("2027H1"));
    }

    [Fact]
    public void Periods_sort_chronologically()
    {
        var sorted = new[] { P("2027"), P("2026H2"), P("2026H1"), P("2025") }.OrderBy(p => p).Select(p => p.Suffix);
        sorted.Should().Equal("2025", "2026H1", "2026H2", "2027");
    }

    [Fact]
    public void Cold_name_is_hot_name_plus_period()
    {
        LogRotationNaming.ColdDatabaseName("PhysLIMSLogDB", P("2026")).Should().Be("PhysLIMSLogDB_2026");
        LogRotationNaming.ColdDatabaseName("PhysLIMSLogDB", P("2026H1")).Should().Be("PhysLIMSLogDB_2026H1");
    }

    [Theory]
    [InlineData("Phys;DROP")]
    [InlineData("Phys LIMS")]
    [InlineData("a]b")]
    [InlineData("")]
    public void Unsafe_database_name_is_rejected(string hot)
    {
        var act = () => LogRotationNaming.ColdDatabaseName(hot, P("2026"));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Default_period_by_granularity()
    {
        static DateTimeOffset At(int y, int m, int d) => new(y, m, d, 1, 0, 0, TimeSpan.FromHours(8));

        // 年度：1~6 月封存上一年度，7~12 月封存當年度
        LogRotationNaming.DefaultPeriod(At(2027, 1, 1), LogRotationGranularity.Year).Should().Be(P("2026"));
        LogRotationNaming.DefaultPeriod(At(2027, 6, 30), LogRotationGranularity.Year).Should().Be(P("2026"));
        LogRotationNaming.DefaultPeriod(At(2026, 11, 10), LogRotationGranularity.Year).Should().Be(P("2026"));

        // 半年度：1~3 月 → 上一年下半年；4~9 月 → 當年上半年；10~12 月 → 當年下半年
        LogRotationNaming.DefaultPeriod(At(2027, 1, 1), LogRotationGranularity.HalfYear).Should().Be(P("2026H2"));
        LogRotationNaming.DefaultPeriod(At(2027, 7, 1), LogRotationGranularity.HalfYear).Should().Be(P("2027H1"));
        LogRotationNaming.DefaultPeriod(At(2026, 11, 10), LogRotationGranularity.HalfYear).Should().Be(P("2026H2"));
    }

    [Fact]
    public void Suggestion_skips_periods_that_already_have_a_cold_database()
    {
        var now = new DateTimeOffset(2026, 11, 10, 1, 0, 0, TimeSpan.FromHours(8));

        // 2026 與 2027 都已有冷庫 → 建議 2028
        var yearly = LogRotationNaming.Suggest(now, LogRotationGranularity.Year, [P("2026"), P("2027")]);
        yearly.Initial.Should().Be(P("2026"));
        yearly.Period.Should().Be(P("2028"));
        yearly.WasSkipped.Should().BeTrue();

        // 無衝突：不跳過
        var free = LogRotationNaming.Suggest(now, LogRotationGranularity.Year, [P("2025")]);
        free.Period.Should().Be(P("2026"));
        free.WasSkipped.Should().BeFalse();
    }

    [Fact]
    public void Suggestion_for_half_year_skips_overlapping_full_year_cold()
    {
        var now = new DateTimeOffset(2026, 11, 10, 1, 0, 0, TimeSpan.FromHours(8));

        // 預設 2026H2；整年度 2026 冷庫已涵蓋 → 往後推到 2027H1
        var suggestion = LogRotationNaming.Suggest(now, LogRotationGranularity.HalfYear, [P("2026")]);
        suggestion.Initial.Should().Be(P("2026H2"));
        suggestion.Period.Should().Be(P("2027H1"));

        // 2026H1 已存在不影響 2026H2
        LogRotationNaming.Suggest(now, LogRotationGranularity.HalfYear, [P("2026H1")]).Period.Should().Be(P("2026H2"));
    }

    [Theory]
    [InlineData("PhysLIMSLogDB_2026", true, "2026")]
    [InlineData("physlimslogdb_2025", true, "2025")]            // 不分大小寫
    [InlineData("PhysLIMSLogDB_2026H1", true, "2026H1")]
    [InlineData("PhysLIMSLogDB_2026h2", true, "2026H2")]
    [InlineData("PhysLIMSLogDB_26", false, "")]
    [InlineData("PhysLIMSLogDB_2026x", false, "")]
    [InlineData("PhysLIMSLogDB_2026_old", false, "")]
    [InlineData("PhysLIMSLogDB_2026H3", false, "")]
    [InlineData("OtherDB_2026", false, "")]
    [InlineData("PhysLIMSLogDB", false, "")]
    [InlineData("PhysLIMSLogDB_1999", false, "")]
    public void Cold_period_parsing(string database, bool ok, string suffix)
    {
        LogRotationNaming.TryParseColdPeriod("PhysLIMSLogDB", database, out var parsed).Should().Be(ok);
        (ok ? parsed.Suffix : string.Empty).Should().Be(suffix);
    }

    [Fact]
    public void Backup_path_normalizes_directory_and_names_kind()
    {
        var now = new DateTimeOffset(2027, 1, 1, 1, 2, 3, TimeSpan.Zero);

        LogRotationNaming.BackupFilePath(@"D:\SQLBackup", "PhysLIMSLogDB", P("2026"), differential: false, now)
            .Should().Be(@"D:\SQLBackup\PhysLIMSLogDB_rotation_2026_full_20270101010203.bak");
        LogRotationNaming.BackupFilePath(@"D:\SQLBackup\", "PhysLIMSLogDB", P("2026H2"), differential: true, now)
            .Should().Be(@"D:\SQLBackup\PhysLIMSLogDB_rotation_2026H2_diff_20270101010203.bak");
    }

    [Fact]
    public void Cold_file_path_replaces_only_file_name()
    {
        LogRotationNaming.DeriveColdFilePath(@"C:\Data\PhysLIMSLogDB.mdf", "PhysLIMSLogDB", "PhysLIMSLogDB_2026H1")
            .Should().Be(@"C:\Data\PhysLIMSLogDB_2026H1.mdf");
        LogRotationNaming.DeriveColdFilePath(@"C:\Data\PhysLIMSLogDB_log.ldf", "PhysLIMSLogDB", "PhysLIMSLogDB_2026")
            .Should().Be(@"C:\Data\PhysLIMSLogDB_2026_log.ldf");

        // 目錄名稱含熱庫名稱、檔名不含時不可推導（避免覆蓋熱庫檔案）
        LogRotationNaming.DeriveColdFilePath(@"D:\PhysLIMSLogDB\data01.mdf", "PhysLIMSLogDB", "PhysLIMSLogDB_2026")
            .Should().BeNull();
        LogRotationNaming.DeriveColdFilePath(@"C:\Data\custom_data.mdf", "PhysLIMSLogDB", "PhysLIMSLogDB_2026")
            .Should().BeNull();
    }

    [Fact]
    public void Like_escape_covers_wildcards_and_pattern_matches_period_suffixes()
    {
        LogRotationNaming.EscapeLike("Phys_LIMS%[x]").Should().Be(@"Phys\_LIMS\%\[x]");
        LogRotationNaming.ColdLikePattern("PhysLIMSLogDB").Should().Be(@"PhysLIMSLogDB\_[0-9][0-9][0-9][0-9]%");
    }
}
