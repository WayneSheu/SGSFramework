namespace SGSFramework.ApiInfrastructure.Filters;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SGSFramework.ApiInfrastructure.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// OpenAPI 全域文件轉換器，彙整 x-module-title 與 Controller Tags 建構 Scalar UI 之 x-tagGroups 樹狀結構。
/// </summary>
public sealed class DynamicControllerDocumentFilter : IOpenApiDocumentTransformer
{
    private const string DefaultModuleName = "通用模組";
    private const string DefaultControllerName = "一般功能";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            document.Tags ??= new HashSet<OpenApiTag>();

            if (document.Paths == null || document.Paths.Count == 0)
            {
                return Task.CompletedTask;
            }

            var moduleGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var pathItem in document.Paths.Values)
            {
                if (pathItem?.Operations == null) continue;

                foreach (var operation in pathItem.Operations.Values)
                {
                    if (operation == null) continue;

                    // 取得第二層 Controller Title
                    var controllerTitle = operation.Tags?.FirstOrDefault()?.Name ?? DefaultControllerName;

                    if (!document.Tags.Any(t => string.Equals(t.Name, controllerTitle, StringComparison.Ordinal)))
                    {
                        document.Tags.Add(new OpenApiTag
                        {
                            Name = controllerTitle,
                            Description = $"{controllerTitle} 相關 API 接口"
                        });
                    }

                    // 取得第一層 Section Title
                    string moduleTitle = DefaultModuleName;
                    if (operation.Extensions != null &&
                        operation.Extensions.TryGetValue("x-module-title", out var ext) &&
                        ext is OpenApiJsonNodeExtension jsonExt)
                    {
                        var rawValue = jsonExt.Node?.ToString()?.Trim('"');
                        if (!string.IsNullOrWhiteSpace(rawValue))
                        {
                            moduleTitle = rawValue;
                        }
                    }

                    if (!moduleGroups.TryGetValue(moduleTitle, out var tagSet))
                    {
                        tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        moduleGroups[moduleTitle] = tagSet;
                    }
                    tagSet.Add(controllerTitle);
                }
            }

            BuildScalarTagGroups(document, moduleGroups);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("建構 API 階層結構文件時發生錯誤。", ex);
        }

        return Task.CompletedTask;
    }

    private static void BuildScalarTagGroups(OpenApiDocument document, Dictionary<string, HashSet<string>> moduleGroups)
    {
        if (moduleGroups.Count == 0) return;

        var tagGroupsArray = new JsonArray();

        foreach (var (moduleName, tags) in moduleGroups)
        {
            var tagList = new JsonArray();
            foreach (var tag in tags)
            {
                tagList.Add(JsonValue.Create(tag));
            }

            var groupObject = new JsonObject
            {
                ["name"] = moduleName,
                ["tags"] = tagList
            };

            tagGroupsArray.Add(groupObject);
        }

        document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        document.Extensions["x-tagGroups"] = new OpenApiJsonNodeExtension(tagGroupsArray);
    }
}