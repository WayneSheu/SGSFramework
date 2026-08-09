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

            // 1.1 走訪所有有效的 ControllerMetadata，建立檢索字典
            foreach (var meta in activeMeta)
            {
                // 1.2 清理 Controller 名稱 (去除命名空間與 Controller 後綴)
                string cleanController = ExtractCleanControllerName(meta.ControllerName) ?? string.Empty;

                // A. 全路徑字典 (處理 [controller] 展開)
                if (!string.IsNullOrWhiteSpace(meta.RouteTemplate))
                {
                    // 展開 [controller] 佔位符，並進行路徑正規化
                    string expandedRoute = meta.RouteTemplate.Replace("[controller]", cleanController, StringComparison.OrdinalIgnoreCase);
                    // 將路徑轉為小寫並去除前後斜線
                    string normalizedRoute = NormalizePath(expandedRoute);
                    // 將正規化後的路徑與對應的 ControllerMetadata 存入字典
                    if (!string.IsNullOrWhiteSpace(normalizedRoute))
                    {
                        exactRouteLookup[normalizedRoute] = meta;
                    }
                }

                // B. Action 組合字典 (以 ControllerName.ActionName 作為備用精準匹配)
                if (!string.IsNullOrWhiteSpace(cleanController) && !string.IsNullOrWhiteSpace(meta.ActionName))
                {
                    // 將 Controller 與 Action 名稱組合成唯一鍵，並轉為小寫
                    string actionKey = $"{cleanController}.{meta.ActionName}".ToLowerInvariant();
                    // 將組合鍵與對應的 ControllerMetadata 存入字典
                    actionLookup[actionKey] = meta;
                }
            }
            // 1.3 建立模組分組字典，用於後續的 Tag 分組
            var moduleGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            // 2. 走訪 OpenAPI Paths，進行精準對映
            if (document.Paths != null)
            {
                // 2.1 走訪每個路徑與其對應的操作
                foreach (var (pathKey, pathObj) in document.Paths)
                {
                    if (pathObj.Operations == null) continue;
                    // 將路徑正規化，去除前後斜線並轉為小寫
                    string cleanPath = NormalizePath(pathKey);
                    // 2.2 走訪每個 HTTP 方法與其對應的操作
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

                            //精準覆蓋 Endpoint 摘要名稱（修正所有子項目變成同一個名字的 BUG）
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
            // 5.1 將模組分組資料轉換為 JSON 可序列化的結構
            var tagGroupsData = new List<Dictionary<string, object>>();
            // 5.2 走訪模組分組，建立每個模組對應的 Tag 清單
            foreach (var (moduleName, tagNames) in moduleGroups)
            {
                // 過濾掉空白或無效的 Tag 名稱
                var validTags = tagNames.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
                if (!validTags.Any()) continue;
                // 將模組名稱與對應的 Tag 清單加入到 tagGroupsData 中
                tagGroupsData.Add(new Dictionary<string, object>
                {
                    ["name"] = moduleName,
                    ["tags"] = validTags
                });
            }
            // 5.3 將 tagGroupsData 序列化為 JsonArray，並加入到 OpenAPI 文件的 Extensions 中
            var jsonArray = JsonSerializer.SerializeToNode(tagGroupsData) as JsonArray;
            if (jsonArray != null)
            {
                // 確保 Extensions 字典已初始化
                document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                // 使用自定義的 OpenApiJsonNodeExtension 將 JsonArray 包裝為 IOpenApiExtension
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

    /// <summary>
    /// 將路徑字串正規化，去除前後斜線並轉為小寫。
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        return path.Trim().TrimStart('/').ToLowerInvariant();
    }

    /// <summary>
    /// 提取乾淨的 Controller 名稱，去除命名空間與 "Controller" 後綴。
    /// </summary>
    /// <param name="rawName"></param>
    /// <returns></returns>
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