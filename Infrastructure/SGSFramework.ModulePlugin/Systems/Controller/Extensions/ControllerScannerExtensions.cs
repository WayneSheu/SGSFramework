namespace SGSFramework.ModulePlugin.Systems.Controller.Extensions
{

    //public static class ControllerScannerExtensions
    //{
    //    /// <summary>
    //    /// 註冊 Controller 掃描服務至 DI 容器
    //    /// </summary>
    //    /// <typeparam name="TDbContext">目標 DbContext</typeparam>
    //    public static IServiceCollection AddControllerScanner<TDbContext>(this IServiceCollection services)
    //        where TDbContext : DbContext
    //    {
    //        // 使用 TryAdd 避免重複註冊
    //        services.TryAddScoped<IControllerScannerService<TDbContext>, ControllerScannerService<TDbContext>>();
    //        return services;
    //    }

    //    /// <summary>
    //    /// 系統啟動時執行自動掃描與註冊
    //    /// </summary>
    //    /// <param name="host">IHost 實例</param>
    //    /// <param name="moduleAssemblyFilter">用於篩選模組 Assembly 的條件 (例如: name => name.Contains("Modules"))</param>
    //    public static async Task UseControllerScanner<TDbContext>(this IHost host, Func<string, bool> moduleAssemblyFilter)
    //        where TDbContext : DbContext
    //    {
    //        using var scope = host.Services.CreateScope();
    //        var scanner = scope.ServiceProvider.GetRequiredService<IControllerScannerService<TDbContext>>();
    //        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IControllerScannerService<TDbContext>>>();

    //        try
    //        {
    //            // 取得已載入且符合篩選條件的 Assemblies
    //            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
    //                .Where(a => a.FullName != null && moduleAssemblyFilter(a.FullName));

    //            await scanner.ScanAndRegisterAsync(assemblies);
    //            logger.LogInformation("Controller Metadata synchronization completed successfully.");
    //        }
    //        catch (Exception ex)
    //        {UseDynamicControllersAsync
    //            logger.LogError(ex, "An error occurred while synchronizing Controller Metadata.");
    //            throw;
    //        }
    //    }
    //}

}
