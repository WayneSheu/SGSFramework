using System.Text;
using FluentAssertions;
using SGS.Modules.CAL.Application.Contracts;
using SGS.Modules.CAL.Application.Rules;
using SGS.Modules.CAL.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.CAL.Tests;

public sealed class GovCalendarRulesTests
{
    /// <summary>產生 2026 全年資料：週一至週五上班、週六日放假，再套用例外。</summary>
    private static IEnumerable<(DateOnly Date, bool IsOff, string Description)> Year2026(IReadOnlyDictionary<string, (bool IsOff, string Desc)>? overrides = null)
    {
        var date = new DateOnly(2026, 1, 1);
        while (date.Year == 2026)
        {
            var weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var key = date.ToString("yyyyMMdd");
            if (overrides is not null && overrides.TryGetValue(key, out var o)) yield return (date, o.IsOff, o.Desc);
            else yield return (date, weekend, string.Empty);

            date = date.AddDays(1);
        }
    }

    private static string Json(IEnumerable<(DateOnly Date, bool IsOff, string Description)> days) =>
        "[" + string.Join(",", days.Select(d => $"{{\"date\":\"{d.Date:yyyyMMdd}\",\"week\":\"一\",\"isHoliday\":{(d.IsOff ? "true" : "false")},\"description\":\"{d.Description}\"}}")) + "]";

    private static string Csv(IEnumerable<(DateOnly Date, bool IsOff, string Description)> days) =>
        "西元日期,星期,是否放假,備註\n" + string.Join("\n", days.Select(d => $"{d.Date:yyyyMMdd},一,{(d.IsOff ? 2 : 0)},{d.Description}"));

    private static readonly Dictionary<string, (bool, string)> Exceptions = new()
    {
        ["20260101"] = (true, "開國紀念日"),    // 週四放假
        ["20260207"] = (false, "補行上班"),     // 週六補班
        ["20261010"] = (true, "國慶日"),        // 週六國定假日（恰逢週末）
        ["20260216"] = (true, ""),             // 週一放假，無備註
    };

    // ---------- 解析 ----------

    [Fact]
    public void Parse_Json_FullYear()
    {
        var result = GovCalendarRules.Parse(Json(Year2026(Exceptions)), 2026);

        result.Errors.Should().BeEmpty();
        result.Days.Should().HaveCount(365);
        result.Days.Single(d => d.Date == new DateOnly(2026, 1, 1)).Description.Should().Be("開國紀念日");
    }

    [Fact]
    public void Parse_Csv_OfficialFormat()
    {
        var result = GovCalendarRules.Parse(Csv(Year2026(Exceptions)), 2026);

        result.Errors.Should().BeEmpty();
        result.Days.Should().HaveCount(365);
        result.Days.Single(d => d.Date == new DateOnly(2026, 2, 7)).IsOff.Should().BeFalse();
        result.Days.Single(d => d.Date == new DateOnly(2026, 10, 10)).IsOff.Should().BeTrue();
    }

    [Fact]
    public void Parse_Csv_WithUtf8BomAndQuotedRemark()
    {
        var text = "\uFEFF" + Csv(Year2026()).Replace("20260101,一,0,", "20260101,一,2,\"元旦,開國紀念日\"");

        var result = GovCalendarRules.Parse(text, 2026);

        result.Errors.Should().BeEmpty();
        result.Days.Single(d => d.Date == new DateOnly(2026, 1, 1)).Description.Should().Be("元旦,開國紀念日");
    }

    [Fact]
    public void Parse_RejectsIncompleteYear()
    {
        var partial = Year2026().Take(100).ToList();

        var result = GovCalendarRules.Parse(Json(partial), 2026);

        result.Errors.Should().ContainSingle().Which.Should().Contain("100 天").And.Contain("未同步");
        result.Days.Should().BeEmpty();
    }

