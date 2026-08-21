using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Modules;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Controllers.Services;
using SGSFramework.Core.Migrations;
using SGSFramework.Core.Security.Services;
using SGSFramework.ModulePlugin.Abstractions;
using SGSFramework.ModulePlugin.Systems.Controller.Repositories;
using SGSFramework.ModulePlugin.Systems.Module.Containers;
using SGSFramework.ModulePlugin.Systems.Module.Contexts;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Loader;

namespace SGSFramework.ModulePlugin.Systems.Module.Loaders;

public static class ModuleLoaderExtensions
{
    public static readonly ConcurrentDictionary<string, IModuleInitializer> InitializerRegistry = new();
    private static readonly ConcurrentDictionary<string, (ModuleAssemblyLoadContext Context, Assembly Assembly, IModuleInitializer Initializer)> LoadedModulesRegistry = new();
    private static ApplicationPartManager? _globalPartManager;

    static ModuleLoaderExtensions()
    {
        AssemblyLoadContext.Default.Resolving += (sender, assemblyName) =>
        {
            foreach (var registry in LoadedModulesRegistry.Values)
            {
                if (registry.Assembly.GetName().Name == assemblyName.Name)
                {
                    return registry.Assembly;
                }
            }
            return null;
        };
    }

    #region Service Collection & Middleware Extensions

