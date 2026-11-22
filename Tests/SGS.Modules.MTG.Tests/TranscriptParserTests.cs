using FluentAssertions;
using SGS.Modules.MTG.Application.Rules;
using Xunit;

namespace SGS.Modules.MTG.Tests;

public class TranscriptParserTests
{
    [Fact]
    public void 方括號講者會被拆出()
    {
        var r = TranscriptParser.Parse("[我方] 大家好");
        r.Should().ContainSingle().Which.Should().Be(new ParsedSegment(1, "我方", "大家好"));
    }

    [Theory]
    [InlineData("王經理：本週進度正常", "王經理", "本週進度正常")]
    [InlineData("Alice: let us start", "Alice", "let us start")]
    public void 冒號講者會被拆出(string line, string speaker, string text)
    {
        TranscriptParser.Parse(line).Should().ContainSingle().Which.Should().Be(new ParsedSegment(1, speaker, text));
    }

    [Fact]
    public void 太長的冒號前文不視為講者()
    {
        var line = "這是一整句很長很長很長很長很長很長很長的敘述：後面還有內容";
        var r = TranscriptParser.Parse(line);
        r.Single().Speaker.Should().BeNull();
        r.Single().Text.Should().Be(line);
    }

    [Fact]
    public void 含空白的前文不視為講者()
    {
        TranscriptParser.Parse("注意事項 如下：請準時").Single().Speaker.Should().BeNull();
    }

    [Fact]
    public void 空行會被略過且序號連續並可指定起始序號()
    {
        var r = TranscriptParser.Parse("a\r\n\r\n  \nb\rc", startSeq: 10);
        r.Select(x => x.Seq).Should().Equal(10, 11, 12);
        r.Select(x => x.Text).Should().Equal("a", "b", "c");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  \n ")]
    public void 空內容回傳空清單(string? text) => TranscriptParser.Parse(text).Should().BeEmpty();
}
