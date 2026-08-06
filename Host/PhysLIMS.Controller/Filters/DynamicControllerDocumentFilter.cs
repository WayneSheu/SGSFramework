using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using SGSFramework.ApiInfrastructure.Extensions;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.ModulePlugin.Systems.Controller.Repositories;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SGSFramework.ApiInfrastructure.Filters;

/// <summary>
///  OpenAPI 文件轉換器，用於根據動態控制器的元數據來調整 OpenAPI 文檔的結構和內容。
/// </summary>
public sealed class DynamicControllerDocumentTransformer : IOpenApiDocumentTransformer
{
    private readonly IServiceProvider _serviceProvider;
    private const string DefaultModuleName = "SGSFramework.System";
    private const string DefaultTagName = "通用服務";

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
                var metaDataList = await controllerRepo.GetActiveControllersAsync().ConfigureAwait(false);
                activeMeta = (metaDataList ?? Enumerable.Empty<ControllerMetadata>())
                    .Where(m => m.IsActive)
                    .ToList();
            }

            // 1. 建立雙軌檢索字典：全路徑字典 (Exact Route) 與 Action 字典 (Controller+Action)
            var exactRouteLookup = new Dictionary<string, ControllerMetadata>(StringComparer.OrdinalIgnoreCase);
            var actionLookup = new Dictionary<string, ControllerMetadata>(StringComparer.OrdinalIgnoreCase);

            foreach (var meta in activeMeta)
            {
                string cleanController = ExtractCleanControllerName(meta.ControllerName) ?? string.Empty;

                // A. 全路徑字典 (處理 [controller] 展開)
                if (!string.IsNullOrWhiteSpace(meta.RouteTemplate))
                {
                    string expandedRoute = meta.RouteTemplate.Replace("[controller]", cleanController, StringComparison.OrdinalIgnoreCase);
                    string normalizedRoute = NormalizePath(expandedRoute);
                    if (!string.IsNullOrWhiteSpace(normalizedRoute))
                    {
                        exactRouteLookup[normalizedRoute] = meta;
                    }
                }

                // B. Action 組合字典 (以 ControllerName.ActionName 作為備用精準匹配)
                if (!string.IsNullOrWhiteSpace(cleanController) && !string.IsNullOrWhiteSpace(meta.ActionName))
                {
                    string actionKey = $"{cleanController}.{meta.ActionName}".ToLowerInvariant();
                    actionLookup[actionKey] = meta;
                }
            }

            var moduleGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            // 2. 走訪 OpenAPI Paths，進行精準對映
            if (document.Paths != null)
            {
                foreach (var (pathKey, pathObj) in document.Paths)
                {
                    if (pathObj.Operations == null) continue;

                    string cleanPath = NormalizePath(pathKey);

                    foreach (var (httpMethod, operation) in pathObj.Operations)
                    {
                        ControllerMetadata? matchedMeta = null;

                        // 優先順序 1：精準 Route 匹配
                        if (exactRouteLookup.TryGetValue(cleanPath, out var exactMatch))
                        {
                            matchedMeta = exactMatch;
                        }

                        // 優先順序 2：若有 OperationId (格式通常為 Controller_Action)，嘗試以 Controller.Action 匹配
                        if (matchedMeta == null && !string.IsNullOrWhiteSpace(operation.OperationId))
                        {
                            string keyFromOpId = operation.OperationId.Replace("_", ".").ToLowerInvariant();
                            actionLookup.TryGetValue(keyFromOpId, out matchedMeta);
                        }

                        // 確定 Tag 與 Module 名稱
                        string targetModule = DefaultModuleName;
                        string targetTag = DefaultTagName;

                        if (matchedMeta != null)
                        {
                            targetModule = string.IsNullOrWhiteSpace(matchedMeta.ModuleName) ? DefaultModuleName : matchedMeta.ModuleName.Trim();

                            // 優先採用 ParentMenuName 做為左側階層 Tag
                            targetTag = !string.IsNullOrWhiteSpace(matchedMeta.ParentMenuName)
                                ? matchedMeta.ParentMenuName.Trim()
                                : ExtractCleanControllerName(matchedMeta.ControllerName) ?? DefaultTagName;

                            // 🎯 精準覆蓋 Endpoint 摘要名稱（修正所有子項目變成同一個名字的 BUG）
                            if (!string.IsNullOrWhiteSpace(matchedMeta.DisplayName))
                            {
                                operation.Summary = matchedMeta.DisplayName;
                            }
                        }
                        else
                        {
                            // 無法從 DB 找到對應 meta 時的預設處置
                            var firstTag = operation.Tags?.FirstOrDefault()?.Name ?? DefaultTagName;
                            targetTag = ExtractCleanControllerName(firstTag) ?? DefaultTagName;
                        }

                        // 更新 Operation 的 Tag
                        operation.Tags?.Clear();
                        operation.Tags?.Add(new OpenApiTagReference(targetTag));

                        // 歸控至模組分組
                        if (!moduleGroups.TryGetValue(targetModule, out var tagSet))
                        {
                            tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            moduleGroups[targetModule] = tagSet;
                        }
                        tagSet.Add(targetTag);
                    }
                }
            }

            // 3. 補齊選單樹中定義的所有 Tag
            foreach (var meta in activeMeta)
            {
                string moduleName = string.IsNullOrWhiteSpace(meta.ModuleName) ? DefaultModuleName : meta.ModuleName.Trim();
                string targetTag = !string.IsNullOrWhiteSpace(meta.ParentMenuName) ? meta.ParentMenuName.Trim() : DefaultTagName;

                if (!moduleGroups.TryGetValue(moduleName, out var tagSet))
                {
                    tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    moduleGroups[moduleName] = tagSet;
                }
                tagSet.Add(targetTag);
            }

            // 4. 重構全域 document.Tags 宣告
            document.Tags.Clear();
            foreach (var (moduleName, tagNames) in moduleGroups)
            {
                foreach (var tagName in tagNames)
                {
                    if (!document.Tags.Any(t => string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var meta = activeMeta.FirstOrDefault(m => string.Equals(m.ParentMenuName, tagName, StringComparison.OrdinalIgnoreCase));

                        document.Tags.Add(new OpenApiTag
                        {
                            Name = tagName,
                            Description = meta?.Description ?? $"{moduleName} - {tagName}"
                        });
                    }
                }
            }

            // 5. 寫入 Scalar 專用 x-tagGroups 擴充欄位
            var tagGroupsData = new List<Dictionary<string, object>>();
            foreach (var (moduleName, tagNames) in moduleGroups)
            {
                var validTags = tagNames.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
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

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        return path.Trim().TrimStart('/').ToLowerInvariant();
    }

    private static string? ExtractCleanControllerName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return null;
        var clean = rawName.Trim();

        // 處理完整 TypeName (如 SGSFramework.Identity.Controllers.UserManagementController)
        if (clean.Contains('.'))
        {
            clean = clean.Split('.').Last();
        }

        if (clean.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
        {
            return clean.Substring(0, clean.Length - 10);
        }
        return clean;
    }
}