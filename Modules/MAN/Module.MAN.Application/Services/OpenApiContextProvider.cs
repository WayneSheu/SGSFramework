using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.MAN.Application.Options;
using SGS.Modules.MAN.Application.Rules;

namespace SGS.Modules.MAN.Application.Services;

/// <summary>自設定的 OpenAPI 文件取得與功能相關的規格上下文；任何失敗都降級為空字串（僅依截圖生成）。</summary>
public interface IOpenApiContextProvider
{
    Task<string> GetContextAsync(string? keyword, CancellationToken cancellationToken);
}

public sealed class OpenApiContextProvider(IHttpClientFactory httpClientFactory, IOptionsMonitor<ManOptions> options, ILogger<OpenApiContextProvider> logger)
    : IOpenApiContextProvider
{
    public const string HttpClientName = "man-openapi";

    public async Task<string> GetContextAsync(string? keyword, CancellationToken cancellationToken)
    {
        var url = options.CurrentValue.OpenApiUrl;
        if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(url)) return string.Empty;

        // 規格內容同樣屬於內部資料：位址須為本機或內網，與 Ollama 相同的限制
        if (!GenerationRules.IsAllowedOllamaUrl(url))
        {
            logger.LogWarning("[MAN] OpenApiUrl 不是本機或內網位址，已略過規格上下文。");
            return string.Empty;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            var client = httpClientFactory.CreateClient(HttpClientName);
            var json = await client.GetStringAsync(url, cts.Token).ConfigureAwait(false);
            return OpenApiContextFilter.Build(json, keyword);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("[MAN] 取得 OpenAPI 規格逾時，僅依截圖生成。");
            return string.Empty;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            logger.LogWarning(ex, "[MAN] 取得 OpenAPI 規格失敗，僅依截圖生成。");
            return string.Empty;
        }
    }
}