    public static IServiceCollection AddModularModules(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            var partManager = services.GetApplicationPartManager();

            string trustedThumbprint = configuration["ModularSettings:TrustedThumbprint"]
                ?? throw new InvalidOperationException("未設定受信任的模組簽章憑證指紋 (TrustedThumbprint)，系統拒絕啟動以確保安全性。");

            services.AddSingleton(sp => new AssemblySecurityVerifier(
                sp.GetRequiredService<ILogger<AssemblySecurityVerifier>>(),
                trustedThumbprint));

            string absolutePluginPath = GetPluginsDirectory(configuration);
            Log.Information(">>> [IIS/Host Path Fix] 開始掃描外掛目錄 (絕對路徑解析): {Path}", absolutePluginPath);

            if (!Directory.Exists(absolutePluginPath))
            {
                Log.Warning(">>> 外掛模組路徑不存在，系統將自動建立: {Path}", absolutePluginPath);
                Directory.CreateDirectory(absolutePluginPath);
            }

            var mainModuleFiles = Directory.GetFiles(absolutePluginPath, "*.dll")
                .Where(f => !Path.GetFileName(f).EndsWith(".Application.dll", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(f).EndsWith(".Infrastructure.dll", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Log.Information(">>> 找到 {Count} 個主模組入口 DLL 檔案", mainModuleFiles.Length);

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
            var bootstrapVerifier = new AssemblySecurityVerifier(loggerFactory.CreateLogger<AssemblySecurityVerifier>(), trustedThumbprint);

            foreach (var mainDllPath in mainModuleFiles)
            {
                ModuleAssemblyLoadContext? currentContext = null;

                try
                {
                    if (!bootstrapVerifier.VerifyAssembly(mainDllPath))
                    {
                        Log.Warning(">>> [Security] 主模組數位簽章驗證失敗，已阻擋非法檔案載入. 檔案: {File}", mainDllPath);
                        QuarantineOrDeleteFile(mainDllPath);
                        continue;
                    }

                    currentContext = new ModuleAssemblyLoadContext(mainDllPath);

                    string directory = Path.GetDirectoryName(mainDllPath)!;
                    string baseModuleName = Path.GetFileNameWithoutExtension(mainDllPath);

                    string[] sidecarSuffixes = { ".Application.dll", ".Infrastructure.dll" };
                    foreach (var suffix in sidecarSuffixes)
                    {
                        string sidecarPath = Path.Combine(directory, baseModuleName + suffix);
                        if (File.Exists(sidecarPath))
                        {
                            if (!bootstrapVerifier.VerifyAssembly(sidecarPath))
                            {
                                Log.Warning(">>> [Security] 附屬組件數位簽章驗證失敗，已隔離: {File}", sidecarPath);
                                QuarantineOrDeleteFile(sidecarPath);
                                continue;
                            }

                            byte[] sidecarBytes = File.ReadAllBytes(sidecarPath);
                            string sidecarPdbPath = Path.ChangeExtension(sidecarPath, ".pdb");
                            byte[]? sidecarPdbBytes = File.Exists(sidecarPdbPath) ? File.ReadAllBytes(sidecarPdbPath) : null;

                            using var sidecarAsmStream = new MemoryStream(sidecarBytes);
                            using var sidecarPdbStream = sidecarPdbBytes != null ? new MemoryStream(sidecarPdbBytes) : null;

                            var sidecarAssembly = currentContext.LoadFromStream(sidecarAsmStream, sidecarPdbStream);

                            if (ContainsControllers(sidecarAssembly))
                            {
                                if (!HasAssemblyPart(partManager, sidecarAssembly))
                                {
                                    partManager.ApplicationParts.Add(new AssemblyPart(sidecarAssembly));
                                    Log.Information(">>> [MVC 註冊] 成功將附屬 Controllers 組件加入 ApplicationParts: {AssemblyName}", sidecarAssembly.FullName);
                                }
                            }
                            Log.Information(">>> [模組上下文] 成功將依賴層組件載入至相同 ALC: {Path}", sidecarPath);
                        }
                    }

                    byte[] assemblyBytes = File.ReadAllBytes(mainDllPath);
                    string pdbPath = Path.ChangeExtension(mainDllPath, ".pdb");
                    byte[]? pdbBytes = File.Exists(pdbPath) ? File.ReadAllBytes(pdbPath) : null;

                    using var asmStream = new MemoryStream(assemblyBytes);
                    using var pdbStream = pdbBytes != null ? new MemoryStream(pdbBytes) : null;

                    var assembly = currentContext.LoadFromStream(asmStream, pdbStream);

                    if (ContainsControllers(assembly))
                    {
                        if (!HasAssemblyPart(partManager, assembly))
                        {
                            partManager.ApplicationParts.Add(new AssemblyPart(assembly));
                            Log.Information(">>> [MVC 註冊] 成功將外掛 Controllers 組件加入 ApplicationParts: {AssemblyName}", assembly.FullName);
                        }
                        else
                        {
                            Log.Warning(">>> [MVC 註冊] 組件 {AssemblyName} 已存在於 ApplicationParts，忽略重複加入以避免 AmbiguousMatchException。", assembly.FullName);
                        }
                    }

                    var types = assembly.GetTypes();
                    var initializerType = types.FirstOrDefault(t => typeof(IModuleInitializer).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                    if (initializerType != null)
                    {
                        Log.Information(">>> 成功找到模組初始化器: {Type}", initializerType.FullName);
                        var initializer = (IModuleInitializer)Activator.CreateInstance(initializerType)!;
                        initializer.RegisterDependencies(services, configuration);

                        InitializerRegistry[initializer.ModuleName] = initializer;
                        LoadedModulesRegistry[initializer.ModuleName] = (currentContext, assembly, initializer);

                        var container = new ModuleAssemblyContainer(assembly, currentContext, initializer.ModuleName);
                        services.AddSingleton(container);

                        string fullDllPath = Path.GetFullPath(mainDllPath);
                        services.AddHostedService(sp => new ModuleDatabaseInitializerHostedService(sp, assembly, initializer.ModuleName, fullDllPath, isHostBuiltin: false));

                        Log.Information("模組 {Name} 依賴註冊設定完成。實體路徑: {Path}", initializer.ModuleName, fullDllPath);
                    }

                    services.AddSingleton(currentContext);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "載入模組失敗: {Dll}", mainDllPath);
                    if (currentContext != null)
                    {
                        try
                        {
                            currentContext.Unload();
                        }
                        catch (Exception ex2)
                        {
                            Log.Warning(ex2, ">>> [ModuleLoader] 卸載載入器內容時發生預期外的例外狀況，已略過。");
                        }
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "初始化模組載入路徑時發生未預期的系統錯誤。");
            throw;
        }
        return services;
    }

    public static async Task UseModularModulesAsync(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _globalPartManager = app.ApplicationServices.GetRequiredService<ApplicationPartManager>();

        foreach (var entry in LoadedModulesRegistry.Values)
        {
            var initializer = entry.Initializer;
            await Task.Run(async () =>
            {
                await initializer.OnApplicationConfigureAsync(app);
            });
        }
    }

    public static void UnloadModule(string moduleName)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);

        if (LoadedModulesRegistry.TryRemove(moduleName, out var tuple))
        {
            try
            {
                if (_globalPartManager is not null)
                {
                    var part = _globalPartManager.ApplicationParts
                        .FirstOrDefault(p => p.Name == tuple.Assembly.GetName().Name);

                    if (part is not null)
                    {
                        _globalPartManager.ApplicationParts.Remove(part);
                    }
                }

                tuple.Context.Unload();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                throw new MigrationException($"動態卸載模組 '{moduleName}' 時發生錯誤。", moduleName, "UnloadModule", ex);
            }
        }
    }

    public static IEnumerable<string> GetLoadedModuleNames()
    {
        return LoadedModulesRegistry.Keys.ToList();
    }

    public static IEnumerable<IModuleInitializer> GetAllInitializers()
    {
        return InitializerRegistry.Values;
    }

    #endregion

    #region Database Registration Methods (Single & Multiple Assemblies)

    /// <summary>
    /// 註冊單一模組與 Controller Metadata 至資料庫。
    /// </summary>
    public static async Task RegisterModuleToDbAsync(
        Assembly assembly,
        string moduleName,
        IServiceProvider serviceProvider,
        bool isHostBuiltin = true)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        string dllPath = assembly.Location;
        await RegisterModuleToDbAsync(new[] { assembly }, moduleName, dllPath, serviceProvider, isHostBuiltin);
    }

    /// <summary>
    /// 註冊單一 Assembly 模組與 Controller Metadata 至資料庫（指定 DLL 路徑）。
    /// </summary>
    public static async Task RegisterModuleToDbAsync(
        Assembly assembly,
        string moduleName,
        string dllPath,
        IServiceProvider serviceProvider,
        bool isHostBuiltin = false)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        await RegisterModuleToDbAsync(new[] { assembly }, moduleName, dllPath, serviceProvider, isHostBuiltin);
    }