    [Fact]
    public void Parse_RejectsWrongYear() =>
        GovCalendarRules.Parse(Json(Year2026()), 2027).Errors.Should().ContainSingle().Which.Should().Contain("2027");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_RejectsEmpty(string text) => GovCalendarRules.Parse(text, 2026).Errors.Should().NotBeEmpty();

    [Fact]
    public void Parse_Csv_ReportsBadFlagAndBadDate()
    {
        var text = new StringBuilder("西元日期,星期,是否放假,備註\n20260101,四,9,\n壞日期,五,0,\n").ToString();

        var result = GovCalendarRules.Parse(text, 2026);

        result.Errors.Should().Contain(e => e.Contains("第 2 行") && e.Contains("是否放假"));
        result.Errors.Should().Contain(e => e.Contains("第 3 行") && e.Contains("日期"));
    }

    [Fact]
    public void Parse_Json_ReportsMissingFields()
    {
        var result = GovCalendarRules.Parse("[{\"date\":\"20260101\"},{\"date\":\"x\",\"isHoliday\":true}]", 2026);

        result.Errors.Should().HaveCount(2);
    }

    // ---------- 轉成「與預設不同」的日子 ----------

    [Fact]
    public void Derive_KeepsOnlyDifferencesFromDefault()
    {
        var derived = GovCalendarRules.DeriveOverrides(GovCalendarRules.Parse(Json(Year2026(Exceptions)), 2026).Days);

        derived.Should().HaveCount(4);
        derived.Single(d => d.Date == new DateOnly(2026, 1, 1)).Should().Be(new HolidayDto(new DateOnly(2026, 1, 1), "開國紀念日", false));
        derived.Single(d => d.Date == new DateOnly(2026, 2, 7)).IsWorkday.Should().BeTrue();
        derived.Single(d => d.Date == new DateOnly(2026, 2, 7)).Name.Should().Be("補行上班");
        // 國定假日恰逢週六：仍保留（加班倍率判定需要它是國定假日）
        derived.Single(d => d.Date == new DateOnly(2026, 10, 10)).Name.Should().Be("國慶日");
        // 週一放假但無備註 → 預設名稱
        derived.Single(d => d.Date == new DateOnly(2026, 2, 16)).Name.Should().Be("放假");
    }

    [Fact]
    public void Derive_PlainWeekendsAndWorkdays_AreNotStored() =>
        GovCalendarRules.DeriveOverrides(GovCalendarRules.Parse(Json(Year2026()), 2026).Days).Should().BeEmpty();

    [Fact]
    public void Derive_TruncatesLongNames()
    {
        var longName = new string('假', 80);
        var derived = GovCalendarRules.DeriveOverrides([new GovDay(new DateOnly(2026, 1, 1), true, longName)]);

        derived.Single().Name.Should().HaveLength(50);
    }

    // ---------- 同步計畫 ----------

    private static readonly IReadOnlyList<HolidayDto> Derived =
    [
        new(new DateOnly(2026, 1, 1), "開國紀念日", false),
        new(new DateOnly(2026, 2, 7), "補行上班", true),
        new(new DateOnly(2026, 10, 10), "國慶日", false)
    ];

    [Fact]
    public void Plan_AllNew_WhenNothingExists()
    {
        var plan = GovCalendarRules.Plan(2026, Derived, new Dictionary<DateOnly, GovCalendarRules.ExistingHoliday>(), overwriteManual: false);

        plan.Added.Should().Be(3);
        plan.Updated.Should().Be(0);
        plan.Items.Should().OnlyContain(i => i.Action == GovSyncAction.Add);
    }

