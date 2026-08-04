using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Module.Loaders
{
    public sealed class ModuleDatabaseInitializerHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Assembly _assembly;
        private readonly string _moduleName;
        private readonly string _dllPath;
        private readonly bool _isHostBuiltin;

        public ModuleDatabaseInitializerHostedService(
            IServiceProvider serviceProvider,
            Assembly assembly,
            string moduleName,
            string dllPath,
            bool isHostBuiltin = false)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            _moduleName = moduleName ?? throw new ArgumentNullException(nameof(moduleName));
            _dllPath = dllPath ?? throw new ArgumentNullException(nameof(dllPath));
            _isHostBuiltin = isHostBuiltin;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            await ModuleLoaderExtensions.RegisterModuleToDbAsync(_assembly, _moduleName, _dllPath, scope.ServiceProvider, _isHostBuiltin);
            Log.Information("模組 {Name} 依賴註冊與資料庫同步完成。", _moduleName);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
