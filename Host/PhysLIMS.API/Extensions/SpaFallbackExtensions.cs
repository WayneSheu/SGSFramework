// Path: src/SGSFramework/Host/PhysLIMS.API/Extensions/SpaFallbackExtensions.cs
#nullable enable

namespace PhysLIMS.API.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

public static class SpaFallbackExtensions
{
    // 排除 api, scalar, openapi, blazor 後端路徑與具副檔名之靜態檔案
    private const string VueSpaRouteRegex = @"^(?!(api|scalar|openapi|blazor)(/|$))[^.]*$";

    public static IEndpointConventionBuilder MapProtectedSpaFallback(
        this IEndpointRouteBuilder endpoints,
        IWebHostEnvironment environment,
        string fallbackFile = "index.html")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(environment);

        string webRoot = GetValidatedWebRootPath(environment);
        string indexPath = Path.Combine(webRoot, fallbackFile);

        if (!File.Exists(indexPath))
        {
            return new NullEndpointConventionBuilder();
        }

        return endpoints.MapFallbackToFile($"{{*path:regex({VueSpaRouteRegex})}}", fallbackFile);
    }

    public static IEndpointConventionBuilder MapBlazorSpaFallback(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var environment = endpoints.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var logger = endpoints.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger(nameof(SpaFallbackExtensions));

        string webRoot = GetValidatedWebRootPath(environment);
        string blazorIndexPath = Path.Combine(webRoot, "blazor", "index.html");

        if (File.Exists(blazorIndexPath))
        {
            logger?.LogInformation("[SPA Fallback] 成功掛載 Blazor WASM Fallback 路由: {Path}", blazorIndexPath);

            // 正確匹配 /blazor, /blazor/, /blazor/xxx 等前端路由
            return endpoints.MapFallbackToFile("blazor/{*path:regex(^[^.]*$)}", "blazor/index.html");
        }

        logger?.LogWarning("[SPA Fallback] 未發現 Blazor 頁面檔案：{Path}，將無法處理 Blazor SPA 路由！", blazorIndexPath);
        return new NullEndpointConventionBuilder();
    }

    private static string GetValidatedWebRootPath(IWebHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(environment.WebRootPath))
        {
            return environment.WebRootPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "wwwroot");
    }

    private sealed class NullEndpointConventionBuilder : IEndpointConventionBuilder
    {
        public void Add(Action<Microsoft.AspNetCore.Builder.EndpointBuilder> convention)
        {
            ArgumentNullException.ThrowIfNull(convention);
        }
    }
}