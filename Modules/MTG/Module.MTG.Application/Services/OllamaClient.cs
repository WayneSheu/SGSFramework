using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.MTG.Application.Options;
using SGS.Modules.MTG.Application.Rules;

namespace SGS.Modules.MTG.Application.Services;

/// <summary>文字生成介面（Ollama /api/generate）；摘要引擎只依賴此介面，方便以假實作測試。</summary>
public interface ITextGenerator
{
    /// <summary>送出提示詞，回傳模型的文字輸出。</summary>
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken);

    /// <summary>目前使用的模型名稱（寫入紀要供追溯）。</summary>
    string ModelName { get; }
}

/// <summary>呼叫本機 Ollama 的 /api/generate，並強制鎖定 num_ctx。位址每次呼叫前都再驗證，只允許本機或內網。</summary>
public sealed class OllamaClient(IHttpClientFactory httpClientFactory, IOptionsMonitor<MtgOptions> options, ILogger<OllamaClient> logger) : ITextGenerator
{
    public const string HttpClientName = "mtg-ollama";

    public string ModelName => options.CurrentValue.TextModel;

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var opt = options.CurrentValue;
        if (!InternalUrlRules.IsAllowed(opt.OllamaBaseUrl))
            throw new InvalidOperationException("Ollama 位址必須是本機或內網位址。");

        var body = new GenerateRequest
        {
            Model = opt.TextModel,
            Prompt = prompt,
            Stream = false,
            Think = false, // 關閉思考過程：縮短等待時間，也避免思考內容混入紀要
            Options = new GenerateOptions
            {
                Temperature = 0.2, // 摘要求穩定、少發揮
                NumCtx = Math.Max(2048, opt.NumCtx),
                NumPredict = Math.Clamp(opt.MaxOutputTokens, 256, 32768)
            }
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(Math.Max(1, opt.TimeoutMinutes)));

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var uri = new Uri(new Uri(opt.OllamaBaseUrl.TrimEnd('/') + "/"), "api/generate");
            using var response = await client.PostAsJsonAsync(uri, body, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // 回應內容可能含逐字稿片段或模型細節，只記錄狀態碼，不記錄內容、也不回傳給使用者
                logger.LogWarning("[MTG] Ollama 回應 HTTP {Status}。", (int)response.StatusCode);
                throw new InvalidOperationException(response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? $"Ollama 找不到模型 {opt.TextModel}，請先執行 ollama pull。"
                    : $"Ollama 回應 HTTP {(int)response.StatusCode}。");
            }

            var result = await response.Content.ReadFromJsonAsync<GenerateResponse>(cts.Token).ConfigureAwait(false);
            return result?.Response ?? string.Empty;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"模型生成逾時（超過 {opt.TimeoutMinutes} 分鐘）。");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "[MTG] 無法連線 Ollama。");
            throw new InvalidOperationException("無法連線 Ollama，請確認服務已啟動。");
        }
    }

    private sealed class GenerateRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;
        [JsonPropertyName("stream")] public bool Stream { get; set; }
        [JsonPropertyName("think")] public bool Think { get; set; }
        [JsonPropertyName("options")] public GenerateOptions? Options { get; set; }
    }

    private sealed class GenerateOptions
    {
        [JsonPropertyName("temperature")] public double Temperature { get; set; }
        [JsonPropertyName("num_ctx")] public int NumCtx { get; set; }
        [JsonPropertyName("num_predict")] public int NumPredict { get; set; }
    }

    private sealed class GenerateResponse
    {
        [JsonPropertyName("response")] public string? Response { get; set; }
    }
}
