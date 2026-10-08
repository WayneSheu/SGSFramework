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
    /// <summary>
    /// Vue 3 SPA 後備路由 (排除 /api, /scalar, /openapi, /blazor 等後端與特定 SPA 路徑)
    /// </summary>
    public static void MapProtectedSpaFallback(
        this WebApplication app,
        IWebHostEnvironment environment,
        string fallbackFile = "index.html")
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(environment);

        string webRoot = GetValidatedWebRootPath(environment);
        string indexPath = Path.Combine(webRoot, fallbackFile);

        if (!File.Exists(indexPath))
        {
            var logger = app.Services.GetService<ILoggerFactory>()?.CreateLogger(nameof(SpaFallbackExtensions));
            logger?.LogWarning("[SPA Fallback] 未發現 Vue 3 頁面檔案：{Path}，跳過掛載。", indexPath);
            return;
        }

        // 使用 MapWhen 分流，既能保持優雅的封裝，又能確保 100% 精準攔截非後端/Blazor 請求
        app.MapWhen(context =>
            !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
            !context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase) &&
            !context.Request.Path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase) &&
            !context.Request.Path.StartsWithSegments("/blazor", StringComparison.OrdinalIgnoreCase),
            builder =>
            {
                builder.UseRouting();
                builder.UseEndpoints(endpoints =>
                {
                    endpoints.MapFallbackToFile(fallbackFile);
                });
            });
    }

    /// <summary>
    /// Blazor WASM SPA 後備路由
    /// </summary>
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

            // 精準匹配 /blazor, /blazor/, /blazor/xxx
            endpoints.MapFallbackToFile("blazor", "blazor/index.html");
            return endpoints.MapFallbackToFile("blazor/{*path}", "blazor/index.html");
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