// ==========================================
// 檔案路徑: src/SGSFramework/Infrastructure/SGSFramework.ModulePlugin/Extensions/ModulePluginExtensions.cs
// 架構層級: Infrastructure Framework
// ==========================================

namespace SGSFramework.ModulePlugin.Extensions;

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
using SGSFramework.Alert.Extensions;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Controllers.Services;
using SGSFramework.Core.Migrations;
using SGSFramework.ModulePlugin.Abstractions;
using SGSFramework.ModulePlugin.Services;
using SGSFramework.ModulePlugin.Systems.Controller.Providers;
using SGSFramework.ModulePlugin.Systems.Controller.Repositories;
using SGSFramework.ModulePlugin.Systems.Controller.Services;
using SGSFramework.ModulePlugin.Systems.Module;
using SGSFramework.ModulePlugin.Systems.Module.Containers;
using SGSFramework.ModulePlugin.Systems.Module.Extensions;
using SGSFramework.ModulePlugin.Systems.Module.Loaders;
using SGSFramework.ModulePlugin.Systems.Module.Registries;
using SGSFramework.ModulePlugin.Systems.Module.Services;

/// <summary>
/// 模組化插件系統 DI 服務註冊與應用程式啟動擴充
/// </summary>
public static class ModulePluginExtensions
{
    /// <summary>
    /// 將模組化插件系統的核心服務、策略模式存取層與動態控制器倉儲註冊至 DI 容器（支援指定 DbContext）。
    /// </summary>
    public static IServiceCollection AddModulePlugin<TDbContext>(this IServiceCollection services, IConfiguration config)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddModuleFrameworkServices<TDbContext>();

        RegisterCoreServices(services);
        RegisterRepositories<TDbContext>(services);
        RegisterHostedServices(services, config);

        return services;
    }

    /// <summary>
    /// 保留相容性之非泛型多載，適用於無 DbContext 依賴之基礎模組註冊。
    /// </summary>
    public static IServiceCollection AddModulePlugin(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        RegisterCoreServices(services);
        services.TryAddScoped(typeof(IDynamicControllerRepository<>), typeof(DynamicControllerRepository<>));

        services.AddModularModules(config);
        services.AddHostedService<ModuleMonitorService>();
        services.AddHostedService<ModuleFileWatcherService>();
        services.AddHostedService<SystemModuleDatabaseInitializerHostedService>();

        return services;
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.TryAddSingleton<ModuleRegistry>();
        services.TryAddSingleton<IModuleRegistry>(sp => sp.GetRequiredService<ModuleRegistry>());
        services.TryAddSingleton<ServiceRegistryMonitor>();

        services.TryAddSingleton<IDynamicActionDescriptorChangeProvider>(DynamicActionDescriptorChangeProvider.Instance);
        services.TryAddSingleton<IActionDescriptorChangeProvider>(sp => sp.GetRequiredService<IDynamicActionDescriptorChangeProvider>());

        services.TryAddScoped<ModuleLifecycleService>();
        services.TryAddScoped<IModuleAssemblyRegisterService, ModuleAssemblyRegisterService>();
        services.TryAddScoped<IModuleUnloader, ModuleUnloader>();
        services.TryAddScoped<IModuleManagementApplicationService, ModuleManagementApplicationService>();
    }

    private static void RegisterRepositories<TDbContext>(IServiceCollection services) where TDbContext : DbContext
    {
        services.TryAddScoped<IDynamicControllerRepository<ControllerMetadata>>(sp =>
        {
            var context = sp.GetRequiredService<TDbContext>();
            var cache = sp.GetRequiredService<IMemoryCache>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DynamicControllerRepository<ControllerMetadata>>>();
            return new DynamicControllerRepository<ControllerMetadata>(context, cache, logger);
        });

        services.TryAddScoped(typeof(IDynamicControllerRepository<>), typeof(DynamicControllerRepository<>));
    }

    private static void RegisterHostedServices(IServiceCollection services, IConfiguration config)
    {
        services.AddModularModules(config);
        services.AddEnterpriseAlertInfrastructure();

        services.AddHostedService<ModuleMonitorService>();
        services.AddHostedService<ModuleFileWatcherService>();
        services.AddHostedService<SystemModuleDatabaseInitializerHostedService>();
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
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IControllerScannerService<TDbContext>>>();

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

        var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("ModularSystemInitialization");
        var registry = provider.GetRequiredService<IModuleRegistry>();
        var lifecycleService = provider.GetRequiredService<ModuleLifecycleService>();
        var assemblies = provider.GetServices<ModuleAssemblyContainer>();

        logger.LogInformation(">>> 開始執行各功能模組初始化...");

        var modules = ModuleLoaderExtensions.GetAllInitializers();
        foreach (var module in modules)
        {
            await InitializeSingleModuleAsync(module, provider, registry, lifecycleService, host, logger).ConfigureAwait(false);
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

    private static async Task InitializeSingleModuleAsync(
        IModuleInitializer module,
        IServiceProvider provider,
        IModuleRegistry registry,
        ModuleLifecycleService lifecycleService,
        IHost host,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        try
        {
            registry.RegisterModule(module);

            var migrationService = provider.GetService<IMigrationService>();
            if (migrationService != null)
            {
                await ProcessModuleMigrationsAsync(migrationService, module.ModuleName, logger).ConfigureAwait(false);
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

    private static async Task ProcessModuleMigrationsAsync(
        IMigrationService migrationService,
        string moduleName,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        await migrationService.DiagnosticMigrations().ConfigureAwait(false);

        var pending = await migrationService.GetPendingMigrationsAsync().ConfigureAwait(false);
        var pendingList = pending.ToList();

        if (pendingList.Count > 0)
        {
            logger.LogInformation(">>> 發現模組 {Name} 有 {Count} 個待處理遷移...", moduleName, pendingList.Count);
            try
            {
                await migrationService.MigrateAsync().ConfigureAwait(false);
                logger.LogInformation(">>> 模組 {Name} 資料庫遷移成功。", moduleName);
            }
            catch (MigrationException mex)
            {
                logger.LogError(mex, ">>> 模組 {Name} 遷移失敗: {Message}", moduleName, mex.Message);
                throw;
            }
        }
        else
        {
            logger.LogInformation(">>> 模組 {Name} 遷移已是最新狀態。", moduleName);
        }
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
            .Where(name => name.StartsWith("sgs.modules."))
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