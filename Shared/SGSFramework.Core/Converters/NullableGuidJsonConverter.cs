using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGSFramework.Core.Converters
{
    /// <summary>
    /// 處理 Nullable Guid 的客製化 JSON 反序列化器。
    /// 自動將空字串 ("")、純空白字串或無效格式轉譯為 null，防止 Model Binding 拋出 HTTP 400 異常。
    /// </summary>
    public sealed class NullableGuidJsonConverter : JsonConverter<Guid?>
    {
        public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string? stringValue = reader.GetString();
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    return null;
                }

                if (Guid.TryParse(stringValue, out var parsedGuid))
                {
                    return parsedGuid;
                }
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToString());
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
