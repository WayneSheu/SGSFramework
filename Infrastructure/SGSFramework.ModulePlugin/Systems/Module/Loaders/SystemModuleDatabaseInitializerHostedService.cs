using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Module.Loaders
{
    public sealed class SystemModuleDatabaseInitializerHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private const string SystemModuleName = "SGSFramework.System";

        public SystemModuleDatabaseInitializerHostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // 1. 自動掃描所有 SGSFramework 前綴組件
            var systemAssemblies = SystemModuleScanner.ScanSystemFrameworkAssemblies();

            if (systemAssemblies.Count == 0)
            {
                Log.Warning(">>> [SystemModuleInitializer] 未掃描到任何 SGSFramework 組件。");
                return;
            }

            // 2. 執行寫入/同步資料庫
            using var scope = _serviceProvider.CreateScope();
            string hostDllPath = Assembly.GetEntryAssembly()?.Location ?? string.Empty;

            await ModuleLoaderExtensions.RegisterModuleToDbAsync(
                systemAssemblies,
                SystemModuleName,
                hostDllPath,
                scope.ServiceProvider,
                isHostBuiltin: true
            );

            Log.Information(">>> [SystemModuleInitializer] 系統核心組件 ({Count} 個 Assembly) 已經成功自動同步至 DB！", systemAssemblies.Count);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