    [Fact]
    public void Plan_ManualConflict_IsKept_UnlessOverwrite()
    {
        var existing = new Dictionary<DateOnly, GovCalendarRules.ExistingHoliday>
        {
            [new DateOnly(2026, 1, 1)] = new("元旦（公司自訂）", false, HolidaySource.Manual),
            [new DateOnly(2026, 2, 7)] = new("補行上班", true, HolidaySource.Manual)
        };

        var keep = GovCalendarRules.Plan(2026, Derived, existing, overwriteManual: false);
        keep.ManualConflicts.Should().Be(1);
        keep.Updated.Should().Be(0);
        keep.Items.Single(i => i.Date == new DateOnly(2026, 1, 1)).Action.Should().Be(GovSyncAction.SkipManual);
        keep.Items.Single(i => i.Date == new DateOnly(2026, 2, 7)).Action.Should().Be(GovSyncAction.Unchanged);

        var overwrite = GovCalendarRules.Plan(2026, Derived, existing, overwriteManual: true);
        overwrite.Updated.Should().Be(1);
        overwrite.Items.Single(i => i.Date == new DateOnly(2026, 1, 1)).Action.Should().Be(GovSyncAction.Update);
    }

    [Fact]
    public void Plan_GovSyncRows_AreUpdatedAndRemoved_ButManualNeverRemoved()
    {
        var existing = new Dictionary<DateOnly, GovCalendarRules.ExistingHoliday>
        {
            [new DateOnly(2026, 1, 1)] = new("舊名稱", false, HolidaySource.GovSync),                 // 更新
            [new DateOnly(2026, 3, 3)] = new("政府已撤銷的假日", false, HolidaySource.GovSync),       // 移除
            [new DateOnly(2026, 4, 4)] = new("公司自訂休假", false, HolidaySource.Manual),             // 人工，不得移除
            [new DateOnly(2025, 12, 31)] = new("去年的同步資料", false, HolidaySource.GovSync)         // 不同年度，不動
        };

        var plan = GovCalendarRules.Plan(2026, Derived, existing, overwriteManual: false);

        plan.Updated.Should().Be(1);
        plan.Removed.Should().Be(1);
        plan.Items.Should().Contain(i => i.Date == new DateOnly(2026, 3, 3) && i.Action == GovSyncAction.Remove);
        plan.Items.Should().NotContain(i => i.Date == new DateOnly(2026, 4, 4));
        plan.Items.Should().NotContain(i => i.Date == new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void Plan_Unchanged_WhenSame()
    {
        var existing = Derived.ToDictionary(d => d.Date, d => new GovCalendarRules.ExistingHoliday(d.Name, d.IsWorkday, HolidaySource.GovSync));

        var plan = GovCalendarRules.Plan(2026, Derived, existing, overwriteManual: false);

        plan.Unchanged.Should().Be(3);
        plan.Added.Should().Be(0);
        plan.Updated.Should().Be(0);
        plan.Removed.Should().Be(0);
    }

    // ---------- 網址與年度 ----------

    [Fact]
    public void ResolveUrl_ReplacesPlaceholders_AndRequiresHttps()
    {
        GovCalendarRules.ResolveUrl("https://x.example/{year}.json", 2026).Should().Be("https://x.example/2026.json");
        GovCalendarRules.ResolveUrl("https://x.example/{rocYear}/cal.csv", 2026).Should().Be("https://x.example/115/cal.csv");
        GovCalendarRules.ResolveUrl("http://x.example/{year}.json", 2026).Should().BeNull();
        GovCalendarRules.ResolveUrl("file:///c:/x.json", 2026).Should().BeNull();
        GovCalendarRules.ResolveUrl("", 2026).Should().BeNull();
        GovCalendarRules.ResolveUrl(null, 2026).Should().BeNull();
    }

    [Fact]
    public void ValidateYear_OnlyLastThisAndNext()
    {
        GovCalendarRules.ValidateYear(2026, 2026).Should().BeNull();
        GovCalendarRules.ValidateYear(2025, 2026).Should().BeNull();
        GovCalendarRules.ValidateYear(2027, 2026).Should().BeNull();
        GovCalendarRules.ValidateYear(2024, 2026).Should().Contain("2025");
        GovCalendarRules.ValidateYear(2028, 2026).Should().NotBeNull();
        GovSyncSchedule.Years(2026).Should().Equal(2026, 2027);
    }
}
