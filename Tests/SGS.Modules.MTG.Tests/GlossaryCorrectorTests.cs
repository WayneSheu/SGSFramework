using FluentAssertions;
using SGS.Modules.MTG.Application.Rules;
using Xunit;

namespace SGS.Modules.MTG.Tests;

public class GlossaryCorrectorTests
{
    private static readonly IReadOnlyList<GlossaryTerm> Terms =
    [
        new("Kubernetes", ["酷伯內提斯", "k8s"]),
        new("API", []),
        new("Sprint", ["四筆潤特"])
    ];

    [Fact]
    public void 誤聽詞會被換成標準詞()
    {
        GlossaryCorrector.Correct("我們把服務部署到酷伯內提斯", Terms).Should().Be("我們把服務部署到Kubernetes");
    }

    [Fact]
    public void 英文大小寫會標準化()
    {
        GlossaryCorrector.Correct("呼叫 api 與 Api 兩次", Terms).Should().Be("呼叫 API 與 API 兩次");
    }

    [Fact]
    public void 英文詞不會誤改其他單字的一部分()
    {
        GlossaryCorrector.Correct("capital 與 rapid 都不該被改", Terms).Should().Be("capital 與 rapid 都不該被改");
    }

    [Fact]
    public void 英數誤聽詞依邊界比對()
    {
        GlossaryCorrector.Correct("k8s 叢集與 k8sx 不同", Terms).Should().Be("Kubernetes 叢集與 k8sx 不同");
    }

    [Fact]
    public void 沒有術語時原文不變()
    {
        GlossaryCorrector.Correct("原文", []).Should().Be("原文");
        GlossaryCorrector.Correct(null, Terms).Should().BeEmpty();
    }

    [Fact]
    public void 替換結果不會被其他規則二次處理()
    {
        IReadOnlyList<GlossaryTerm> terms = [new("A", ["B"]), new("B", ["C"])];
        GlossaryCorrector.Correct("B", terms).Should().Be("A", "單趟掃描，B 換成 A 後不應再被其他規則影響");
    }

    [Fact]
    public void 較長的誤聽詞優先於較短的()
    {
        IReadOnlyList<GlossaryTerm> terms = [new("雲端", ["雲"]), new("雲端運算", ["雲端運"])];
        GlossaryCorrector.Correct("雲端運算", terms).Should().Be("雲端運算");
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("a|b", 2)]
    [InlineData(" a | | b ||", 2)]
    public void 解析誤聽詞(string? raw, int expected)
    {
        GlossaryCorrector.ParseVariants(raw).Should().HaveCount(expected);
    }

    [Fact]
    public void 初始提示詞列出術語並去重()
    {
        var prompt = GlossaryCorrector.BuildInitialPrompt([new("API", []), new("api", []), new("Deploy", [])]);
        prompt.Should().Contain("API").And.Contain("Deploy");
        prompt.Split("API", StringSplitOptions.None).Should().HaveCount(2, "API 只應出現一次");
    }

    [Fact]
    public void 中文轉錄的提示詞永遠帶繁體提示句_術語表是空的也一樣()
    {
        // 實測 Whisper 沒有提示詞時整段輸出簡體；術語表為空時若不送提示詞，轉錄結果就會是簡體
        GlossaryCorrector.BuildInitialPrompt([]).Should().Be(GlossaryCorrector.TraditionalChineseHint);
        GlossaryCorrector.BuildInitialPrompt([new("API", [])]).Should().StartWith("以下是繁體中文的會議記錄");
    }

    [Fact]
    public void 非中文轉錄時不帶繁體提示句_且沒有術語就不送提示詞()
    {
        GlossaryCorrector.BuildInitialPrompt([], traditionalChinese: false).Should().BeEmpty();
        var prompt = GlossaryCorrector.BuildInitialPrompt([new("Kubernetes", [])], traditionalChinese: false);
        prompt.Should().Contain("Kubernetes").And.NotContain("繁體");
    }

    [Fact]
    public void 提示詞的術語數量有上限()
    {
        var terms = Enumerable.Range(0, 100).Select(i => new GlossaryTerm($"term{i:D3}", [])).ToList();
        var prompt = GlossaryCorrector.BuildInitialPrompt(terms, maxTerms: 5);
        prompt.Should().Contain("term004").And.NotContain("term005");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("auto", true)]
    [InlineData("zh", true)]
    [InlineData("ZH", true)]
    [InlineData("zh-TW", true)]
    [InlineData("en", false)]
    [InlineData("ja", false)]
    public void 判斷是否需要繁體提示(string? language, bool expected) => GlossaryCorrector.IsChineseOrAuto(language).Should().Be(expected);
}
