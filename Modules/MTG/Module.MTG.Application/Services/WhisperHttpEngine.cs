using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.MTG.Application.Options;
using SGS.Modules.MTG.Application.Rules;

namespace SGS.Modules.MTG.Application.Services;

/// <summary>語音轉錄引擎：把音檔內容轉成帶時間的文字片段。</summary>
public interface ITranscriptionEngine
{
    /// <summary>轉錄音檔。<paramref name="prompt"/> 是由術語表產生的提示詞，用來引導中英夾雜與專有名詞的辨識。</summary>
    Task<TranscriptionResult> TranscribeAsync(Stream audio, string fileName, string? prompt, CancellationToken cancellationToken);
}

/// <summary>
/// 呼叫本機／內網的 OpenAI 相容轉錄服務（<c>POST {BaseUrl}{Path}</c>，multipart：file、model、language、response_format=verbose_json、prompt）。
/// 位址每次呼叫前都再驗證，只允許本機或內網；錯誤訊息不含音檔或逐字稿內容。
/// </summary>
public sealed class WhisperHttpEngine(IHttpClientFactory httpClientFactory, IOptionsMonitor<MtgOptions> options, ILogger<WhisperHttpEngine> logger) : ITranscriptionEngine
{
    public const string HttpClientName = "mtg-whisper";

    public async Task<TranscriptionResult> TranscribeAsync(Stream audio, string fileName, string? prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);
        var opt = options.CurrentValue.Whisper;
        if (!InternalUrlRules.IsAllowed(opt.BaseUrl))
            throw new InvalidOperationException("轉錄服務位址必須是本機或內網位址。");

        using var form = new MultipartFormDataContent();
        // 欄位須在檔案之前；整檔以已知長度的串流送出（部分轉錄服務不接受未知長度的 chunked 上傳）
        form.Add(new StringContent(opt.Model), "model");
        form.Add(new StringContent("verbose_json"), "response_format");
        form.Add(new StringContent("0"), "temperature");
        if (!string.IsNullOrWhiteSpace(opt.Language)) form.Add(new StringContent(opt.Language.Trim()), "language");
        if (!string.IsNullOrWhiteSpace(prompt)) form.Add(new StringContent(prompt), "prompt");

        var file = new StreamContent(audio);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", string.IsNullOrWhiteSpace(fileName) ? "audio" : Path.GetFileName(fileName));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(Math.Max(1, opt.TimeoutMinutes)));

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var uri = new Uri(new Uri(opt.BaseUrl.TrimEnd('/') + "/"), opt.Path.TrimStart('/'));
            using var response = await client.PostAsync(uri, form, cts.Token).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // 回應內容可能含音檔細節或服務內部資訊：只記錄狀態碼
                logger.LogWarning("[MTG] 轉錄服務回應 HTTP {Status}。", (int)response.StatusCode);
                throw new InvalidOperationException(response.StatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound => "轉錄服務找不到指定的端點或模型，請檢查 Meeting:Whisper 設定。",
                    System.Net.HttpStatusCode.UnsupportedMediaType or System.Net.HttpStatusCode.BadRequest => "轉錄服務無法處理這個音檔，請確認格式（建議 16 kHz 單聲道 wav）。",
                    System.Net.HttpStatusCode.RequestEntityTooLarge => "音檔超過轉錄服務可接受的大小。",
                    _ => $"轉錄服務回應 HTTP {(int)response.StatusCode}。"
                });
            }

            try
            {
                return WhisperResponseParser.Parse(body);
            }
            catch (FormatException ex)
            {
                logger.LogWarning(ex, "[MTG] 無法解析轉錄服務的回應。");
                throw new InvalidOperationException("轉錄服務回傳的格式無法解析，請確認它支援 verbose_json。");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"轉錄逾時（超過 {opt.TimeoutMinutes} 分鐘）。");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "[MTG] 無法連線轉錄服務。");
            throw new InvalidOperationException("無法連線轉錄服務，請確認服務已啟動。");
        }
    }
}
