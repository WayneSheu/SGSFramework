using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Security.Services;
using SGSFramework.ModulePlugin.Abstractions;
using SGSFramework.ModulePlugin.Systems.Controller.Providers;
using SGSFramework.ModulePlugin.Systems.Module.Contexts;
using SGSFramework.ModulePlugin.Systems.Module.Loaders;

namespace SGSFramework.ModulePlugin.Systems.Module.Services
{
    public sealed class ModuleAssemblyRegisterService : IModuleAssemblyRegisterService
    {
        private readonly ApplicationPartManager _partManager;
        private readonly IDynamicActionDescriptorChangeProvider _changeProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ModuleAssemblyRegisterService> _logger;

        public ModuleAssemblyRegisterService(
            ApplicationPartManager partManager,
            IDynamicActionDescriptorChangeProvider changeProvider,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<ModuleAssemblyRegisterService> logger)
        {
            _partManager = partManager ?? throw new ArgumentNullException(nameof(partManager));
            _changeProvider = changeProvider ?? throw new ArgumentNullException(nameof(changeProvider));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RegisterAndRefreshModuleAsync(string mainDllPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(mainDllPath) || !File.Exists(mainDllPath))
            {
                throw new FileNotFoundException($"找不到指定的模組入口檔案: {mainDllPath}");
            }

            _logger.LogInformation(">>> [Runtime Heat-Load] 開始執行模組熱載入程序: {Path}", mainDllPath);

            // 1. 數位簽章驗證
            var verifier = _serviceProvider.GetRequiredService<AssemblySecurityVerifier>();
            if (!verifier.VerifyAssembly(mainDllPath))
            {
                _logger.LogWarning(">>> [Security] 運行期上傳模組數位簽章驗證失敗: {Path}", mainDllPath);
                throw new InvalidOperationException($"模組 '{Path.GetFileName(mainDllPath)}' 未通過數位簽章驗證，拒絕熱載入。");
            }

            // 2. 建立專屬 ModuleAssemblyLoadContext 並加載 Sidecar 組件 (.Application.dll, .Infrastructure.dll)
            var alc = new ModuleAssemblyLoadContext(mainDllPath);
            string directory = Path.GetDirectoryName(mainDllPath)!;
            string baseModuleName = Path.GetFileNameWithoutExtension(mainDllPath);

            string[] sidecarSuffixes = { ".Application.dll", ".Infrastructure.dll" };
            foreach (var suffix in sidecarSuffixes)
            {
                string sidecarPath = Path.Combine(directory, baseModuleName + suffix);
                if (File.Exists(sidecarPath))
                {
                    if (!verifier.VerifyAssembly(sidecarPath))
                    {
                        _logger.LogWarning(">>> [Security] 附屬組件驗證失敗，阻擋載入: {Path}", sidecarPath);
                        continue;
                    }

                    byte[] sidecarBytes = await File.ReadAllBytesAsync(sidecarPath, cancellationToken);
                    string sidecarPdbPath = Path.ChangeExtension(sidecarPath, ".pdb");
                    byte[]? sidecarPdbBytes = File.Exists(sidecarPdbPath) ? await File.ReadAllBytesAsync(sidecarPdbPath, cancellationToken) : null;

                    using var sidecarAsmStream = new MemoryStream(sidecarBytes);
                    using var sidecarPdbStream = sidecarPdbBytes != null ? new MemoryStream(sidecarPdbBytes) : null;

                    alc.LoadFromStream(sidecarAsmStream, sidecarPdbStream);
                    _logger.LogInformation(">>> [Runtime Heat-Load] 附屬組件成功載入至同一 ALC: {Path}", sidecarPath);
                }
            }

            // 3. 加載主模組 Assembly (修正 mainPdbBytes 變數宣告)
            byte[] mainBytes = await File.ReadAllBytesAsync(mainDllPath, cancellationToken);
            string pdbPath = Path.ChangeExtension(mainDllPath, ".pdb");
            byte[]? mainPdbBytes = File.Exists(pdbPath) ? await File.ReadAllBytesAsync(pdbPath, cancellationToken) : null;

            using var mainAsmStream = new MemoryStream(mainBytes);
            using var mainPdbStream = mainPdbBytes != null ? new MemoryStream(mainPdbBytes) : null;

            var assembly = alc.LoadFromStream(mainAsmStream, mainPdbStream);

            // 4. 解析 Initializer 並註冊至 Registry
            var initializerType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IModuleInitializer).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            string moduleName = baseModuleName;
            if (initializerType != null)
            {
                var initializer = (IModuleInitializer)Activator.CreateInstance(initializerType)!;
                moduleName = initializer.ModuleName;

                // 寫入已修改為 public 的 InitializerRegistry
                ModuleLoaderExtensions.InitializerRegistry[moduleName] = initializer;
            }

            // 5. 更新 ApplicationPartManager
            var controllerAssemblyPart = new AssemblyPart(assembly);
            if (!_partManager.ApplicationParts.Any(p => p.Name == controllerAssemblyPart.Name))
            {
                _partManager.ApplicationParts.Add(controllerAssemblyPart);
                _logger.LogInformation(">>> [Runtime Heat-Load] 已將 AssemblyPart 加入 ApplicationPartManager: {Name}", controllerAssemblyPart.Name);
            }

            // 6. 寫入 DB 中繼資料與 ControllerMetadata
            using (var scope = _serviceProvider.CreateScope())
            {
                await ModuleLoaderExtensions.RegisterModuleToDbAsync(assembly, moduleName, mainDllPath, scope.ServiceProvider);
                _logger.LogInformation(">>> [Runtime Heat-Load] 模組 {ModuleName} DB 元資料與 ControllerMetadata 同步成功。", moduleName);
            }

            // 7. 發送通知以刷新 MVC 路由與文件
            _changeProvider.NotifyChanges();
            _logger.LogInformation(">>> [Runtime Heat-Load] 已發送 NotifyChanges，MVC 路由與 Scalar/OpenAPI 文件刷新完畢。");
        }
    }
}
