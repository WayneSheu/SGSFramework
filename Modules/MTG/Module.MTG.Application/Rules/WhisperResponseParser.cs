using System.Globalization;
using System.Text.Json;

namespace SGS.Modules.MTG.Application.Rules;

/// <summary>轉錄服務回傳的一段文字；時間為相對音檔開頭的秒數（服務未提供時為 null）。</summary>
public sealed record TranscribedSegment(double? StartSeconds, double? EndSeconds, string Text);

/// <summary>轉錄結果。</summary>
public sealed record TranscriptionResult(IReadOnlyList<TranscribedSegment> Segments, string? Language, double? DurationSeconds);

/// <summary>
/// 解析 OpenAI 相容的 <c>verbose_json</c> 回應（純函式）：優先使用 segments（含起訖秒數）；
/// 服務只回 <c>text</c>（例如 response_format=json）時，依換行與句尾標點切成多段，沒有時間資訊。
/// 空白段落一律丟棄，起訖時間不合理（負數、結束早於開始）時視為未提供。
/// </summary>
public static class WhisperResponseParser
{
    private static readonly char[] SentenceEnders = ['。', '！', '？', '!', '?', '\n'];

    public static TranscriptionResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new FormatException("轉錄服務沒有回傳內容。");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException("轉錄服務回傳的不是有效的 JSON。", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new FormatException("轉錄服務回傳的格式不正確。");

            var language = ReadString(root, "language");
            var duration = ReadNumber(root, "duration");
            var segments = new List<TranscribedSegment>();

            if (root.TryGetProperty("segments", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var text = ReadString(item, "text")?.Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var start = ReadNumber(item, "start");
                    var end = ReadNumber(item, "end");
                    if (start is < 0 || end is < 0 || (start is { } s && end is { } e && e < s)) (start, end) = (null, null);
                    segments.Add(new TranscribedSegment(start, end, text));
                }
            }

            if (segments.Count == 0)
            {
                var text = ReadString(root, "text");
                if (!string.IsNullOrWhiteSpace(text)) segments.AddRange(SplitText(text).Select(t => new TranscribedSegment(null, null, t)));
            }

            return new TranscriptionResult(segments, language, duration);
        }
    }

    /// <summary>依換行與句尾標點切段（標點留在句尾）；過短的碎片併入前一段。</summary>
    public static IReadOnlyList<string> SplitText(string text)
    {
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();

        void Flush()
        {
            var s = sb.ToString().Trim();
            sb.Clear();
            if (s.Length == 0) return;
            if (result.Count > 0 && s.Length < 4 && s.All(c => !char.IsLetterOrDigit(c))) result[^1] += s; // 孤立的標點併回前一句
            else result.Add(s);
        }

        foreach (var ch in text.Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            if (ch == '\n') { Flush(); continue; }
            sb.Append(ch);
            if (Array.IndexOf(SentenceEnders, ch) >= 0) Flush();
        }
        Flush();
        return result;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static double? ReadNumber(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var p)) return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d) && double.IsFinite(d)) return d;
        if (p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && double.IsFinite(s)) return s;
        return null;
    }
}
