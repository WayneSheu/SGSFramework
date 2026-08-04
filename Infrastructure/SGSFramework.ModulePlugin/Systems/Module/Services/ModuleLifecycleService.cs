using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using SGSFramework.ModulePlugin.Abstractions;
using System.Diagnostics;

namespace SGSFramework.ModulePlugin.Systems.Module.Services
{
    public class ModuleLifecycleService
    {
        private readonly IModuleRegistry _registry;
        private readonly ILogger<ModuleLifecycleService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ModuleLifecycleService(
            IModuleRegistry registry,
            ILogger<ModuleLifecycleService> logger,
            IServiceProvider serviceProvider)
        {
            _registry = registry;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 註冊模組並觸發生命週期初始化，自動記錄耗時與健康狀態
        /// </summary>
        public async Task RegisterAndInitializeAsync(IModuleInitializer module, IApplicationBuilder app)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Initializing module: {Name}", module.ModuleName);

                // 1. 執行初始化邏輯
                await module.OnApplicationConfigureAsync(app);

                sw.Stop();

                // 2. 註冊至 Registry (需在 Registry 中擴充對應方法)
                _registry.RegisterModule(module);

                // 3. 記錄初始化報告
                // 此處記錄耗時，MemoryUsage 可透過 GC.GetTotalMemory 獲取
                var report = new ModuleHealthReport(
                    module.ModuleName,
                    sw.Elapsed,
                    true,
                    GC.GetTotalMemory(false)
                );

                _logger.LogInformation("Module {Name} initialized successfully in {Elapsed}ms",
                    module.ModuleName, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Failed to initialize module: {Name}", module.ModuleName);
                throw; // 依據企業級規範，初始化失敗通常應阻斷啟動流程
            }
        }
    }
}