    /// <summary>
    /// 註冊多個 Assemblies (系統核心模組) 與 Controller Metadata 至資料庫。
    /// </summary>
    public static async Task RegisterModuleToDbAsync(
        IEnumerable<Assembly> assemblies,
        string moduleName,
        string dllPath,
        IServiceProvider serviceProvider,
        bool isHostBuiltin = false)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var assemblyList = assemblies.Where(a => a != null).ToList();
        if (assemblyList.Count == 0)
        {
            Log.Warning(">>> [RegisterModuleToDbAsync] 傳入的 Assembly 列表為空，忽略註冊作業。模組名稱: {ModuleName}", moduleName);
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var scopedSp = scope.ServiceProvider;

        var moduleRepo = scopedSp.GetRequiredService<IModuleRepository>();
        var controllerRepo = scopedSp.GetRequiredService<IDynamicControllerRepository<ControllerMetadata>>();
        var configuration = scopedSp.GetRequiredService<IConfiguration>();

        string checksum = string.Empty;
        string targetPath = dllPath;

        if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
        {
            try
            {
                using var stream = File.OpenRead(targetPath);
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(stream);
                checksum = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, ">>> [Checksum] 無法讀取檔案計算 Hash: {Path}", targetPath);
            }
        }

        // 1. 解析 ModuleTitle: [ModuleAttribute] -> [AssemblyTitleAttribute] -> moduleName
        var primaryAssembly = assemblyList.First();
        var moduleAttr = primaryAssembly.GetCustomAttribute<ModuleAttribute>();
        var assemblyTitleAttr = primaryAssembly.GetCustomAttribute<AssemblyTitleAttribute>();

        string moduleTitle = !string.IsNullOrWhiteSpace(moduleAttr?.Title)
            ? moduleAttr.Title
            : (!string.IsNullOrWhiteSpace(assemblyTitleAttr?.Title) ? assemblyTitleAttr.Title : moduleName);

        // 寫入/更新當前模組的中繼資料 (包含 ModuleTitle)
        var moduleMeta = new ModuleMetadata
        {
            ModuleName = moduleName,
            ModuleTitle = moduleTitle,
            Version = primaryAssembly.GetName().Version?.ToString() ?? "1.0.0",
            AssemblyPath = string.IsNullOrEmpty(targetPath) ? primaryAssembly.Location : targetPath,
            IsActive = true,
            Checksum = checksum
        };
        await moduleRepo.UpsertAsync(moduleMeta);

        // 2. 僅從傳入的 Assemblies 中精準收集 Controller
        var controllerTypes = new List<Type>();
        foreach (var asm in assemblyList)
        {
            controllerTypes.AddRange(GetControllerTypesFromAssembly(asm));
        }

        controllerTypes = controllerTypes.Distinct().ToList();

        Log.Information(">>> [Controller Scan] 於模組 '{ModuleName}' ({AsmCount} 個 Assembly) 掃描到 {Count} 個原生 Controller 類別。",
            moduleName, assemblyList.Count, controllerTypes.Count);

        var newMetas = ExtractControllerMetadatas(controllerTypes, moduleName, moduleTitle);

