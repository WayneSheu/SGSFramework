using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.MTG.Application.Options;
using SGS.Modules.MTG.Application.Rules;

namespace SGS.Modules.MTG.Application.Services;

/// <summary>
/// 呼叫 OpenAI 相容的 <c>/v1/chat/completions</c>（LM Studio、vLLM、llama.cpp server…）。位址每次呼叫前都再驗證，只允許本機或內網。
/// <para>
/// 與 Ollama 版的差異：OpenAI 介面沒有 <c>num_ctx</c> 參數，上下文長度由服務端載入模型時決定
/// （LM Studio 請在載入模型時把 Context Length 設到至少 8192），所以 <c>Meeting:NumCtx</c> 對這個提供者無效。
/// 推理模型（Qwen3…）的思考預設關閉（<c>Meeting:DisableReasoning</c>）：思考會佔用輸出上限、拖慢數倍，也不是紀要內容。
/// </para>
/// </summary>
public sealed class OpenAiCompatibleClient(IHttpClientFactory httpClientFactory, IOptionsMonitor<MtgOptions> options, ILogger<OpenAiCompatibleClient> logger) : ITextGenerator
{
    public const string HttpClientName = "mtg-openai";

    public string ModelName => options.CurrentValue.TextModel;

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var opt = options.CurrentValue;
        if (!InternalUrlRules.IsAllowed(opt.OpenAiBaseUrl))
            throw new InvalidOperationException("模型服務位址必須是本機或內網位址。");

        var body = JsonSerializer.Serialize(new
        {
            model = opt.TextModel,
            messages = new[] { new { role = "user", content = ChatCompletionRules.BuildUserMessage(prompt, opt.DisableReasoning) } },
            temperature = 0.2, // 摘要求穩定、少發揮
            max_tokens = Math.Clamp(opt.MaxOutputTokens, 256, 32768),
            stream = false
        });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(Math.Max(1, opt.TimeoutMinutes)));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionRules.BuildEndpoint(opt.OpenAiBaseUrl))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(opt.OpenAiApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opt.OpenAiApiKey);

            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // 回應內容可能含逐字稿片段或模型細節，只記錄狀態碼，不記錄內容、也不回傳給使用者
                logger.LogWarning("[MTG] 模型服務回應 HTTP {Status}。", (int)response.StatusCode);
                throw new InvalidOperationException(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "模型服務拒絕存取，請檢查 Meeting:OpenAiApiKey。",
                    HttpStatusCode.NotFound or HttpStatusCode.BadRequest => $"模型服務找不到或沒有載入模型 {opt.TextModel}，請在服務端載入該模型（LM Studio：載入模型並把 Context Length 設到至少 8192）。",
                    _ => $"模型服務回應 HTTP {(int)response.StatusCode}。"
                });
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            var result = ChatCompletionRules.Parse(json) ?? throw new InvalidOperationException("模型服務回傳的格式不正確。");

            if (result.Content.Length == 0)
            {
                throw new InvalidOperationException(result.FinishReason == "length"
                    ? "模型輸出在產生內容前就達到上限（可能是思考過程用完額度），請確認 Meeting:DisableReasoning 為 true，或調高 Meeting:MaxOutputTokens。"
                    : "模型沒有產生任何內容。");
            }

            if (result.ReasoningTokens > 0)
                logger.LogInformation("[MTG] 模型用了 {Tokens} 個 token 思考；建議開啟 Meeting:DisableReasoning 以縮短等待。", result.ReasoningTokens);
            return result.Content;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"模型生成逾時（超過 {opt.TimeoutMinutes} 分鐘）。");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "[MTG] 無法連線模型服務。");
            throw new InvalidOperationException("無法連線模型服務，請確認 LM Studio（或其他 OpenAI 相容服務）已啟動並載入模型。");
        }
    }
}
