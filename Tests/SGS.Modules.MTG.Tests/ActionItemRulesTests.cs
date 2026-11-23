using FluentAssertions;
using SGS.Modules.MTG.Application.Rules;
using Xunit;

namespace SGS.Modules.MTG.Tests;

public class ActionItemRulesTests
{
    // 2026-11-18 是星期三
    private static readonly DateOnly Wed = new(2026, 11, 18);

    [Theory]
    [InlineData("後端與資料庫設計", "後端與資料庫設計")]
    [InlineData("  **加粗**  的  文字 ", "加粗 的 文字")]
    [InlineData("「引號」", "引號")]
    [InlineData("第一行<br>第二行", "第一行 第二行")]
    [InlineData("未提及", null)]
    [InlineData("無", null)]
    [InlineData("N/A", null)]
    [InlineData("—", null)]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void CleanField_strips_markup_and_treats_placeholders_as_missing(string? input, string? expected) =>
        ActionItemRules.CleanField(input, 500).Should().Be(expected);

    [Fact]
    public void CleanField_truncates_to_the_maximum_length() =>
        ActionItemRules.CleanField(new string('字', 600), 500).Should().HaveLength(500);

    // ---------- 表格 ----------

    private const string Minutes = """
        ## 1. 核心摘要
        本次會議確認範圍。

        ## 4. 待辦行動清單
        | 待辦事項 | 負責人 | 期限 | 備註 |
        |---|---|---|---|
        | 後端與資料庫設計：第一周完成資料模型 | 李工程師 | 11/25 | 優先 |
        | 測試計劃：第三周提出測試案例清單 | **陳經理** | 第三周 | 未提及 |
        | 整理會議紀錄 | 未提及 | 未提及 | |
        | 未提及 | 王經理 | 週五 | |
        """;

    [Fact]
    public void ParseTable_reads_the_action_section_and_skips_rows_without_a_task()
    {
        var items = ActionItemRules.ParseTable(Minutes, Wed);

        items.Should().HaveCount(3);
        items[0].Should().Be(new ExtractedActionItem("後端與資料庫設計：第一周完成資料模型", "李工程師", "11/25", new DateOnly(2026, 11, 25), "優先"));
        items[1].Owner.Should().Be("陳經理");
        items[1].DueText.Should().Be("第三周");
        items[1].DueDate.Should().BeNull("「第三周」無法對應到確切日期，不猜");
        items[1].Note.Should().BeNull();
        items[2].Should().Be(new ExtractedActionItem("整理會議紀錄", null, null, null, null));
    }

    [Fact]
    public void ParseTable_uses_header_names_not_column_positions()
    {
        var md = """
            ## 4. 下週核心里程碑與待辦清單
            | 備註 | 期限 | 待辦事項 | 負責人 |
            |---|---|---|---|
            | 要快 | 2026-12-01 | 完成驗收 | 小明 |
            """;

        var item = ActionItemRules.ParseTable(md, Wed).Single();

        item.Should().Be(new ExtractedActionItem("完成驗收", "小明", "2026-12-01", new DateOnly(2026, 12, 1), "要快"));
    }

    [Fact]
    public void ParseTable_ignores_tables_in_other_sections_and_stops_at_the_next_heading()
    {
        var md = """
            ## 2. 各部門進度
            | 部門 | 進展 |
            |---|---|
            | 開發 | 完成 |

            ## 4. 待辦行動清單
            | 待辦事項 | 負責人 | 期限 | 備註 |
            |---|---|---|---|
            | A 事項 | 甲 | | |

            ## 5. 其他
            | 待辦事項 | 負責人 | 期限 | 備註 |
            |---|---|---|---|
            | 不該被讀到 | 乙 | | |
            """;

        ActionItemRules.ParseTable(md, Wed).Select(i => i.Description).Should().Equal("A 事項");
    }

    [Theory]
    [InlineData("")]
    [InlineData("## 1. 摘要\n只有文字，沒有表格")]
    [InlineData("## 4. 待辦行動清單\n目前沒有待辦。")]
    public void ParseTable_returns_nothing_when_there_is_no_table(string md) =>
        ActionItemRules.ParseTable(md, Wed).Should().BeEmpty();

    // ---------- JSON ----------

    [Fact]
    public void ParseJson_tolerates_fences_prose_and_alternate_field_names()
    {
        var raw = "好的，以下是結果：\n```json\n[\n {\"description\":\"提交修復版本\",\"owner\":\"李工程師\",\"due\":\"週四\",\"note\":null},\n {\"task\":\"回報資源需求\",\"assignee\":\"各部門\",\"deadline\":\"下週五\"}\n]\n```\n希望有幫助";

        var items = ActionItemRules.ParseJson(raw, Wed);

        items.Should().HaveCount(2);
        items[0].Should().Be(new ExtractedActionItem("提交修復版本", "李工程師", "週四", new DateOnly(2026, 11, 19), null));
        items[1].Description.Should().Be("回報資源需求");
        items[1].DueDate.Should().Be(new DateOnly(2026, 11, 27));
    }

    [Theory]
    [InlineData("")]
    [InlineData("沒有待辦")]
    [InlineData("[]")]
    [InlineData("[{\"owner\":\"沒有內容\"}]")]
    [InlineData("[{broken json")]
    [InlineData("{\"description\":\"不是陣列\"}")]
    [InlineData("[1, 2, \"x\"]")]
    public void ParseJson_returns_nothing_for_unusable_output(string raw) =>
        ActionItemRules.ParseJson(raw, Wed).Should().BeEmpty();

