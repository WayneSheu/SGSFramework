using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.AuthTokenBucket.HostedServices
{
    /// <summary>
    /// 啟動時的動態選單種子同步。
    /// 系統核心 / 外掛模組的 ControllerMetadata 由各自的 HostedService 於 StartAsync 寫入，
    /// 因此選單同步必須在 StartedAsync（所有 HostedService 的 StartAsync 完成後）才執行，
    /// 否則全新資料庫部署時讀不到任何 IsMenu 節點，MenuItems 將維持空白。
    /// </summary>
    public sealed class MenuSeedHostedService : IHostedLifecycleService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MenuSeedHostedService> _logger;

        public MenuSeedHostedService(IServiceProvider serviceProvider, ILogger<MenuSeedHostedService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartedAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(">>> [System Startup] 開始同步動態選單種子...");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var menuSeeder = scope.ServiceProvider.GetRequiredService<IMenuSeedService>();

                // 與 AdminSeedHostedService 一致，避免啟動生命週期 Token 觸發 OperationCanceledException 導致中斷
                await menuSeeder.SeedAndSyncMenusAsync(CancellationToken.None).ConfigureAwait(false);

                _logger.LogInformation(">>> [System Startup] 動態選單種子同步程序完成。");
            }
            catch (Exception ex)
            {
                // 選單同步失敗不阻斷站台啟動，可於「選單管理 > 手動同步選單」重新觸發
                _logger.LogError(ex, ">>> [System Startup] 動態選單種子同步失敗，請於選單管理頁手動同步。");
            }
        }

        public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
