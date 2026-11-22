using FluentAssertions;
using SGS.Modules.MTG.Application.Rules;
using Xunit;

namespace SGS.Modules.MTG.Tests;

public class TranscriptChunkerTests
{
    private static string Lines(int count, int lineLength) =>
        string.Join('\n', Enumerable.Range(0, count).Select(i => $"L{i:D3}".PadRight(lineLength, 'x')));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n \r\n ")]
    public void 空逐字稿回傳空清單(string? text)
    {
        TranscriptChunker.Chunk(text, 1000, 100).Should().BeEmpty("沒有內容就不該產生任何切塊");
    }

    [Fact]
    public void 短逐字稿只有一塊()
    {
        var chunks = TranscriptChunker.Chunk("第一行\n第二行", 1000, 100);
        chunks.Should().ContainSingle().Which.Should().Be("第一行\n第二行");
    }

    [Fact]
    public void 每一塊都不超過上限()
    {
        var chunks = TranscriptChunker.Chunk(Lines(200, 50), 500, 100);
        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().OnlyContain(c => c.Length <= 500, "切塊上限是硬性限制");
    }

    [Fact]
    public void 相鄰切塊保留重疊的前後文()
    {
        var chunks = TranscriptChunker.Chunk(Lines(60, 50), 500, 120);
        var lastLineOfFirst = chunks[0].Split('\n')[^1];
        chunks[1].Split('\n').Should().Contain(lastLineOfFirst, "下一塊應從上一塊結尾的行開始，保留語意連貫");
    }

    [Fact]
    public void 沒有重疊時相鄰切塊不重複()
    {
        var chunks = TranscriptChunker.Chunk(Lines(60, 50), 500, 0);
        var all = chunks.SelectMany(c => c.Split('\n')).ToList();
        all.Should().OnlyHaveUniqueItems();
        all.Should().HaveCount(60, "每一行都應該出現且只出現一次");
    }

    [Fact]
    public void 單一超長行會被強制切開且不會無窮迴圈()
    {
        var chunks = TranscriptChunker.Chunk(new string('字', 2500), 1000, 300);
        chunks.Should().HaveCount(3);
        chunks.Should().OnlyContain(c => c.Length <= 1000);
        string.Concat(chunks).Length.Should().Be(2500, "強制切開不應遺失或重複文字");
    }

    [Fact]
    public void 上限過小時拋出例外()
    {
        var act = () => TranscriptChunker.Chunk("abc", 50, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void 組合逐字稿文字會帶講者並略過空白片段()
    {
        var text = TranscriptChunker.BuildTranscriptText([("我方", "你好"), (null, "沒有講者"), ("與會者 A", "  "), ("與會者 B", "多行\n內容")]);
        text.Should().Be("[我方] 你好\n沒有講者\n[與會者 B] 多行 內容");
    }
}
