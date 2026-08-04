using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Migrations;
using SGSFramework.ModulePlugin.Abstractions;
using SGSFramework.ModulePlugin.Systems.Controller.Providers;
using SGSFramework.ModulePlugin.Systems.Controller.Repositories;
using SGSFramework.ModulePlugin.Systems.Controller.Services;
using SGSFramework.ModulePlugin.Systems.Menu.Extensions;
using SGSFramework.ModulePlugin.Systems.Module;
using SGSFramework.ModulePlugin.Systems.Module.Containers;
using SGSFramework.ModulePlugin.Systems.Module.Loaders;
using SGSFramework.ModulePlugin.Systems.Module.Registries;
using SGSFramework.ModulePlugin.Systems.Module.Repositories;
using SGSFramework.ModulePlugin.Systems.Module.Services;

namespace SGSFramework.ModulePlugin.Extensions;

public static class ModulePluginExtensions
{
    /// <summary>
    /// 將模組化插件系統的核心服務與動態控制器倉儲註冊至 DI 容器（支援指定 DbContext）。
    /// </summary>
    /// <typeparam name="TDbContext">目標 EF Core DbContext 類型</typeparam>
    public static IServiceCollection AddModulePlugin<TDbContext>(this IServiceCollection services, IConfiguration config)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        // 1. 模組註冊與註冊表服務
        services.AddSingleton<ModuleRegistry>();
        services.AddSingleton<IModuleRegistry>(sp => sp.GetRequiredService<ModuleRegistry>());
        services.AddSingleton<ServiceRegistryMonitor>();

        // 2. 動態 ActionDescriptor 變更通知提供者 (Singleton)
        services.AddSingleton<IDynamicActionDescriptorChangeProvider>(DynamicActionDescriptorChangeProvider.Instance);
        services.AddSingleton<IActionDescriptorChangeProvider>(sp => sp.GetRequiredService<IDynamicActionDescriptorChangeProvider>());

        // 3. 模組與動態控制器資料庫倉儲 (Scoped, 依賴目標 DbContext)
        services.AddScoped<IModuleRepository>(sp =>
        {
            var context = sp.GetRequiredService<TDbContext>();
            var cache = sp.GetRequiredService<IMemoryCache>();
            return new ModuleRepository<TDbContext>(context, cache);
        });

        services.AddScoped<IDynamicControllerRepository<ControllerMetadata>>(sp =>
        {
            var context = sp.GetRequiredService<TDbContext>();
            var cache = sp.GetRequiredService<IMemoryCache>();
            var logger = sp.GetRequiredService<ILogger<DynamicControllerRepository<ControllerMetadata>>>();
            return new DynamicControllerRepository<ControllerMetadata>(context, cache, logger);
        });

        services.AddScoped(typeof(IDynamicControllerRepository<>), typeof(DynamicControllerRepository<>));

        // 4. 模組生命週期管理、熱加載與卸載服務
        services.AddScoped<ModuleLifecycleService>();
        services.AddScoped<IModuleAssemblyRegisterService, ModuleAssemblyRegisterService>();
        services.AddScoped<IModuleUnloader, ModuleUnloader>();

        // 5. 動態外掛模組與背景監控服務 (HostedServices)
        services.AddModularModules(config);
        services.AddHostedService<ModuleMonitorService>();
        services.AddHostedService<ModuleFileWatcherService>();

        // 6. 系統核心模組與內建控制器自動同步初始化服務
        services.AddHostedService<SystemModuleDatabaseInitializerHostedService>();

        // 7. 註冊動態選單擴充
        services.AddDynamicMenu();

