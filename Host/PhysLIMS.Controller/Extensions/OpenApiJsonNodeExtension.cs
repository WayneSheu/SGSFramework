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

        /// <summary>
        /// 遞迴將 JsonNode 寫入 OpenApiWriter，正確處理 JsonArray、JsonObject 與 JsonValue
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="node"></param>
        private static void WriteJsonNode(IOpenApiWriter writer, JsonNode? node)
        {
            // 如果 node 為 null，則寫入 null
            if (node == null)
            {
                writer.WriteNull();
                return;
            }
            // 遞迴處理 JsonNode 的不同類型
            if (node is JsonArray array)
            {
                // 如果是 JsonArray，則寫入陣列開始標記，遞迴寫入每個元素，最後寫入陣列結束標記
                writer.WriteStartArray();
                foreach (var item in array)
                {
                    // 遞迴寫入每個元素
                    WriteJsonNode(writer, item);
                }
                // 寫入陣列結束標記
                writer.WriteEndArray();
            }
            // 如果是 JsonObject，則寫入物件開始標記，遞迴寫入每個屬性名稱與值，最後寫入物件結束標記
            else if (node is JsonObject obj)
            {
                // 寫入物件開始標記
                writer.WriteStartObject();
                foreach (var property in obj)
                {
                    // 寫入屬性名稱
                    writer.WritePropertyName(property.Key);
                    // 遞迴寫入屬性值
                    WriteJsonNode(writer, property.Value);
                }
                // 寫入物件結束標記
                writer.WriteEndObject();
            }
            // 如果是 JsonValue，則嘗試將其轉換為原生型別，並寫入對應的值
            else if (node is JsonValue value)
            {
                // 嘗試將 JsonValue 轉換為原生型別，並寫入對應的值
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
                // 如果是其他未知類型，則寫入 null
                writer.WriteNull();
            }
        }
    }
}