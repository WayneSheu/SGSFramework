namespace SGSFramework.ApiInfrastructure.Transformers;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SGSFramework.ApiInfrastructure.Extensions;
using SGSFramework.Core.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Operation 層級轉換器。直接讀取 ModuleAttribute、ControllerTitleAttribute 與 FunctionAttribute 重構三層選單。
/// </summary>
public sealed class MenuAttributeTransformer : IOpenApiOperationTransformer
{
    private const string FallbackModuleName = "通用模組";
    private const string FallbackControllerName = "一般功能";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
            var cad = context.Description.ActionDescriptor as ControllerActionDescriptor;

            // 1. 第三層 (Action) -> 解析 [Function] 之 Title 或 FunctionName[cite: 8]
            var functionAttr = endpointMetadata.OfType<FunctionAttribute>().FirstOrDefault();
            string displayName = functionAttr?.Title ?? functionAttr?.FunctionName ?? cad?.ActionName ?? operation.Summary ?? "未命名操作";
            operation.Summary = displayName;

            // 2. 第二層 (Controller) -> 解析 [ControllerTitle] 之 Title[cite: 7]
            var controllerTitleAttr = endpointMetadata.OfType<ControllerTitleAttribute>().FirstOrDefault();
            string controllerTitle = controllerTitleAttr?.Title ?? ResolveCleanControllerName(cad?.ControllerName);

            operation.Tags ??= new HashSet<OpenApiTagReference>();
            operation.Tags.Clear();
            operation.Tags.Add(new OpenApiTagReference(controllerTitle));

            // 3. 第一層 (Section) -> 依序從 Method -> Class -> Assembly 尋找 [Module] 之 Title/ModuleName[cite: 6]
            var moduleAttr = endpointMetadata.OfType<ModuleAttribute>().FirstOrDefault()
                ?? cad?.ControllerTypeInfo.GetCustomAttribute<ModuleAttribute>()
                ?? cad?.ControllerTypeInfo.Assembly.GetCustomAttribute<ModuleAttribute>();

            string moduleTitle = moduleAttr?.Title ?? moduleAttr?.ModuleName ?? ResolveModuleByNamespace(cad?.ControllerTypeInfo.Namespace);

            // 將第一層資訊寫入 Operation Extension 供 DocumentFilter 彙整 x-tagGroups[cite: 6]
            operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            operation.Extensions["x-module-title"] = new OpenApiJsonNodeExtension(JsonValue.Create(moduleTitle));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("執行 MenuAttributeTransformer 轉譯時發生錯誤。", ex);
        }

        return Task.CompletedTask;
    }

    private static string ResolveCleanControllerName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return FallbackControllerName;
        return rawName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
            ? rawName[..^10]
            : rawName;
    }

    private static string ResolveModuleByNamespace(string? ns)
    {
        if (string.IsNullOrWhiteSpace(ns)) return FallbackModuleName;
        if (ns.Contains("Identity", StringComparison.OrdinalIgnoreCase) || ns.Contains("Auth", StringComparison.OrdinalIgnoreCase)) return "身分安全與權限";
        if (ns.Contains("System", StringComparison.OrdinalIgnoreCase) || ns.Contains("ApiInfrastructure", StringComparison.OrdinalIgnoreCase)) return "系統管理";
        return FallbackModuleName;
    }
}