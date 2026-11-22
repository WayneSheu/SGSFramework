using FluentAssertions;
using SGS.Modules.MTG.Application.Rules;
using Xunit;

namespace SGS.Modules.MTG.Tests;

public class HallucinationFilterTests
{
    [Theory]
    // 真實 whisper 在本專案模擬錄音結尾實際產生的文字
    [InlineData("请不吝点赞 订阅 转发 打赏支持明镜与点点栏目")]
    [InlineData("字幕志愿者 杨茜茜")]
    // 繁體寫法
    [InlineData("請不吝點贊 訂閱 轉發 打賞支持明鏡與點點欄目")]
    [InlineData("字幕志願者 楊茜茜")]
    // 其他常見片尾
    [InlineData("感謝觀看")]
    [InlineData("感谢观看。")]
    [InlineData("謝謝觀看！")]
    [InlineData("欢迎订阅我的频道")]
    [InlineData("Thanks for watching!")]
    [InlineData("Thank you for watching.")]
    [InlineData("Please subscribe to my channel")]
    [InlineData("Subtitles by the Amara.org community")]
    [InlineData("由 Amara.org 社群提供的字幕")]
    [InlineData("ご視聴ありがとうございました")]
    [InlineData("  請不吝點贊，訂閱，轉發，打賞  ")]
    public void 已知幻聽片語會被判定為幻聽(string text) => HallucinationFilter.IsHallucination(text).Should().BeTrue();

    [Theory]
    [InlineData("各位好，今天的週會先報告進度。")]
    [InlineData("謝謝大家。")]                                  // 會議結尾常見的真話，不能刪
    [InlineData("散會。")]
    [InlineData("字幕檔案請依規格產生，並放在共用資料夾。")]    // 提到「字幕」的正常談話
    [InlineData("客戶說請訂閱服務的人需要先完成身分驗證，這部分要列入需求。")] // 含「請訂閱」但只佔一小部分
    [InlineData("我們的影片網站有訂閱功能，使用者可以感謝觀看者並回覆留言，這是第二階段的需求。")]
    [InlineData("Thanks for the update, let's move on to the next item and review the API latency numbers.")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("。，！")]
    public void 正常談話不會被誤刪(string text) => HallucinationFilter.IsHallucination(text).Should().BeFalse();

    [Fact]
    public void 片語必須涵蓋整段的一半以上_前面夾帶少量真話仍視為幻聽()
    {
        // 「好的。」+ 片尾：片語佔絕大部分
        HallucinationFilter.IsHallucination("好的。请不吝点赞 订阅 转发 打赏支持明镜与点点栏目").Should().BeTrue();
        // 片語只佔一小部分 → 保留
        HallucinationFilter.IsHallucination("今天要討論的是第三季的銷售目標與各區域的達成狀況，另外感謝觀看").Should().BeFalse();
    }

    [Fact]
    public void Apply只移除幻聽片段_保留其餘片段的順序與時間()
    {
        var result = new TranscriptionResult(
        [
            new(0.0, 3.3, "各位好，今天的週會先報告進度。"),
            new(3.3, 9.0, "訂單系統已經完成部署。"),
            new(82.7, 83.9, "请不吝点赞 订阅 转发 打赏支持明镜与点点栏目"),
            new(null, null, "散會。")
        ], "chinese", 84.0);

        var (cleaned, removed) = HallucinationFilter.Apply(result);

        removed.Should().Be(1);
        cleaned.Segments.Select(s => s.Text).Should().Equal("各位好，今天的週會先報告進度。", "訂單系統已經完成部署。", "散會。");
        cleaned.Segments[1].Should().Be(new TranscribedSegment(3.3, 9.0, "訂單系統已經完成部署。"));
        cleaned.Language.Should().Be("chinese");
        cleaned.DurationSeconds.Should().Be(84.0, "音檔長度不受過濾影響");
    }

    [Fact]
    public void 沒有幻聽時原樣回傳同一個物件()
    {
        var result = new TranscriptionResult([new(0.0, 1.0, "你好")], "zh", 1.0);

        var (cleaned, removed) = HallucinationFilter.Apply(result);

        removed.Should().Be(0);
        cleaned.Should().BeSameAs(result);
    }

    [Fact]
    public void 全部都是幻聽時結果為空()
    {
        var result = new TranscriptionResult([new(0.0, 2.0, "字幕志愿者 杨茜茜"), new(2.0, 4.0, "感謝觀看")], null, 4.0);

        var (cleaned, removed) = HallucinationFilter.Apply(result);

        removed.Should().Be(2);
        cleaned.Segments.Should().BeEmpty();
    }

    [Fact]
    public void 空結果可安全處理() =>
        HallucinationFilter.Apply(new TranscriptionResult([], null, null)).Removed.Should().Be(0);

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    public void 沒有移除時不產生說明(int removed, string? expected) => HallucinationFilter.DescribeRemoval(removed).Should().Be(expected);

    [Fact]
    public void 有移除時說明包含數量() => HallucinationFilter.DescribeRemoval(2).Should().Contain("2 段");

    [Fact]
    public void 正規化會去除標點空白並轉小寫() =>
        HallucinationFilter.Normalize(" Thanks, FOR watching! 。請 不吝 ").Should().Be("thanksforwatching請不吝");
}
