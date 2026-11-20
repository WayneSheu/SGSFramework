using System.Text;
using System.Text.Json;

namespace SGS.Modules.MAN.Application.Rules;

/// <summary>
/// 依功能關鍵字自 OpenAPI 文件挑出相關的路徑與資料模型，並截斷長度（純函式）。
/// 本地 7B/8B 模型的 Context 有限，切勿整份規格塞入；寧可少而準。
/// </summary>
public static class OpenApiContextFilter
{
    public const int DefaultMaxChars = 3500;

    /// <summary>找不到關鍵字或文件無法解析時回傳空字串（呼叫端改為僅依截圖生成）。</summary>
    public static string Build(string? openApiJson, string? keyword, int maxChars = DefaultMaxChars)
    {
        if (string.IsNullOrWhiteSpace(openApiJson) || string.IsNullOrWhiteSpace(keyword)) return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(openApiJson);
            var kw = keyword.Trim();
            var sb = new StringBuilder();

            if (doc.RootElement.TryGetProperty("paths", out var paths) && paths.ValueKind == JsonValueKind.Object)
            {
                foreach (var path in paths.EnumerateObject())
                {
                    if (!path.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)) continue;
                    sb.AppendLine($"API 路徑：{path.Name}").AppendLine(path.Value.GetRawText()).AppendLine("---");
                    if (sb.Length >= maxChars) break;
                }
            }

            if (sb.Length < maxChars
                && doc.RootElement.TryGetProperty("components", out var components)
                && components.TryGetProperty("schemas", out var schemas)
                && schemas.ValueKind == JsonValueKind.Object)
            {
                foreach (var schema in schemas.EnumerateObject())
                {
                    if (!schema.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)) continue;
                    sb.AppendLine($"資料模型：{schema.Name}").AppendLine(schema.Value.GetRawText()).AppendLine("---");
                    if (sb.Length >= maxChars) break;
                }
            }

            return sb.Length <= maxChars ? sb.ToString().Trim() : sb.ToString(0, maxChars).Trim();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}
