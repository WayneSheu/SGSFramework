namespace PhysLIMS.API.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using PhysLIMS.API.Dbcontexts;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Database;
using SGSFramework.ModulePlugin.Extensions;
using SGSFramework.ModulePlugin.Systems.Controller.Providers;

/// <summary>
/// WebApplication 中間件管道與啟動任務擴充類別
/// </summary>
public static class WebApplicationPipelineExtensions
{
    /// <summary>
    /// 配置靜態檔案與 Blazor WASM / Vue 專屬 Content-Type 映射
    /// </summary>
    public static WebApplication UseEnterpriseStaticFiles(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var webRootPath = app.Environment.WebRootPath;
        if (!string.IsNullOrEmpty(webRootPath) && !Directory.Exists(webRootPath))
        {
            Directory.CreateDirectory(webRootPath);
        }

        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".js"] = "application/javascript";
        provider.Mappings[".mjs"] = "application/javascript";
        provider.Mappings[".json"] = "application/json";
        provider.Mappings[".wasm"] = "application/wasm";
        provider.Mappings[".dat"] = "application/octet-stream";
        provider.Mappings[".blat"] = "application/octet-stream";
        provider.Mappings[".clat"] = "application/octet-stream";
        provider.Mappings[".br"] = "application/brotli";

        // 1. 掛載 Blazor WASM 框架檔案路徑
        app.UseBlazorFrameworkFiles("/blazor");

        // 2. 預設檔案與靜態資源 ContentTypeProvider 映射
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = provider
        });

        return app;
    }

    /// <summary>
    /// 執行系統啟動自動遷移與種子資料同步 (單一 Scope 優化)
    /// </summary>
    public static async Task ExecuteStartupSeedersAsync(this WebApplication app, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(config);

        using var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var services = scope.ServiceProvider;

        // 1. 自動 Migration
        var autoMigrate = config.GetValue<bool>("Database:AutoMigrate", true);
        if (app.Environment.IsDevelopment() || autoMigrate)
        {
            var initializer = services.GetRequiredService<IDatabaseInitializer>();
            await initializer.InitializeDatabaseAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);

            var mainDbContext = services.GetRequiredService<PhysLIMSDbContext>();
            await mainDbContext.Database.MigrateAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        }

        // 2. 權限與選單樹種子資料同步
        var permissionSeeder = services.GetRequiredService<IPermissionSeedService>();
        await permissionSeeder.SeedAndSyncPermissionsAsync().ConfigureAwait(false);

        var menuSeeder = services.GetRequiredService<IMenuSeedService>();
        await menuSeeder.SeedAndSyncMenusAsync().ConfigureAwait(false);
    }
}