        return services;
    }

    /// <summary>
    /// 保留相容性之非泛型多載，適用於無 DbContext 依賴之基礎模組註冊。
    /// </summary>
    public static IServiceCollection AddModulePlugin(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddSingleton<ModuleRegistry>();
        services.AddSingleton<IModuleRegistry>(sp => sp.GetRequiredService<ModuleRegistry>());
        services.AddSingleton<ServiceRegistryMonitor>();

        services.AddSingleton<IDynamicActionDescriptorChangeProvider>(DynamicActionDescriptorChangeProvider.Instance);
        services.AddSingleton<IActionDescriptorChangeProvider>(sp => sp.GetRequiredService<IDynamicActionDescriptorChangeProvider>());

        services.AddScoped<ModuleLifecycleService>();
        services.AddScoped<IModuleAssemblyRegisterService, ModuleAssemblyRegisterService>();
        services.AddScoped<IModuleUnloader, ModuleUnloader>();
        services.AddScoped(typeof(IDynamicControllerRepository<>), typeof(DynamicControllerRepository<>));

        services.AddModularModules(config);
        services.AddHostedService<ModuleMonitorService>();
        services.AddHostedService<ModuleFileWatcherService>();
        services.AddHostedService<SystemModuleDatabaseInitializerHostedService>();

        services.AddDynamicMenu();

        return services;
    }

    /// <summary>
    /// 註冊 Controller 掃描服務至 DI 容器
    /// </summary>
    public static IServiceCollection AddControllerScanner<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IControllerScannerService<TDbContext>, ControllerScannerService<TDbContext>>();
        return services;
    }

    /// <summary>
    /// 系統啟動時執行自動掃描與註冊
    /// </summary>
    public static async Task UseControllerScanner<TDbContext>(this IHost host, Func<string, bool> moduleAssemblyFilter)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(moduleAssemblyFilter);

        using var scope = host.Services.CreateScope();
        var scanner = scope.ServiceProvider.GetRequiredService<IControllerScannerService<TDbContext>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IControllerScannerService<TDbContext>>>();

        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName != null && moduleAssemblyFilter(a.FullName));

            await scanner.ScanAndRegisterAsync(assemblies).ConfigureAwait(false);
            logger.LogInformation("Controller Metadata synchronization completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while synchronizing Controller Metadata.");
            throw;
        }
    }

    /// <summary>
    /// 透過註冊模組、執行遷移和確保控制器一致性來初始化模組化系統。
    /// </summary>
    public static async Task<IHost> InitializeModularSystemAsync(this IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("ModularSystemInitialization");
        var registry = provider.GetRequiredService<IModuleRegistry>();
        var lifecycleService = provider.GetRequiredService<ModuleLifecycleService>();
        var repo = provider.GetRequiredService<IDynamicControllerRepository<ControllerMetadata>>();
        var assemblies = provider.GetServices<ModuleAssemblyContainer>();

        logger.LogInformation(">>> 開始執行各功能模組初始化...");

        var modules = ModuleLoaderExtensions.GetAllInitializers();
        foreach (var module in modules)
        {
            try
            {
                registry.RegisterModule(module);

                var migrationService = provider.GetService<IMigrationService>();
                if (migrationService != null)
                {
                    await migrationService.DiagnosticMigrations().ConfigureAwait(false);

                    var pending = await migrationService.GetPendingMigrationsAsync().ConfigureAwait(false);
                    var pendingList = pending.ToList();

                    if (pendingList.Count > 0)
                    {
                        logger.LogInformation(">>> 發現模組 {Name} 有 {Count} 個待處理遷移...", module.ModuleName, pendingList.Count);
                        try
                        {
                            await migrationService.MigrateAsync().ConfigureAwait(false);
                            logger.LogInformation(">>> 模組 {Name} 資料庫遷移成功。", module.ModuleName);
                        }
                        catch (MigrationException mex)
                        {
                            logger.LogError(mex, ">>> 模組 {Name} 遷移失敗: {Message}", module.ModuleName, mex.Message);
                            throw;
                        }
                    }
                    else
                    {
                        logger.LogInformation(">>> 模組 {Name} 遷移已是最新狀態。", module.ModuleName);
                    }
                }

                if (host is IApplicationBuilder appBuilder)
                {
                    await lifecycleService.RegisterAndInitializeAsync(module, appBuilder).ConfigureAwait(false);
                }
                else
                {
                    logger.LogWarning(">>> [Warning] 當前宿主 (IHost) 未實作 IApplicationBuilder，跳過 Web 相關中間件註冊。");
                }

                logger.LogInformation(">>> 模組 {Name} 初始化完成。", module.ModuleName);
            }
            catch (Exception ex)
            {
                LogModuleException(ex, module.ModuleName);
                throw;
            }
        }

        foreach (var container in assemblies)
        {
            string moduleName = container.Assembly.GetName().Name ?? "UnknownModule";
            await ModuleLoaderExtensions.RegisterModuleToDbAsync(container.Assembly, moduleName, provider).ConfigureAwait(false);
        }

        logger.LogInformation(">>> 正在進行系統路由一致性檢查...");
        await EnsureControllerConsistency(provider).ConfigureAwait(false);
        logger.LogInformation(">>> 系統路由一致性已確認。");

        return host;
    }

    /// <summary>
    /// 確保檔案系統 (Plugins 目錄) 與 資料庫 (ControllerMetadata 表) 兩者狀態同步
    /// </summary>
    public static async Task EnsureControllerConsistency(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        var unloader = sp.GetRequiredService<IModuleUnloader>();
        var controllerRepo = sp.GetRequiredService<IDynamicControllerRepository<ControllerMetadata>>();

        var currentModules = ModuleLoaderExtensions.GetLoadedModuleNames()
            .Select(m => m.ToLowerInvariant().Trim())
            .ToHashSet();

        var allControllers = await controllerRepo.GetActiveControllersAsync().ConfigureAwait(false);

        var registeredPluginModules = allControllers
            .Select(x => x.ModuleName.ToLowerInvariant().Trim())
            .Where(name => name.StartsWith("ses.modules."))
            .Distinct();

        var deadModules = registeredPluginModules.Except(currentModules).ToList();

        if (deadModules.Count == 0)
        {
            Log.Information(">>> 一致性檢查完成，未發現需要清理的殭屍模組。");
        }
        else
        {
            Log.Warning(">>> 發現殭屍模組: {Count} 個", deadModules.Count);
            foreach (var moduleName in deadModules)
            {
                Log.Warning(">>> [自動化修復] 偵測到殭屍插件模組: {Module}, 正在卸載...", moduleName);
                await unloader.UnloadModuleAsync(moduleName).ConfigureAwait(false);
            }
        }
    }

    private static void LogModuleException(Exception ex, string moduleName)
    {
        Log.Fatal(">>> [Critical] 模組 {Name} 初始化發生嚴重異常: {Msg}", moduleName, ex.Message);

        var inner = ex.InnerException;
        int level = 1;
        while (inner != null)
        {
            Log.Error("  └─ 錯誤層級 [{Level}] | 類型: {Type} | 訊息: {Msg}",
                level++, inner.GetType().Name, inner.Message);

            if (inner is AggregateException aggEx)
            {
                foreach (var innerEx in aggEx.InnerExceptions)
                {
                    Log.Error("      └─ Aggregate 子錯誤: {Msg}", innerEx.Message);
                }
            }
            inner = inner.InnerException;
        }
    }
}