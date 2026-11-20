using System.Text;
using System.Text.Json;

namespace SGS.Modules.MAN.Application.Rules;

/// <summary>AI 生成的手冊內容。</summary>
public sealed record GeneratedManual(string Tooltip, string StepsMarkdown, string Precautions);

/// <summary>解析模型輸出（純函式）：容忍 Markdown 程式碼區塊、前後說明文字，以及陣列形式的步驟。</summary>
public static class ManualOutputParser
{
    /// <summary>解析成功回傳內容；失敗回傳 null 並以 <paramref name="error"/> 說明（不含模型原文）。</summary>
    public static GeneratedManual? TryParse(string? raw, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "模型沒有回傳內容。";
            return null;
        }

        var json = ExtractJsonObject(raw);
        if (json is null)
        {
            error = "模型回傳的內容不是有效的 JSON。";
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "模型回傳的 JSON 不是物件。";
                return null;
            }

            var root = doc.RootElement;
            var steps = ReadText(root, "steps_markdown", "stepsMarkdown", "steps");
            if (string.IsNullOrWhiteSpace(steps))
            {
                error = "模型沒有產生操作步驟。";
                return null;
            }

            var tooltip = ReadText(root, "tooltip");
            if (string.IsNullOrWhiteSpace(tooltip))
            {
                error = "模型沒有產生內嵌提示。";
                return null;
            }

            var precautions = ReadText(root, "precautions", "notes");
            return new GeneratedManual(
                TruncateTooltip(tooltip),
                steps.Trim(),
                string.IsNullOrWhiteSpace(precautions) ? "無" : precautions.Trim());
        }
        catch (JsonException)
        {
            error = "模型回傳的內容不是有效的 JSON。";
            return null;
        }
    }

    /// <summary>內嵌提示上限 30 字；超過時截斷並加上刪節號（總長仍不超過上限）。</summary>
    public static string TruncateTooltip(string tooltip, int max = ManualTopicRules.MaxTooltipLength)
    {
        var text = tooltip.Trim().ReplaceLineEndings(" ");
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }

    private static string? ExtractJsonObject(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
    }

    private static string? ReadText(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value)) continue;

            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return value.GetString();

                case JsonValueKind.Array:
                    var sb = new StringBuilder();
                    var index = 1;
                    foreach (var item in value.EnumerateArray())
                    {
                        var line = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // 模型已自行編號時不重複加號
                        var trimmed = line.Trim();
                        sb.AppendLine(char.IsDigit(trimmed[0]) ? trimmed : $"{index}. {trimmed}");
                        index++;
                    }

                    return sb.ToString();
            }
        }

        return null;
    }
}
