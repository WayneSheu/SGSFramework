using FluentAssertions;
using Microsoft.Extensions.Options;
using SGS.Modules.MTG.Application.Options;
using SGS.Modules.MTG.Application.Services;
using Xunit;

namespace SGS.Modules.MTG.Tests;

public class MapReduceSummarizerTests
{
    private sealed class FakeGenerator(Func<string, string> respond) : ITextGenerator
    {
        public List<string> Prompts { get; } = [];
        public string ModelName => "fake";

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            return Task.FromResult(respond(prompt));
        }
    }

    private sealed class FixedOptions(MtgOptions value) : IOptionsMonitor<MtgOptions>
    {
        public MtgOptions CurrentValue => value;
        public MtgOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<MtgOptions, string?> listener) => null;
    }

    private static readonly SummaryTemplate Template = new("MAP指令", "REDUCE骨架");

    private static MapReduceSummarizer Create(FakeGenerator generator, int chunkChars = 500, int overlap = 50) =>
        new(generator, new FixedOptions(new MtgOptions { ChunkChars = chunkChars, OverlapChars = overlap }));

    private static string Transcript(int lines) =>
        string.Join('\n', Enumerable.Range(0, lines).Select(i => $"[我方] 第{i:D3}行內容".PadRight(60, '。')));

    [Fact]
    public async Task 短逐字稿_一次Map加一次Reduce()
    {
        var gen = new FakeGenerator(p => p.Contains("REDUCE骨架") ? "## 紀要" : "重點");
        var result = await Create(gen).SummarizeAsync(Transcript(3), Template, CancellationToken.None);

        result.Should().Be("## 紀要");
        gen.Prompts.Should().HaveCount(2);
        gen.Prompts[0].Should().Contain("MAP指令").And.Contain("[我方]");
        gen.Prompts[1].Should().Contain("REDUCE骨架").And.Contain("重點");
    }

    [Fact]
    public async Task 每個提示詞都附加防幻覺約束()
    {
        var gen = new FakeGenerator(p => p.Contains("REDUCE骨架") ? "ok" : "重點");
        await Create(gen).SummarizeAsync(Transcript(40), Template, CancellationToken.None);
        gen.Prompts.Should().OnlyContain(p => p.Contains("不得編造"));
    }

    [Fact]
    public async Task 長逐字稿_Map次數大於一且Reduce最後執行一次()
    {
        var gen = new FakeGenerator(p => p.Contains("REDUCE骨架") ? "最終" : "短");
        var result = await Create(gen).SummarizeAsync(Transcript(40), Template, CancellationToken.None);

        result.Should().Be("最終");
        gen.Prompts.Count(p => p.Contains("【逐字稿片段")).Should().BeGreaterThan(1);
        gen.Prompts.Count(p => p.Contains("REDUCE骨架")).Should().Be(1);
        gen.Prompts[^1].Should().Contain("REDUCE骨架", "Reduce 必須是最後一次呼叫");
    }

    [Fact]
    public async Task Map結果過長時會先分批濃縮再Reduce()
    {
        // Map 每次產出 200 字：合併後遠超 ChunkChars（500），且每批可放 2 段，應觸發濃縮
        var gen = new FakeGenerator(p =>
            p.Contains("REDUCE骨架") ? "最終" : p.Contains("合併、去除重複") ? "濃縮" : new string('料', 200));
        await Create(gen).SummarizeAsync(Transcript(40), Template, CancellationToken.None);

        gen.Prompts.Should().Contain(p => p.Contains("合併、去除重複"));
        gen.Prompts[^1].Should().Contain("REDUCE骨架");
    }

    [Fact]
    public async Task 濃縮無法縮小時不會無窮迴圈()
    {
        // 濃縮與 Map 都回傳超長內容：必須在有限次數內結束
        var gen = new FakeGenerator(p => p.Contains("REDUCE骨架") ? "最終" : new string('料', 2000));
        var act = async () => await Create(gen).SummarizeAsync(Transcript(40), Template, CancellationToken.None);
        await act.Should().NotThrowAsync();
        gen.Prompts.Count.Should().BeLessThan(200);
    }

    [Fact]
    public async Task 空逐字稿拋出例外()
    {
        var gen = new FakeGenerator(_ => "x");
        var act = async () => await Create(gen).SummarizeAsync("  ", Template, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
        gen.Prompts.Should().BeEmpty();
    }

    [Fact]
    public async Task 模型回傳空紀要時拋出例外()
    {
        var gen = new FakeGenerator(p => p.Contains("REDUCE骨架") ? "  " : "重點");
        var act = async () => await Create(gen).SummarizeAsync(Transcript(3), Template, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task 取消時停止()
    {
        var gen = new FakeGenerator(_ => "x");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var act = async () => await Create(gen).SummarizeAsync(Transcript(40), Template, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("```markdown\n## 標題\n內容\n```", "## 標題\n內容")]
    [InlineData("```\n## 標題\n```", "## 標題")]
    [InlineData("  ## 純文字  ", "## 純文字")]
    public void 去除最外層Markdown圍欄(string input, string expected)
    {
        MapReduceSummarizer.StripMarkdownFence(input).Should().Be(expected);
    }

    [Fact]
    public void 非最外層的程式碼圍欄保持不變()
    {
        const string input = "## 標題\n```csharp\ncode\n```";
        MapReduceSummarizer.StripMarkdownFence(input).Should().Be(input);
    }
}
