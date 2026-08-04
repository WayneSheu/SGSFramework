using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SGSFramework.ModulePlugin.Abstractions;

namespace SGSFramework.ModulePlugin.Systems.Module.Services
{
    public class ModuleFileWatcherService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public ModuleFileWatcherService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 確保路徑存在
            var path = Path.Combine(Directory.GetCurrentDirectory(), "plugins");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            var watcher = new FileSystemWatcher(path, "*.dll");
            watcher.Deleted += async (s, e) => {
                var moduleName = Path.GetFileNameWithoutExtension(e.Name);
                //await _unloader.UnloadModuleAsync(moduleName);
                // 當事件觸發時，建立獨立的 Scope 來執行 Unloader
                using var scope = _scopeFactory.CreateScope();
                var unloader = scope.ServiceProvider.GetRequiredService<IModuleUnloader>();
                await unloader.UnloadModuleAsync(moduleName);
            };

            watcher.EnableRaisingEvents = true;

            // 保持此服務運行，直到收到停止訊號
            return Task.Delay(Timeout.Infinite, stoppingToken);
        }

    }
}
