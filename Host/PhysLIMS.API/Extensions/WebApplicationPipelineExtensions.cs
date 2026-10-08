// Path: src/SGSFramework/Host/PhysLIMS.API/Extensions/WebApplicationPipelineExtensions.cs
#nullable enable

namespace PhysLIMS.API.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using PhysLIMS.API.Dbcontexts;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Database;
using System.IO;

public static class WebApplicationPipelineExtensions
{
    /// <summary>
    /// 啟用企業級靜態檔案服務，支援 Vue 3 與 Blazor WASM 的完整 MIME Type 映射。
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static WebApplication UseEnterpriseStaticFiles(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // ----------------------------------------------------
        // Blazor WASM 與 Vue 靜態資源託管配置（確保 WebRoot 目錄存在，避免靜態檔案處置拋出警告）
        // ----------------------------------------------------
        // ----------------------------------------------------
        // 【執行期】靜態資源託管防護（確保目錄存在）
        // ----------------------------------------------------
        var webRootPath = app.Environment.WebRootPath;
        if (!string.IsNullOrEmpty(webRootPath) && !Directory.Exists(webRootPath))
        {
            Directory.CreateDirectory(webRootPath);
        }

        // 指定 Blazor WebAssembly 在 /blazor 前綴下掛載 _framework 資源
        app.UseBlazorFrameworkFiles("/blazor");
        // 啟用預設檔案（如 index.html）與靜態檔案託管
        app.UseDefaultFiles();

        // 建立 ContentTypeProvider 並補強/覆寫 Vue 與 Blazor WASM 的 MIME 對映
        var provider = new FileExtensionContentTypeProvider();

        // --- Vue / 現代前端 JavaScript & JSON ---
        provider.Mappings[".js"] = "application/javascript";
        provider.Mappings[".mjs"] = "application/javascript"; // ⚠️ Vite/Modern Web 必備 ESM 模組
        provider.Mappings[".json"] = "application/json";

        // --- Blazor WebAssembly 專屬檔案 ---
        provider.Mappings[".wasm"] = "application/wasm";
        provider.Mappings[".dat"] = "application/octet-stream";
        provider.Mappings[".blat"] = "application/octet-stream";
        provider.Mappings[".clat"] = "application/octet-stream";
        provider.Mappings[".br"] = "application/brotli";

        // 套用至 UseStaticFiles 中間件
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = provider
        });


        return app;
    }

    public static async Task ExecuteStartupSeedersAsync(this WebApplication app, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(config);

        using var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var services = scope.ServiceProvider;

        var autoMigrate = config.GetValue<bool>("Database:AutoMigrate", true);
        if (app.Environment.IsDevelopment() || autoMigrate)
        {
            var initializer = services.GetRequiredService<IDatabaseInitializer>();
            await initializer.InitializeDatabaseAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);

            var mainDbContext = services.GetRequiredService<PhysLIMSDbContext>();
            await mainDbContext.Database.MigrateAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        }

        var permissionSeeder = services.GetRequiredService<IPermissionSeedService>();
        await permissionSeeder.SeedAndSyncPermissionsAsync().ConfigureAwait(false);

        var menuSeeder = services.GetRequiredService<IMenuSeedService>();
        await menuSeeder.SeedAndSyncMenusAsync().ConfigureAwait(false);
    }
}