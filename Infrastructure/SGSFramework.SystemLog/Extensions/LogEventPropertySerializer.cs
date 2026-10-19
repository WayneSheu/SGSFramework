#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Serilog.Events;

namespace SGSFramework.SystemLog.Extensions
{
    /// <summary>
    /// 將 Serilog LogEvent.Properties 序列化為可讀 JSON。
    /// 直接 JsonSerializer.Serialize(Properties) 會因 LogEventPropertyValue 為抽象型別而全部輸出 {}。
    /// </summary>
    public static class LogEventPropertySerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            // 保留中文字元，避免 \uXXXX 跳脫導致資料庫內容不可讀
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string Serialize(IReadOnlyDictionary<string, LogEventPropertyValue> properties)
        {
            ArgumentNullException.ThrowIfNull(properties);
            if (properties.Count == 0)
            {
                return string.Empty;
            }

            var plain = properties.ToDictionary(p => p.Key, p => ToPlain(p.Value));
            return JsonSerializer.Serialize(plain, Options);
        }

        private static object? ToPlain(LogEventPropertyValue value) => value switch
        {
            ScalarValue scalar => ToPlainScalar(scalar.Value),
            SequenceValue sequence => sequence.Elements.Select(ToPlain).ToList(),
            StructureValue structure => structure.Properties.ToDictionary(p => p.Name, p => ToPlain(p.Value)),
            DictionaryValue dictionary => dictionary.Elements.ToDictionary(
                e => e.Key.Value?.ToString() ?? string.Empty,
                e => ToPlain(e.Value)),
            _ => value.ToString()
        };

        // 僅保留 JSON 原生可表達的型別，其餘以字串呈現，確保序列化不會因未知型別失敗
        private static object? ToPlainScalar(object? value) => value switch
        {
            null => null,
            string or bool or byte or sbyte or short or ushort or int or uint or long or ulong
                or float or double or decimal or DateTime or DateTimeOffset or Guid => value,
            TimeSpan ts => ts.ToString(),
            _ => value.ToString()
        };
    }
}
