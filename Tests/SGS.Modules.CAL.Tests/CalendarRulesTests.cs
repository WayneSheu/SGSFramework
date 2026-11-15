using FluentAssertions;
using SGS.Modules.CAL.Application.Contracts;
using SGS.Modules.CAL.Application.Rules;
using SGS.Modules.CAL.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.CAL.Tests;

public sealed class CalendarRulesTests
{
    private static readonly TimeSpan Tw = TimeSpan.FromHours(8);

    private static DateTimeOffset At(int month, int day, int hour = 0, int minute = 0) => new(2026, month, day, hour, minute, 0, Tw);

    // ---------- 假日驗證 ----------

    [Fact]
    public void Holiday_ValidateItem_Rules()
    {
        HolidayRules.ValidateItem(new HolidayDto(new DateOnly(2026, 10, 10), "國慶日", false)).Should().BeNull();
        HolidayRules.ValidateItem(new HolidayDto(new DateOnly(2026, 10, 10), " ", false)).Should().Contain("名稱");
        HolidayRules.ValidateItem(new HolidayDto(new DateOnly(2026, 10, 10), new string('x', 51), false)).Should().Contain("50");
        HolidayRules.ValidateItem(new HolidayDto(new DateOnly(1999, 1, 1), "太早", false)).Should().Contain("範圍");
    }

    [Fact]
    public void Holiday_ValidateBatch_Rules()
    {
        HolidayRules.ValidateBatch(null).Should().Contain("至少");
        HolidayRules.ValidateBatch([]).Should().Contain("至少");
        HolidayRules.ValidateBatch([new HolidayDto(new DateOnly(2026, 1, 1), "元旦", false)]).Should().BeNull();

        var tooMany = Enumerable.Range(0, 401).Select(i => new HolidayDto(new DateOnly(2026, 1, 1).AddDays(i % 300), "x", false)).ToList();
        HolidayRules.ValidateBatch(tooMany).Should().Contain("400");
    }

    [Fact]
    public void Holiday_Dedupe_LastWins_AndSorted()
    {
        var items = HolidayRules.Dedupe([
            new HolidayDto(new DateOnly(2026, 10, 11), "B", false),
            new HolidayDto(new DateOnly(2026, 10, 10), "A1", false),
            new HolidayDto(new DateOnly(2026, 10, 10), "A2", true)
        ]);

        items.Should().HaveCount(2);
        items[0].Name.Should().Be("A2");
        items[0].IsWorkday.Should().BeTrue();
        items[1].Date.Should().Be(new DateOnly(2026, 10, 11));
    }

    // ---------- CSV 匯入解析 ----------

