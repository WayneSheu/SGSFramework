using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace SGSFramework.Scheduler.Http;

/// <summary>內部 API 呼叫失敗（非 2xx）：設定、權限等可預期錯誤（4xx），重試無法改善，作業直接判定失敗並告警。</summary>
public class ScheduledJobException(string message, int? statusCode = null) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}

/// <summary>
/// 暫時性失敗（5xx、408、429）：作業以 [AutomaticRetry(OnlyOn = ...)] 只對此類例外與網路錯誤、逾時重試。
/// </summary>
public sealed class TransientScheduledJobException(string message, int? statusCode = null) : ScheduledJobException(message, statusCode);

/// <summary>
/// 排程作業呼叫內部 API 的用戶端。底層具名 HttpClient 由 AddSystemAccountHttpClient 註冊：
/// 自動以系統帳號換發 / 快取權杖、注入 X-Correlation-ID，並對 5xx / 網路錯誤指數退讓重試。
/// </summary>
public sealed class SchedulerApiClient(IHttpClientFactory httpClientFactory)
{
    public const string HttpClientName = "SGSFramework.Scheduler.InternalApi";

    /// <summary>系統帳號設定名稱（ISystemAccountTokenService 依此快取權杖）</summary>
    public const string AccountName = "Scheduler";

    public async Task<TResponse> PostAsync<TResponse>(string path, object? body, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // HttpClient.Timeout 設為無限，改由每次呼叫依作業性質（例如備份可能數十分鐘）決定逾時
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsJsonAsync(path, body, timeoutCts.Token).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadProblemDetailAsync(response, timeoutCts.Token).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            throw status >= 500 || status is 408 or 429
                ? new TransientScheduledJobException($"內部 API 暫時無法完成（HTTP {status}）：{detail}", status)
                : new ScheduledJobException(detail, status);
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(timeoutCts.Token).ConfigureAwait(false)
            ?? throw new ScheduledJobException($"內部 API {path} 回應內容為空。");
    }

    private static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken).ConfigureAwait(false);
            return problem?.Detail ?? problem?.Title ?? "(無說明)";
        }
        catch (JsonException)
        {
            return "(無法解析錯誤內容)";
        }
        catch (NotSupportedException)
        {
            return "(非 JSON 錯誤內容)";
        }
    }
}
