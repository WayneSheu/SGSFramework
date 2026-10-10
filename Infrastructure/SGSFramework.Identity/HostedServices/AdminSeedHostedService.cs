using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGSFramework.Identity.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.Identity.HostedServices
{
    public sealed class AdminSeedHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AdminSeedHostedService> _logger;

        public AdminSeedHostedService(IServiceProvider serviceProvider, ILogger<AdminSeedHostedService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(">>> [System Startup] 開始檢查並初始化預設系統管理員帳號...");

            using var scope = _serviceProvider.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<IAdminSeederService>();

            // 修正：改用 CancellationToken.None，避免啟動生命週期 Token 觸發 OperationCanceledException 導致中斷
            await seeder.SeedAdminAsync(CancellationToken.None);

            _logger.LogInformation(">>> [System Startup] 預設系統管理員檢查與初始化程序完成。");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}