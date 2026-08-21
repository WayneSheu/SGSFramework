using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SGSFramework.Core.Abstractions.Attributes;
using System.Reflection;
using System.Text.Json.Nodes;

namespace SGSFramework.ApiInfrastructure.Extensions;

/// <summary>
/// 結合 [MenuAttribute] 與 OpenApiJsonNodeExtension 的 OpenApi 轉譯處理器
/// </summary>
public class MenuAttributeTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Description.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
        {
            return Task.CompletedTask;
        }

        // 1. 讀取 Action 與 Controller 的 [Menu] 屬性
        var actionMenuAttr = actionDescriptor.MethodInfo.GetCustomAttribute<MenuAttribute>();
        var controllerMenuAttr = actionDescriptor.ControllerTypeInfo.GetCustomAttribute<MenuAttribute>();

        // 2. 處理 Controller 層級 (對應 OpenAPI Tag / Scalar 大分類)
        if (controllerMenuAttr != null && controllerMenuAttr.IsVisible)
        {
            string tagName = !string.IsNullOrWhiteSpace(controllerMenuAttr.Parent)
                ? $"{controllerMenuAttr.Parent} > {controllerMenuAttr.Name}"
                : controllerMenuAttr.Name;

            if (string.IsNullOrWhiteSpace(tagName))
            {
                tagName = actionDescriptor.ControllerName;
            }

            // 關鍵修正 1：使用 HashSet<OpenApiTagReference> 初始化 Tags，相容 .NET 10 OpenAPI 引擎
            operation.Tags ??= new HashSet<OpenApiTagReference>();
            operation.Tags.Clear();

            // 正確寫入 TagReference
            operation.Tags.Add(new OpenApiTagReference(tagName));
        }

        // 3. 處理 Action 層級 (對應 OpenAPI Operation Summary)
        if (actionMenuAttr != null && actionMenuAttr.IsVisible)
        {
            operation.Summary = actionMenuAttr.Name;

            // 4. 寫入客製化 x-menu 擴充屬性至 OpenAPI Spec
            var menuJsonNode = new JsonObject
            {
                ["name"] = actionMenuAttr.Name,
                ["icon"] = actionMenuAttr.Icon ?? "fa-solid fa-link",
                ["order"] = actionMenuAttr.Order,
                ["parent"] = actionMenuAttr.Parent ?? controllerMenuAttr?.Name,
                ["isVisible"] = actionMenuAttr.IsVisible
            };

            // 關鍵修正 2：初始化 Extensions 字典，防止存取時報出 NullReferenceException
            operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            operation.Extensions["x-menu"] = new OpenApiJsonNodeExtension(menuJsonNode);
        }

        return Task.CompletedTask;
    }
}