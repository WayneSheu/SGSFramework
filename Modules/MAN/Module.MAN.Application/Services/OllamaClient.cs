using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.MAN.Application.Options;
using SGS.Modules.MAN.Application.Rules;

namespace SGS.Modules.MAN.Application.Services;

/// <summary>本機 Ollama 的服務狀態。</summary>
public sealed record OllamaHealth(bool Reachable, IReadOnlyList<string> Models, string? Error);

/// <summary>呼叫本機 Ollama（/api/chat 與 /api/tags）。位址在每次呼叫前都再驗證一次，只允許本機或內網。</summary>
public interface IOllamaClient
{
    /// <summary>讀取已安裝模型清單；無法連線時 Reachable=false。</summary>
    Task<OllamaHealth> GetHealthAsync(CancellationToken cancellationToken);

    /// <summary>送出截圖與提示詞，回傳模型的文字輸出（預期為 JSON）。</summary>
    Task<string> ChatWithImageAsync(string system, string prompt, byte[] image, CancellationToken cancellationToken);
}

public sealed class OllamaClient(IHttpClientFactory httpClientFactory, IOptionsMonitor<ManOptions> options, ILogger<OllamaClient> logger) : IOllamaClient
{
    public const string HttpClientName = "man-ollama";

    public async Task<OllamaHealth> GetHealthAsync(CancellationToken cancellationToken)
    {
        var opt = options.CurrentValue;
        if (!GenerationRules.IsAllowedOllamaUrl(opt.OllamaBaseUrl))
            return new OllamaHealth(false, [], "Ollama 位址必須是本機或內網位址。");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(Combine(opt.OllamaBaseUrl, "api/tags"), cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new OllamaHealth(false, [], $"Ollama 回應 HTTP {(int)response.StatusCode}。");

            var tags = await response.Content.ReadFromJsonAsync<TagsResponse>(cancellationToken).ConfigureAwait(false);
            var names = tags?.Models?.Select(m => m.Name).Where(n => !string.IsNullOrEmpty(n)).Cast<string>().ToList() ?? [];
            return new OllamaHealth(true, names, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new OllamaHealth(false, [], "連線 Ollama 逾時。");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "[MAN] 無法連線 Ollama。");
            return new OllamaHealth(false, [], "無法連線 Ollama，請確認服務已啟動。");
        }
    }

    public async Task<string> ChatWithImageAsync(string system, string prompt, byte[] image, CancellationToken cancellationToken)
    {
        var opt = options.CurrentValue;
        if (!GenerationRules.IsAllowedOllamaUrl(opt.OllamaBaseUrl))
            throw new InvalidOperationException("Ollama 位址必須是本機或內網位址。");

        var body = new ChatRequest
        {
            Model = opt.VisionModel,
            Stream = false,
            Think = false,   // 關閉思考過程：縮短等待時間，也避免思考內容混入輸出
            Format = "json", // 強制輸出合法 JSON
            Options = new ChatOptions { Temperature = 0.3, NumCtx = Math.Max(2048, opt.NumCtx), NumPredict = Math.Clamp(opt.MaxOutputTokens, 256, 32768) },
            Messages =
            [
                new ChatMessage("system", system, null),
                new ChatMessage("user", prompt, [Convert.ToBase64String(image)])
            ]
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(Math.Max(1, opt.TimeoutMinutes)));

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsJsonAsync(Combine(opt.OllamaBaseUrl, "api/chat"), body, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // 回應內容可能含模型或系統細節，只記錄在日誌，不回傳給使用者
                var detail = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                logger.LogWarning("[MAN] Ollama 回應 HTTP {Status}：{Detail}", (int)response.StatusCode, GenerationRules.SafeMessage(detail, 300));
                throw new InvalidOperationException(response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? $"Ollama 找不到模型 {opt.VisionModel}，請先執行 ollama pull。"
                    : $"Ollama 回應 HTTP {(int)response.StatusCode}。");
            }

            var result = await response.Content.ReadFromJsonAsync<ChatResponse>(cts.Token).ConfigureAwait(false);
            var content = result?.Message?.Content ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                // thinking 版模型思考過長（done_reason=length）時正文會是空的；只記錄原因，不記錄模型內容
                logger.LogWarning("[MAN] 模型回傳空內容：done_reason={DoneReason}，思考內容長度={ThinkingLength}。",
                    result?.DoneReason ?? "(none)", result?.Message?.Thinking?.Length ?? 0);
            }

            return content;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"生成逾時（超過 {opt.TimeoutMinutes} 分鐘）。");
        }
    }

    private static Uri Combine(string baseUrl, string path) => new(new Uri(baseUrl.TrimEnd('/') + "/"), path);

    private sealed class ChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; } = [];
        [JsonPropertyName("stream")] public bool Stream { get; set; }
        [JsonPropertyName("think")] public bool Think { get; set; }
        [JsonPropertyName("format")] public string? Format { get; set; }
        [JsonPropertyName("options")] public ChatOptions? Options { get; set; }
    }

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("images"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<string>? Images);

    private sealed class ChatOptions
    {
        [JsonPropertyName("temperature")] public double Temperature { get; set; }
        [JsonPropertyName("num_ctx")] public int NumCtx { get; set; }
        [JsonPropertyName("num_predict")] public int NumPredict { get; set; }
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("message")] public ResponseMessage? Message { get; set; }
        [JsonPropertyName("done_reason")] public string? DoneReason { get; set; }
    }

    private sealed class ResponseMessage
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
        [JsonPropertyName("thinking")] public string? Thinking { get; set; }
    }

    private sealed class TagsResponse
    {
        [JsonPropertyName("models")] public List<TagModel>? Models { get; set; }
    }

    private sealed class TagModel
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}
