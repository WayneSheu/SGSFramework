using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace SGSFramework.ApiInfrastructure.Extensions
{
    /// <summary>
    /// 自訂 OpenApi 擴充屬性包裝器，負責將 JsonNode 正確序列化為原生 JSON Array / Object 寫入 OpenAPI Stream
    /// </summary>
    public class OpenApiJsonNodeExtension : IOpenApiExtension
    {
        private readonly JsonNode _node;

        public OpenApiJsonNodeExtension(JsonNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
        {
            ArgumentNullException.ThrowIfNull(writer);
            WriteJsonNode(writer, _node);
        }

        private static void WriteJsonNode(IOpenApiWriter writer, JsonNode? node)
        {
            if (node == null)
            {
                writer.WriteNull();
                return;
            }

            if (node is JsonArray array)
            {
                writer.WriteStartArray();
                foreach (var item in array)
                {
                    WriteJsonNode(writer, item);
                }
                writer.WriteEndArray();
            }
            else if (node is JsonObject obj)
            {
                writer.WriteStartObject();
                foreach (var property in obj)
                {
                    writer.WritePropertyName(property.Key);
                    WriteJsonNode(writer, property.Value);
                }
                writer.WriteEndObject();
            }
            else if (node is JsonValue value)
            {
                if (value.TryGetValue<string>(out var strVal))
                    writer.WriteValue(strVal);
                else if (value.TryGetValue<bool>(out var boolVal))
                    writer.WriteValue(boolVal);
                else if (value.TryGetValue<long>(out var longVal))
                    writer.WriteValue(longVal);
                else if (value.TryGetValue<double>(out var doubleVal))
                    writer.WriteValue(doubleVal);
                else
                    writer.WriteValue(value.ToString());
            }
            else
            {
                writer.WriteNull();
            }
        }
    }
}