        // 3. 寫入/更新 ControllerMetadata
        await controllerRepo.RegisterAsync(moduleName, newMetas);
        Log.Information(">>> [DynamicController] 同步完成：模組 '{Module}' 已寫入 {Count} 筆 ControllerMetadata 至資料庫。", moduleName, newMetas.Count);
    }

    #endregion

    #region Private Helper Methods

    private static bool HasAssemblyPart(ApplicationPartManager partManager, Assembly assembly)
    {
        string? asmName = assembly.GetName().Name;
        if (string.IsNullOrEmpty(asmName)) return false;

        return partManager.ApplicationParts
            .OfType<AssemblyPart>()
            .Any(p => string.Equals(p.Assembly.GetName().Name, asmName, StringComparison.OrdinalIgnoreCase));
    }

    private static ApplicationPartManager GetApplicationPartManager(this IServiceCollection services)
    {
        var sd = services.FirstOrDefault(s => s.ServiceType == typeof(ApplicationPartManager));
        if (sd?.ImplementationInstance is ApplicationPartManager managerInstance)
        {
            return managerInstance;
        }

        var partManager = new ApplicationPartManager();
        services.AddSingleton(partManager);
        return partManager;
    }

    private static bool ContainsControllers(Assembly assembly)
    {
        try
        {
            return Array.Exists(assembly.GetTypes(), type =>
                type.IsPublic &&
                !type.IsAbstract &&
                type.IsClass &&
                (type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
                 type.IsDefined(typeof(ApiControllerAttribute), inherit: true) ||
                 typeof(ControllerBase).IsAssignableFrom(type)));
        }
        catch
        {
            return false;
        }
    }

    private static string GetPluginsDirectory(IConfiguration configuration)
    {
        string? configPath = configuration["ModularSettings:PluginsPath"];
        string relativeOrAbsolutePath = string.IsNullOrWhiteSpace(configPath) ? "plugins" : configPath;

        return Path.IsPathRooted(relativeOrAbsolutePath)
            ? relativeOrAbsolutePath
            : Path.Combine(AppContext.BaseDirectory, relativeOrAbsolutePath);
    }

    private static List<Type> GetControllerTypesFromAssembly(Assembly asm)
    {
        try
        {
            return asm.GetTypes()
                .Where(t => !t.IsAbstract && t.IsClass && t.IsPublic &&
                           (t.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
                            t.IsDefined(typeof(ApiControllerAttribute), inherit: true) ||
                            typeof(ControllerBase).IsAssignableFrom(t)))
                .ToList();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types
                .Where(t => t != null && !t.IsAbstract && t.IsClass && t.IsPublic &&
                            (t.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
                             t.IsDefined(typeof(ApiControllerAttribute), inherit: true) ||
                             typeof(ControllerBase).IsAssignableFrom(t)))
                .Select(t => t!)
                .ToList();
        }
    }

    private static List<ControllerMetadata> ExtractControllerMetadatas(List<Type> controllerTypes, string moduleName, string moduleTitle)
    {
        var newMetas = new List<ControllerMetadata>();

        foreach (var ctrlType in controllerTypes)
        {
            var routeAttrs = ctrlType.GetCustomAttributes<RouteAttribute>(inherit: true).ToList();
            string cleanControllerName = ctrlType.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
                ? ctrlType.Name[..^10]
                : ctrlType.Name;

            string baseRoute = routeAttrs.FirstOrDefault()?.Template ?? $"api/{cleanControllerName}";

            var ctrlTitleAttr = ctrlType.GetCustomAttribute<ControllerTitleAttribute>();
            var ctrlPermAttr = ctrlType.GetCustomAttribute<RequiresPermissionAttribute>();
            var descAttr = ctrlType.GetCustomAttribute<DescriptionAttribute>();
            var orderAttr = ctrlType.GetCustomAttribute<OrderAttribute>();

            string controllerTitle = !string.IsNullOrWhiteSpace(ctrlTitleAttr?.Title)
                ? ctrlTitleAttr.Title
                : cleanControllerName;

            var actions = ctrlType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(m => m.IsPublic &&
                           !m.IsSpecialName &&
                           !m.IsDefined(typeof(NonActionAttribute), inherit: true) &&
                           m.DeclaringType != typeof(object) &&
                           m.DeclaringType != typeof(ControllerBase) &&
                           m.DeclaringType != typeof(Microsoft.AspNetCore.Mvc.Controller));

            foreach (var action in actions)
            {
                string actionName = action.Name;
                string routeTemplate = baseRoute;

                var httpGet = action.GetCustomAttribute<HttpGetAttribute>();
                var httpPost = action.GetCustomAttribute<HttpPostAttribute>();
                var httpPut = action.GetCustomAttribute<HttpPutAttribute>();
                var httpDelete = action.GetCustomAttribute<HttpDeleteAttribute>();
                var httpPatch = action.GetCustomAttribute<HttpPatchAttribute>();

                string? actionSubRoute = httpGet?.Template ?? httpPost?.Template ?? httpPut?.Template ?? httpDelete?.Template ?? httpPatch?.Template;

                if (!string.IsNullOrEmpty(actionSubRoute))
                {
                    routeTemplate = actionSubRoute.StartsWith('/') || actionSubRoute.StartsWith("~/")
                        ? actionSubRoute
                        : $"{baseRoute}/{actionSubRoute}";
                }

                var actionFuncAttr = action.GetCustomAttribute<FunctionAttribute>();
                var actionMenu = action.GetCustomAttribute<MenuAttribute>();
                var actionPerm = action.GetCustomAttribute<RequiresPermissionAttribute>() ?? ctrlPermAttr;
                var actionDesc = action.GetCustomAttribute<DescriptionAttribute>() ?? descAttr;
                var actionOrder = action.GetCustomAttribute<OrderAttribute>() ?? orderAttr;

                string displayName = !string.IsNullOrWhiteSpace(actionFuncAttr?.Title)
                    ? actionFuncAttr.Title
                    : (actionMenu?.Name ?? actionName);

                string icon = actionFuncAttr?.Icon ?? actionMenu?.Icon ?? ctrlTitleAttr?.Icon ?? string.Empty;
                int displayOrder = actionFuncAttr?.Order ?? actionOrder?.Order ?? ctrlTitleAttr?.Order ?? 0;
                string? description = actionFuncAttr?.Description ?? actionDesc?.Description ?? ctrlTitleAttr?.Description;

                newMetas.Add(new ControllerMetadata
                {
                    Id = Guid.NewGuid(),
                    ModuleName = moduleName,
                    ModuleTitle = moduleTitle,
                    ControllerName = ctrlType.Name,
                    ControllerTitle = controllerTitle,
                    ActionName = actionName,
                    DisplayName = displayName,
                    RouteTemplate = routeTemplate,
                    ParentMenuName = controllerTitle,
                    Icon = icon,
                    DisplayOrder = displayOrder,
                    PermissionKey = actionPerm?.PermissionKey ?? string.Empty,
                    Description = description,
                    ControllerTypeName = ctrlType.FullName ?? ctrlType.Name,
                    Version = ctrlType.Assembly.GetName().Version?.ToString() ?? "1.0.0.0",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        return newMetas;
    }

    private static void QuarantineOrDeleteFile(string dllPath)
    {
        try
        {
            var fileInfo = new FileInfo(dllPath);
            if (!fileInfo.Exists) return;

            string quarantineDir = Path.Combine(fileInfo.DirectoryName!, "quarantine");
            Directory.CreateDirectory(quarantineDir);

            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

            string mainDest = Path.Combine(quarantineDir, $"{timestamp}_{fileInfo.Name}");
            if (File.Exists(mainDest)) File.Delete(mainDest);
            File.Move(dllPath, mainDest);

            string baseName = Path.GetFileNameWithoutExtension(dllPath);
            if (baseName.EndsWith(".Application", StringComparison.OrdinalIgnoreCase) ||
                baseName.EndsWith(".Infrastructure", StringComparison.OrdinalIgnoreCase))
            {
                baseName = baseName[..^12];
            }

            var relatedExtensions = new[] { ".dll", ".pdb" };
            var candidateSuffixes = new[] { "", ".Application", ".Infrastructure" };

            foreach (var suffix in candidateSuffixes)
            {
                foreach (var ext in relatedExtensions)
                {
                    string relatedFileName = $"{baseName}{suffix}{ext}";
                    string relatedPath = Path.Combine(fileInfo.DirectoryName!, relatedFileName);

                    if (File.Exists(relatedPath) && !Path.GetFullPath(relatedPath).Equals(Path.GetFullPath(dllPath), StringComparison.OrdinalIgnoreCase))
                    {
                        string relatedDest = Path.Combine(quarantineDir, $"{timestamp}_{relatedFileName}");
                        if (File.Exists(relatedDest)) File.Delete(relatedDest);
                        File.Move(relatedPath, relatedDest);
                    }
                }
            }

            Log.Warning(">>> [Security Incident] 非法或未授權簽章模組及其相關檔案已被強制隔離至: {Path}", quarantineDir);
        }
        catch (IOException ioEx)
        {
            Log.Error(">>> [Security Error] 隔離檔案無法移動，嘗試刪除失敗: {Path}, 錯誤: {Msg}", dllPath, ioEx.Message);
        }
        catch (Exception ex)
        {
            Log.Fatal(">>> [Security Critical] 隔離程序發生嚴重異常: {Msg}", ex.Message);
        }
    }

    #endregion
}