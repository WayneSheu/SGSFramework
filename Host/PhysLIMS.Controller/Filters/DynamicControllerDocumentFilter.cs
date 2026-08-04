// Presentation / OpenAPI / DynamicControllerDocumentTransformer.cs
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using SGSFramework.ApiInfrastructure.Extensions;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.ModulePlugin.Systems.Controller.Repositories;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SGSFramework.ApiInfrastructure.Filters;

public class DynamicControllerDocumentTransformer : IOpenApiDocumentTransformer
{
    private readonly IServiceProvider _serviceProvider;
    private const string DefaultModuleName = "系統管理模組";

    public DynamicControllerDocumentTransformer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var controllerRepo = scope.ServiceProvider.GetService<IDynamicControllerRepository<ControllerMetadata>>();

            var activeMeta = new List<ControllerMetadata>();
            if (controllerRepo != null)
            {
                var metaDataList = await controllerRepo.GetActiveControllersAsync();
                activeMeta = (metaDataList ?? Enumerable.Empty<ControllerMetadata>())
                    .Where(m => m.IsActive)
                    .ToList();
            }

            // 1. 建立多元比對字典 (針對 ControllerName, RouteTemplate, DisplayName 做特化)
            var rawLookup = new Dictionary<string, (string TagName, string ModuleName)>(StringComparer.OrdinalIgnoreCase);

            foreach (var meta in activeMeta)
            {
                string moduleName = string.IsNullOrWhiteSpace(meta.ModuleName) ? DefaultModuleName : meta.ModuleName.Trim();
                string targetTag = !string.IsNullOrWhiteSpace(meta.ParentMenuName) ? meta.ParentMenuName.Trim()
                                 : !string.IsNullOrWhiteSpace(meta.DisplayName) ? meta.DisplayName.Trim()
                                 : meta.ControllerName?.Trim() ?? "通用服務";

                if (!string.IsNullOrWhiteSpace(meta.ControllerName))
                {
                    string rawCName = meta.ControllerName.Trim();
                    rawLookup[rawCName] = (targetTag, moduleName);

                    if (rawCName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                    {
                        rawLookup[rawCName.Substring(0, rawCName.Length - 10)] = (targetTag, moduleName);
                    }
                }

                if (!string.IsNullOrWhiteSpace(meta.RouteTemplate))
                {
                    string cleanRoute = meta.RouteTemplate.Trim().TrimStart('/');
                    rawLookup[cleanRoute] = (targetTag, moduleName);
                }
            }

            var sortedLookup = rawLookup.OrderByDescending(kv => kv.Key.Length).ToList();

            // 2. 走訪所有 API Operations，綁定正確 Tag
            var moduleGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            if (document.Paths != null)
            {
                foreach (var (pathKey, pathObj) in document.Paths)
                {
                    if (pathObj.Operations == null) continue;

                    string cleanPath = pathKey.Trim().TrimStart('/');

                    foreach (var operation in pathObj.Operations.Values)
                    {
                        string? targetTag = null;
                        string? targetModule = null;

                        // A. 比對 DB 詮釋資料
                        foreach (var kvp in sortedLookup)
                        {
                            if (cleanPath.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                targetTag = kvp.Value.TagName;
                                targetModule = kvp.Value.ModuleName;
                                break;
                            }
                        }

                        // B. 硬性兜底 (針對 SGS.Modules.ORG 實驗室模組特化比對)
                        if (targetTag == null && (cleanPath.Contains("laboratory", StringComparison.OrdinalIgnoreCase) || cleanPath.Contains("org", StringComparison.OrdinalIgnoreCase)))
                        {
                            targetTag = "實驗室管理";
                            targetModule = "SGS.Modules.ORG";
                        }

                        // C. 若都沒匹配到，沿用預設控制項 Tag
                        if (string.IsNullOrWhiteSpace(targetTag))
                        {
                            var firstTag = operation.Tags?.FirstOrDefault()?.Name ?? "通用服務";
                            targetTag = firstTag.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
                                ? firstTag.Substring(0, firstTag.Length - 10)
                                : firstTag;
                        }

                        if (string.IsNullOrWhiteSpace(targetModule)) targetModule = DefaultModuleName;

                        // 正確寫入 OpenApiTagReference
                        operation.Tags?.Clear();
                        operation.Tags?.Add(new OpenApiTagReference(targetTag));

                        // 收集模組與 Tag 的樹狀關係
                        if (!moduleGroups.TryGetValue(targetModule, out var tagSet))
                        {
                            tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            moduleGroups[targetModule] = tagSet;
                        }
                        tagSet.Add(targetTag);
                    }
                }
            }

            // 3. 補齊 DB 主選單中的所有外掛模組與 Tag 標籤關係（避免即使 Operations 尚未繫結也能正確列出）
            foreach (var meta in activeMeta)
            {
                string moduleName = string.IsNullOrWhiteSpace(meta.ModuleName) ? DefaultModuleName : meta.ModuleName.Trim();
                string? targetTag = !string.IsNullOrWhiteSpace(meta.ParentMenuName) ? meta.ParentMenuName.Trim()
                                  : !string.IsNullOrWhiteSpace(meta.DisplayName) ? meta.DisplayName.Trim()
                                  : meta.ControllerName?.Trim();

                if (string.IsNullOrWhiteSpace(moduleName) || string.IsNullOrWhiteSpace(targetTag)) continue;

                if (!moduleGroups.TryGetValue(moduleName, out var tagSet))
                {
                    tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    moduleGroups[moduleName] = tagSet;
                }
                tagSet.Add(targetTag);
            }

            // 4. 重構 document.Tags 全域宣告，確保所有 Tag 均有實體 OpenApiTag
            document.Tags.Clear();

            foreach (var (moduleName, tagNames) in moduleGroups)
            {
                foreach (var tagName in tagNames)
                {
                    if (!document.Tags.Any(t => string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var meta = activeMeta.FirstOrDefault(m => string.Equals(m.ParentMenuName, tagName, StringComparison.OrdinalIgnoreCase) ||
                                                                  string.Equals(m.DisplayName, tagName, StringComparison.OrdinalIgnoreCase));

                        document.Tags.Add(new OpenApiTag
                        {
                            Name = tagName,
                            Description = meta?.Description ?? $"{moduleName} - {tagName}"
                        });
                    }
                }
            }

            // 5. 輸出符合 Scalar 規範的 x-tagGroups 結構
            var tagGroupsData = new List<Dictionary<string, object>>();

            foreach (var (moduleName, tagNames) in moduleGroups)
            {
                var validTags = tagNames
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                if (!validTags.Any()) continue;

                tagGroupsData.Add(new Dictionary<string, object>
                {
                    ["name"] = moduleName,
                    ["tags"] = validTags
                });
            }

            var jsonArray = JsonSerializer.SerializeToNode(tagGroupsData) as JsonArray;
            if (jsonArray != null)
            {
                document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                var jsonExtension = new OpenApiJsonNodeExtension(jsonArray);

                document.Extensions["x-tagGroups"] = jsonExtension;
                document.Extensions["x-tag-groups"] = jsonExtension;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenAPI Transformer Error]: {ex.Message}");
        }
    }
}