    [Fact]
    public void Csv_Parses_Header_Types_AndDateFormats()
    {
        var result = HolidayCsv.Parse("日期,名稱,類型\n2026-10-10,國慶日,放假\n2026/10/24,補班,補班\n20261225,行憲紀念日\n# 註解\n\n");

        result.Errors.Should().BeEmpty();
        result.Rows.Should().HaveCount(3);
        result.Rows.Select(r => r.Item.Date).Should().Equal(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 24), new DateOnly(2026, 12, 25));
        result.Rows.Single(r => r.Item.Date == new DateOnly(2026, 10, 24)).Item.IsWorkday.Should().BeTrue();
        result.Rows.Single(r => r.Item.Date == new DateOnly(2026, 12, 25)).Item.IsWorkday.Should().BeFalse(); // 類型省略＝放假
        result.Rows[0].Line.Should().Be(2);
    }

    [Fact]
    public void Csv_Accepts_TabSeparated_FromExcel() =>
        HolidayCsv.Parse("2026-02-17\t春節\t放假").Rows.Should().ContainSingle().Which.Item.Name.Should().Be("春節");

    [Fact]
    public void Csv_Reports_Errors_WithLineNumbers()
    {
        var result = HolidayCsv.Parse("2026-10-10,國慶日\n不是日期,錯誤\n2026-10-11\n2026-10-12,名稱,亂填\n2026-10-13, ,放假");

        result.Rows.Should().ContainSingle();
        result.Errors.Should().HaveCount(4);
        result.Errors.Should().Contain(e => e.Contains("第 2 行") && e.Contains("日期"));
        result.Errors.Should().Contain(e => e.Contains("第 3 行") && e.Contains("兩個欄位"));
        result.Errors.Should().Contain(e => e.Contains("第 4 行") && e.Contains("類型"));
        result.Errors.Should().Contain(e => e.Contains("第 5 行") && e.Contains("名稱"));
    }

    [Fact]
    public void Csv_DuplicateDate_LastWins_WithWarning()
    {
        var result = HolidayCsv.Parse("2026-10-10,國慶日,放假\n2026-10-10,補班,補班");

        result.Errors.Should().BeEmpty();
        result.Warnings.Should().ContainSingle().Which.Should().Contain("第 1 行").And.Contain("第 2 行");
        result.Rows.Should().ContainSingle().Which.Item.IsWorkday.Should().BeTrue();
    }

    [Fact]
    public void Csv_Empty_IsError() => HolidayCsv.Parse("  \n ").Errors.Should().ContainSingle();

    [Fact]
    public void Csv_Over400Rows_IsError()
    {
        var text = string.Join('\n', Enumerable.Range(0, 401).Select(i => $"{new DateOnly(2026, 1, 1).AddDays(i):yyyy-MM-dd},日{i}"));

        var result = HolidayCsv.Parse(text);

        result.Rows.Should().HaveCount(400);
        result.Errors.Should().ContainSingle().Which.Should().Contain("400");
    }

    // ---------- 活動驗證 ----------

    private static UpsertCalendarEventRequest Event(DateTimeOffset start, DateTimeOffset end, bool allDay = false, string title = "[研發] 週會 - 王小明") =>
        new(CalendarIds.Company, title, CalendarEventKind.Event, allDay, start, end, null, null, null);

    [Fact]
    public void Event_Validate_Rules()
    {
        CalendarEventRules.Validate(Event(At(11, 16, 9), At(11, 16, 10))).Should().BeNull();
        CalendarEventRules.Validate(Event(At(11, 16, 9), At(11, 16, 9))).Should().Contain("晚於");
        CalendarEventRules.Validate(Event(At(11, 16, 9), At(11, 16, 10), title: " ")).Should().Contain("標題");
        CalendarEventRules.Validate(Event(At(11, 16, 9), At(11, 16, 10), title: new string('x', 201))).Should().Contain("200");
        CalendarEventRules.Validate(Event(At(11, 1), At(12, 10))).Should().Contain("31");
    }

    [Fact]
    public void Event_AllDay_MustAlignToMidnightTaipei()
    {
        CalendarEventRules.Validate(Event(At(11, 16), At(11, 17), allDay: true)).Should().BeNull();
        CalendarEventRules.Validate(Event(At(11, 16, 9), At(11, 17), allDay: true)).Should().Contain("00:00");
        // 以 UTC 表示的同一時刻也要換算成台灣時間判定
        CalendarEventRules.Validate(Event(new DateTimeOffset(2026, 11, 15, 16, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 16, 16, 0, 0, TimeSpan.Zero), allDay: true))
            .Should().BeNull();
    }

    [Theory]
    [InlineData("[研發部] 週會 - 王小明", true)]
    [InlineData("[ERP專案] 需求訪談 - Ann", true)]
    [InlineData("週會", false)]
    [InlineData("[研發部] 週會", false)]
    [InlineData("研發部 週會 - 王小明", false)]
    [InlineData("", false)]
    public void Event_NamingConvention(string title, bool expected) => CalendarEventRules.IsNamingConventional(title).Should().Be(expected);

    [Fact]
    public void Event_ViewRange_Rules()
    {
        CalendarEventRules.ValidateViewRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 12)).Should().BeNull(); // 42 天
        CalendarEventRules.ValidateViewRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 1)).Should().Contain("不得早於");
        CalendarEventRules.ValidateViewRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1)).Should().Contain("62");
    }

    [Fact]
    public void Event_ToRange_UsesTaipeiMidnight()
    {
        var (start, endExclusive) = CalendarEventRules.ToRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        start.Should().Be(At(11, 1));
        endExclusive.Should().Be(At(12, 1));
    }

    [Theory]
    [InlineData("#0F6CBD", true)]
    [InlineData("#abcdef", true)]
    [InlineData("0F6CBD", false)]
    [InlineData("#12345", false)]
    [InlineData(null, false)]
    public void Color_Validation(string? color, bool expected) => CalendarEventRules.IsValidColor(color).Should().Be(expected);
}
