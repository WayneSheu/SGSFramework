using FluentAssertions;
using SGS.Modules.MTG.Application.Rules;
using Xunit;

namespace SGS.Modules.MTG.Tests;

public class WhisperResponseParserTests
{
    [Fact]
    public void verbose_json會解出片段_起訖秒數_語言與長度()
    {
        const string json = """
            {"task":"transcribe","language":"zh","duration":12.5,"text":"大家好 今天開會",
             "segments":[{"id":0,"start":0.0,"end":3.2,"text":" 大家好 "},{"id":1,"start":3.2,"end":12.5,"text":"今天開會"}]}
            """;

        var r = WhisperResponseParser.Parse(json);

        r.Language.Should().Be("zh");
        r.DurationSeconds.Should().Be(12.5);
        r.Segments.Should().Equal(new TranscribedSegment(0.0, 3.2, "大家好"), new TranscribedSegment(3.2, 12.5, "今天開會"));
    }

    [Fact]
    public void 空白片段會被丟棄()
    {
        var r = WhisperResponseParser.Parse("""{"segments":[{"start":0,"end":1,"text":"  "},{"start":1,"end":2,"text":"有內容"},{"text":""}]}""");
        r.Segments.Should().ContainSingle().Which.Text.Should().Be("有內容");
    }

    [Theory]
    [InlineData("""{"segments":[{"start":-1,"end":2,"text":"a"}]}""")]
    [InlineData("""{"segments":[{"start":5,"end":2,"text":"a"}]}""")]
    public void 不合理的起訖時間視為未提供_文字仍保留(string json)
    {
        var s = WhisperResponseParser.Parse(json).Segments.Should().ContainSingle().Subject;
        s.Text.Should().Be("a");
        s.StartSeconds.Should().BeNull();
        s.EndSeconds.Should().BeNull();
    }

    [Fact]
    public void 時間為字串數字時也能解析()
    {
        var s = WhisperResponseParser.Parse("""{"segments":[{"start":"1.5","end":"2.5","text":"a"}]}""").Segments.Single();
        (s.StartSeconds, s.EndSeconds).Should().Be((1.5, 2.5));
    }

    [Fact]
    public void 沒有segments時改用text並依句尾標點切段()
    {
        var r = WhisperResponseParser.Parse("""{"text":"第一句。第二句！第三句？\n第四句"}""");

        r.Segments.Select(s => s.Text).Should().Equal("第一句。", "第二句！", "第三句？", "第四句");
        r.Segments.Should().OnlyContain(s => s.StartSeconds == null && s.EndSeconds == null);
    }

    [Fact]
    public void 沒有任何內容時回傳空片段清單()
    {
        WhisperResponseParser.Parse("""{"text":"   ","segments":[]}""").Segments.Should().BeEmpty();
        WhisperResponseParser.Parse("{}").Segments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 空回應拋出FormatException(string? json) =>
        FluentActions.Invoking(() => WhisperResponseParser.Parse(json)).Should().Throw<FormatException>();

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("\"字串\"")]
    public void 非物件或非JSON拋出FormatException(string json) =>
        FluentActions.Invoking(() => WhisperResponseParser.Parse(json)).Should().Throw<FormatException>();

    [Fact]
    public void 切句時孤立的標點併回前一句()
    {
        WhisperResponseParser.SplitText("你好。。").Should().Equal("你好。。");
        WhisperResponseParser.SplitText("今天天氣很好。 ！").Should().Equal("今天天氣很好。！");
    }

    [Fact]
    public void 只有換行與空白的文字不產生片段() =>
        WhisperResponseParser.SplitText("\n \r\n  ").Should().BeEmpty();
}
