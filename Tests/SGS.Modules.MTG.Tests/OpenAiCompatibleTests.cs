using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SGS.Modules.MTG.Application.Options;
using SGS.Modules.MTG.Application.Rules;
using SGS.Modules.MTG.Application.Services;
using Xunit;

namespace SGS.Modules.MTG.Tests;

public class ChatCompletionRulesTests
{
    [Theory]
    [InlineData("<think>使用者要摘要，我先想想</think>## 1. 核心摘要\n結論。", "## 1. 核心摘要\n結論。")]
    [InlineData("<THINK>\n多行\n思考\n</THINK>\n\n答案。", "答案。")]
    [InlineData("前言。<think>中間思考</think>結論。", "前言。結論。")]
    [InlineData("<think>被截斷，沒有結尾標籤", "")]
    [InlineData("沒有思考區塊的普通輸出。", "沒有思考區塊的普通輸出。")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void StripReasoning_removes_think_blocks(string? raw, string expected) =>
        ChatCompletionRules.StripReasoning(raw).Should().Be(expected);

    [Fact]
    public void The_no_think_switch_is_appended_only_when_reasoning_is_disabled()
    {
        ChatCompletionRules.BuildUserMessage("請摘要", disableReasoning: true).Should().Be("請摘要\n/no_think");
        ChatCompletionRules.BuildUserMessage("請摘要\n\n", disableReasoning: true).Should().Be("請摘要\n/no_think");
        ChatCompletionRules.BuildUserMessage("請摘要", disableReasoning: false).Should().Be("請摘要");
    }

    [Fact]
    public void Parse_reads_content_finish_reason_and_reasoning_token_count_but_never_the_reasoning_text()
    {
        const string json = """
            {"choices":[{"message":{"role":"assistant","content":"紀要內容","reasoning_content":"機密的思考過程"},"finish_reason":"stop"}],
             "usage":{"completion_tokens":264,"completion_tokens_details":{"reasoning_tokens":191}}}
            """;

        var result = ChatCompletionRules.Parse(json)!;

        result.Content.Should().Be("紀要內容");
        result.FinishReason.Should().Be("stop");
        result.ReasoningTokens.Should().Be(191);
        result.Content.Should().NotContain("思考過程");
    }

    [Fact]
    public void Parse_strips_think_tags_that_some_servers_leave_inside_the_content()
    {
        var result = ChatCompletionRules.Parse("""{"choices":[{"message":{"content":"<think>x</think>答案"},"finish_reason":"stop"}]}""")!;
        result.Content.Should().Be("答案");
        result.ReasoningTokens.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"choices":[]}""")]
    public void Parse_returns_null_for_unusable_responses(string json) =>
        ChatCompletionRules.Parse(json).Should().BeNull();

    [Fact]
    public void Parse_tolerates_a_null_content()
    {
        var result = ChatCompletionRules.Parse("""{"choices":[{"message":{"content":null},"finish_reason":"length"}]}""")!;
        result.Content.Should().BeEmpty();
        result.FinishReason.Should().Be("length");
    }

    [Theory]
    [InlineData("http://localhost:1234/v1", "http://localhost:1234/v1/chat/completions")]
    [InlineData("http://localhost:1234/v1/", "http://localhost:1234/v1/chat/completions")]
    [InlineData("http://localhost:1234", "http://localhost:1234/v1/chat/completions")]
    [InlineData("http://llm-server:8000/V1", "http://llm-server:8000/V1/chat/completions")]
    public void BuildEndpoint_adds_v1_only_when_missing(string baseUrl, string expected) =>
        ChatCompletionRules.BuildEndpoint(baseUrl).AbsoluteUri.Should().Be(expected);
}

public class OpenAiCompatibleClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            Calls.Add((request, body));
            return respond(request, body);
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class Monitor(MtgOptions value) : IOptionsMonitor<MtgOptions>
    {
        public MtgOptions CurrentValue => value;
        public MtgOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<MtgOptions, string?> listener) => null;
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json) => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static (OpenAiCompatibleClient Client, StubHandler Handler) Make(MtgOptions options, Func<HttpRequestMessage, string, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        return (new OpenAiCompatibleClient(new Factory(handler), new Monitor(options), NullLogger<OpenAiCompatibleClient>.Instance), handler);
    }

    private static MtgOptions Opts(Action<MtgOptions>? tweak = null)
    {
        var o = new MtgOptions { Enabled = true, Provider = "OpenAI", TextModel = "qwen/qwen3-14b", OpenAiBaseUrl = "http://localhost:1234/v1", MaxOutputTokens = 2048 };
        tweak?.Invoke(o);
        return o;
    }

    private const string Ok = """{"choices":[{"message":{"content":"## 摘要\n內容"},"finish_reason":"stop"}]}""";

    [Fact]
    public async Task Sends_a_chat_completion_request_with_the_model_limits_and_no_think_switch()
    {
        var (client, handler) = Make(Opts(), (_, _) => Json(HttpStatusCode.OK, Ok));

        var text = await client.GenerateAsync("請摘要這段逐字稿", default);

        text.Should().Be("## 摘要\n內容");
        var (request, body) = handler.Calls.Single();
        request.RequestUri!.AbsoluteUri.Should().Be("http://localhost:1234/v1/chat/completions");
        request.Headers.Authorization.Should().BeNull("LM Studio 本機不需要金鑰");
        var unescaped = System.Text.RegularExpressions.Regex.Unescape(body);
        unescaped.Should().Contain("\"model\":\"qwen/qwen3-14b\"").And.Contain("\"max_tokens\":2048").And.Contain("\"stream\":false")
            .And.Contain("請摘要這段逐字稿").And.Contain("/no_think");
    }

    [Fact]
    public async Task Without_reasoning_suppression_the_prompt_is_sent_untouched()
    {
        var (client, handler) = Make(Opts(o => o.DisableReasoning = false), (_, _) => Json(HttpStatusCode.OK, Ok));

        await client.GenerateAsync("請摘要", default);

        handler.Calls.Single().Body.Should().NotContain("/no_think");
    }

    [Fact]
    public async Task The_api_key_is_sent_as_a_bearer_token_only_when_configured()
    {
        var (client, handler) = Make(Opts(o => o.OpenAiApiKey = "secret-key"), (_, _) => Json(HttpStatusCode.OK, Ok));

        await client.GenerateAsync("x", default);

        handler.Calls.Single().Request.Headers.Authorization!.Parameter.Should().Be("secret-key");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "OpenAiApiKey")]
    [InlineData(HttpStatusCode.NotFound, "載入模型")]
    [InlineData(HttpStatusCode.BadRequest, "載入模型")]
    [InlineData(HttpStatusCode.InternalServerError, "HTTP 500")]
    public async Task Http_errors_become_safe_messages_that_never_echo_the_response_body(HttpStatusCode status, string expected)
    {
        var (client, _) = Make(Opts(), (_, _) => Json(status, """{"error":"逐字稿片段：機密內容"}"""));

        var ex = (await client.Invoking(c => c.GenerateAsync("x", default)).Should().ThrowAsync<InvalidOperationException>()).Which;

        ex.Message.Should().Contain(expected).And.NotContain("機密內容");
    }

    [Fact]
    public async Task An_unreachable_server_gives_a_clear_message()
    {
        var (client, _) = Make(Opts(), (_, _) => throw new HttpRequestException("refused"));
        await client.Invoking(c => c.GenerateAsync("x", default)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*LM Studio*");
    }

    [Fact]
    public async Task Output_cut_off_before_any_content_explains_the_reasoning_budget_problem()
    {
        var (client, _) = Make(Opts(), (_, _) => Json(HttpStatusCode.OK, """{"choices":[{"message":{"content":"","reasoning_content":"想很久"},"finish_reason":"length"}]}"""));
        await client.Invoking(c => c.GenerateAsync("x", default)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*DisableReasoning*");
    }

    [Fact]
    public async Task A_malformed_response_is_rejected()
    {
        var (client, _) = Make(Opts(), (_, _) => Json(HttpStatusCode.OK, "{}"));
        await client.Invoking(c => c.GenerateAsync("x", default)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*格式*");
    }

    [Fact]
    public async Task An_external_address_is_refused_before_any_request_is_made()
    {
        var (client, handler) = Make(Opts(o => o.OpenAiBaseUrl = "https://api.openai.com/v1"), (_, _) => Json(HttpStatusCode.OK, Ok));

        await client.Invoking(c => c.GenerateAsync("逐字稿", default)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*本機或內網*");
        handler.Calls.Should().BeEmpty("逐字稿不得送往外部服務");
    }

    [Fact]
    public void Model_name_comes_from_the_text_model_setting() =>
        Make(Opts(), (_, _) => Json(HttpStatusCode.OK, Ok)).Client.ModelName.Should().Be("qwen/qwen3-14b");

    [Theory]
    [InlineData("OpenAI", true)]
    [InlineData("openai", true)]
    [InlineData("Ollama", false)]
    [InlineData("", false)]
    public void Provider_selection_is_case_insensitive_and_defaults_to_ollama(string provider, bool usesOpenAi) =>
        new MtgOptions { Provider = provider }.UsesOpenAi.Should().Be(usesOpenAi);

    [Fact]
    public void The_default_provider_is_still_ollama_so_existing_deployments_are_unchanged() =>
        new MtgOptions().UsesOpenAi.Should().BeFalse();
}