    [Fact]
    public void ParseJson_treats_null_and_placeholder_owner_or_due_as_missing()
    {
        var item = ActionItemRules.ParseJson("[{\"description\":\"事項\",\"owner\":null,\"due\":\"未提及\"}]", Wed).Single();

        item.Owner.Should().BeNull();
        item.DueText.Should().BeNull();
        item.DueDate.Should().BeNull();
    }

    [Fact]
    public void Extraction_prompt_asks_for_json_only_and_forbids_inventing_facts()
    {
        var prompt = ActionItemRules.BuildExtractionPrompt("## 決議\n導入 Redis");

        prompt.Should().Contain("JSON 陣列").And.Contain("不要編造").And.Contain("導入 Redis");
    }

    // ---------- 期限 ----------

    [Theory]
    [InlineData("2026-11-30", 2026, 11, 30)]
    [InlineData("2026/12/5", 2026, 12, 5)]
    [InlineData("2027年1月15日", 2027, 1, 15)]
    [InlineData("11/25", 2026, 11, 25)]
    [InlineData("11月25日前", 2026, 11, 25)]
    [InlineData("12 月 5 日前", 2026, 12, 5)]       // 部分模型（qwen3）會在數字與「月」「日」之間加空白
    [InlineData("2026 年 12 月 5 日", 2026, 12, 5)]
    [InlineData("11 / 25", 2026, 11, 25)]
    [InlineData("今天", 2026, 11, 18)]
    [InlineData("明天下班前", 2026, 11, 19)]
    [InlineData("後天", 2026, 11, 20)]
    [InlineData("週五前", 2026, 11, 20)]        // 沒寫「下」：本週的週五
    [InlineData("週三", 2026, 11, 18)]          // 參考日當天算「不早於」
    [InlineData("週一", 2026, 11, 23)]          // 本週一已過 → 下一個週一
    [InlineData("本週一", 2026, 11, 16)]        // 明確寫「本」就是本週，即使已過
    [InlineData("下週五", 2026, 11, 27)]
    [InlineData("下周一", 2026, 11, 23)]
    [InlineData("下星期四", 2026, 11, 26)]
    [InlineData("下禮拜日", 2026, 11, 29)]
    [InlineData("下下週二", 2026, 12, 1)]
    public void ParseDue_understands_common_taiwanese_phrasing(string text, int y, int m, int d) =>
        ActionItemRules.ParseDue(text, Wed).Should().Be(new DateOnly(y, m, d));

    [Theory]
    [InlineData("第三周")]
    [InlineData("兩週內")]
    [InlineData("盡快")]
    [InlineData("2026-13-45")]
    [InlineData("2/30")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseDue_does_not_guess_when_the_text_has_no_definite_date(string? text) =>
        ActionItemRules.ParseDue(text, Wed).Should().BeNull();

    [Fact]
    public void ParseDue_month_day_in_early_january_after_a_december_meeting_means_next_year() =>
        ActionItemRules.ParseDue("1/15", new DateOnly(2026, 12, 28)).Should().Be(new DateOnly(2027, 1, 15));

    [Fact]
    public void ReferenceDate_converts_utc_to_the_taiwan_calendar_day()
    {
        // UTC 11/17 17:00 = 台灣 11/18 01:00
        ActionItemRules.ReferenceDate(new DateTimeOffset(2026, 11, 17, 17, 0, 0, TimeSpan.Zero)).Should().Be(Wed);
        ActionItemRules.ReferenceDate(new DateTimeOffset(2026, 11, 18, 15, 59, 0, TimeSpan.Zero)).Should().Be(Wed);
        ActionItemRules.ReferenceDate(new DateTimeOffset(2026, 11, 18, 16, 0, 0, TimeSpan.Zero)).Should().Be(Wed.AddDays(1));
    }

    // ---------- 去重與信箱 ----------

    [Fact]
    public void NormalizeKey_ignores_punctuation_spacing_case_and_width()
    {
        var a = ActionItemRules.NormalizeKey("提交 Redis 設計，下週完成。");
        var b = ActionItemRules.NormalizeKey("提交redis設計 下週完成");
        a.Should().Be(b);
        ActionItemRules.NormalizeKey("另一件事").Should().NotBe(a);
    }

    [Theory]
    [InlineData("wayne@example.com", true)]
    [InlineData("a.b-c+d@sub.example.co", true)]
    [InlineData("  wayne@example.com  ", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("no-at-sign", false)]
    [InlineData("a@b", false)]
    [InlineData("名稱 <a@example.com>", false)]
    [InlineData("a@example.com, b@example.com", false)]
    [InlineData("a@example.com;b@example.com", false)]
    [InlineData("a@example.com\r\nBcc: x@evil.com", false)]
    [InlineData("a b@example.com", false)]
    public void IsValidEmail_accepts_only_a_single_plain_address(string? value, bool valid)
    {
        ActionItemRules.IsValidEmail(value, out var normalized).Should().Be(valid);
        if (valid) normalized.Should().Be(value!.Trim());
    }

    [Fact]
    public void SafeSubject_removes_line_breaks_and_caps_the_length()
    {
        ActionItemRules.SafeSubject("標題\r\nBcc: x@evil.com").Should().NotContain("\n").And.NotContain("\r");
        ActionItemRules.SafeSubject(new string('字', 400)).Should().HaveLength(150);
    }
}
