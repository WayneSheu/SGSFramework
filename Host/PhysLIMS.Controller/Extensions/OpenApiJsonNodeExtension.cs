namespace SGSFramework.ApiInfrastructure.Extensions;

using Microsoft.OpenApi;
using System;
using System.Text.Json.Nodes;

/// <summary>
/// OpenAPI JsonNode 擴充節點，提供強型別 JSON 結構輸出與節點讀取。
/// </summary>
public sealed class OpenApiJsonNodeExtension : IOpenApiExtension
{
    public JsonNode Node { get; }

    public OpenApiJsonNodeExtension(JsonNode node)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
    {
        ArgumentNullException.ThrowIfNull(writer);
        WriteJsonNode(writer, Node);
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
                if (string.IsNullOrWhiteSpace(property.Key)) continue;
                writer.WritePropertyName(property.Key);
                WriteJsonNode(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var strVal))
            {
                writer.WriteValue(strVal);
            }
            else if (value.TryGetValue<bool>(out var boolVal))
            {
                writer.WriteValue(boolVal);
            }
            else if (value.TryGetValue<int>(out var intVal))
            {
                writer.WriteValue(intVal);
            }
            else if (value.TryGetValue<long>(out var longVal))
            {
                writer.WriteValue(longVal);
            }
            else if (value.TryGetValue<double>(out var doubleVal))
            {
                writer.WriteValue(doubleVal);
            }
            else
            {
                writer.WriteValue(value.ToString());
            }
        }
        else
        {
            writer.WriteNull();
        }
    }
}