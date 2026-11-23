using System.Text.Json;
using System.Text.RegularExpressions;

namespace SGS.Modules.MTG.Application.Rules;

/// <summary>OpenAI 相容 <c>/chat/completions</c> 回應的解析結果。</summary>
/// <param name="Content">模型的文字輸出（已移除 &lt;think&gt; 思考區塊）。</param>
/// <param name="FinishReason">stop＝正常結束；length＝達到輸出上限被截斷。</param>
public sealed record ChatCompletionResult(string Content, string? FinishReason, int ReasoningTokens);

/// <summary>
/// 文字產生服務為 OpenAI 相容介面（LM Studio、vLLM、llama.cpp server…）時的純函式規則：組出請求、解析回應、
/// 移除推理模型的思考區塊。沒有 I/O，方便單元測試。
/// </summary>
public static class ChatCompletionRules
{
    /// <summary>Qwen3 系列的「不要思考」軟開關；放在使用者訊息尾端。其他模型請在設定關閉 DisableReasoning。</summary>
    public const string NoThinkSwitch = "/no_think";

    /// <summary>
    /// 移除推理模型內嵌在內容裡的 <c>&lt;think&gt;…&lt;/think&gt;</c>（不分大小寫、可跨行）；沒有結尾標籤（輸出被截斷）時，從 &lt;think&gt; 起全部丟棄。
    /// LM Studio 新版會把思考放在獨立的 <c>reasoning_content</c> 欄位，內容裡不會有標籤，這時此函式不改變內容。
    /// </summary>
    public static string StripReasoning(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var withoutClosed = Regex.Replace(text, @"<think>.*?</think>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var open = withoutClosed.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        return (open >= 0 ? withoutClosed[..open] : withoutClosed).Trim();
    }

    /// <summary>組出使用者訊息：需要關閉推理時在尾端附上 <see cref="NoThinkSwitch"/>。</summary>
    public static string BuildUserMessage(string prompt, bool disableReasoning) =>
        disableReasoning ? prompt.TrimEnd() + "\n" + NoThinkSwitch : prompt;

    /// <summary>
    /// 解析回應 JSON；格式不符（沒有 choices）回傳 null。
    /// 刻意不讀 <c>reasoning_content</c>：思考過程不是紀要內容，也可能含逐字稿片段，不該被保存或顯示。
    /// </summary>
    public static ChatCompletionResult? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                return null;

            var first = choices[0];
            var content = first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : null;
            var finish = first.TryGetProperty("finish_reason", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;

            var reasoning = 0;
            if (doc.RootElement.TryGetProperty("usage", out var usage) && usage.TryGetProperty("completion_tokens_details", out var details)
                && details.ValueKind == JsonValueKind.Object && details.TryGetProperty("reasoning_tokens", out var rt) && rt.TryGetInt32(out var n))
                reasoning = n;

            return new ChatCompletionResult(StripReasoning(content), finish, reasoning);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 組出 <c>/chat/completions</c> 的完整網址：設定的位址通常已含 <c>/v1</c>（LM Studio 的預設），沒有就補上。
    /// </summary>
    public static Uri BuildEndpoint(string baseUrl)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) trimmed += "/v1";
        return new Uri(trimmed + "/chat/completions");
    }
}
