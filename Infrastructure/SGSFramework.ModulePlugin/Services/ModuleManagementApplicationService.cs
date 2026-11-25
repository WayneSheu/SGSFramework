// ==========================================
// 檔案路徑: src/SGSFramework/Infrastructure/SGSFramework.ModulePlugin/Services/ModuleManagementApplicationService.cs
// 架構層級: Application Layer
// ==========================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.ModulePlugin.Configurations;
using SGSFramework.ModulePlugin.Abstractions;
using SGSFramework.ModulePlugin.DTOs;
using SGSFramework.ModulePlugin.Systems.Module.Contexts;
using SGSFramework.ModulePlugin.Systems.Module.Loaders;

namespace SGSFramework.ModulePlugin.Services;

public class ModuleManagementApplicationService : IModuleManagementApplicationService
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IModuleRepository _moduleRepo;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModuleManagementApplicationService> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IOptions<ModularOptions> _options;
    private readonly IMemoryCache _cache;
    private readonly IPermissionChangeNotifier _permissionChangeNotifier;

    public ModuleManagementApplicationService(
        IModuleRegistry moduleRegistry,
        IModuleRepository moduleRepo,
        IServiceProvider serviceProvider,
        IHostApplicationLifetime lifetime,
        IOptions<ModularOptions> options,
        IMemoryCache cache,
        IPermissionChangeNotifier permissionChangeNotifier,
        ILogger<ModuleManagementApplicationService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _permissionChangeNotifier = permissionChangeNotifier ?? throw new ArgumentNullException(nameof(permissionChangeNotifier));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _moduleRegistry = moduleRegistry ?? throw new ArgumentNullException(nameof(moduleRegistry));
        _moduleRepo = moduleRepo ?? throw new ArgumentNullException(nameof(moduleRepo));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<ModuleDetailResponse>> GetActiveModulesDetailsAsync(CancellationToken cancellationToken = default)
    {
        // 回傳資料庫中所有模組 (含已停用且未載入者)，讓管理介面可重新啟用；不再依記憶體載入狀態過濾
        var allDbModules = await _moduleRepo.GetAllModulesAsync(cancellationToken).ConfigureAwait(false);

        return allDbModules
            .OrderBy(m => m.ModuleName, StringComparer.OrdinalIgnoreCase)
            .Select(m => new ModuleDetailResponse
            {
                ModuleName = m.ModuleName,
                ModuleTitle = m.ModuleTitle,
                Version = m.Version,
                AssemblyPath = m.AssemblyPath,
                IsActive = m.IsActive,
                LastLoadedAt = m.LastLoadedAt,
                Checksum = m.Checksum
            })
            .ToList();
    }

    public async Task<ModuleUploadResponseDto> UploadModuleAsync(List<IFormFile> files, CancellationToken cancellationToken = default)
    {
        if (files == null || files.Count == 0)
        {
            throw new ArgumentException("上傳的檔案不可為空。", nameof(files));
        }

        var pluginsDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginsDirectory))
        {
            Directory.CreateDirectory(pluginsDirectory);
        }

        var uploadedFilesResult = new List<UploadedFileInfoResponseDto>();
        string? mainDllTempPath = null;
        string? detectedModuleName = null;
        string? detectedVersion = "1.0.0.0";

        try
        {
            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var safeFileName = Path.GetFileName(file.FileName);
                var filePath = Path.Combine(pluginsDirectory, safeFileName);

                if (safeFileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                    !safeFileName.Contains(".Application.dll", StringComparison.OrdinalIgnoreCase) &&
                    !safeFileName.Contains(".Infrastructure.dll", StringComparison.OrdinalIgnoreCase))
                {
                    mainDllTempPath = Path.GetTempFileName();
                    await using (var tempStream = new FileStream(mainDllTempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await file.CopyToAsync(tempStream, cancellationToken).ConfigureAwait(false);
                    }

                    try
                    {
                        var assemblyName = AssemblyName.GetAssemblyName(mainDllTempPath);
                        detectedModuleName = assemblyName.Name ?? Path.GetFileNameWithoutExtension(safeFileName);
                        detectedVersion = assemblyName.Version?.ToString() ?? "1.0.0.0";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "無法從暫存 Assembly [{TempPath}] 讀取 AssemblyName，將改用檔案名稱作爲模組名稱。", mainDllTempPath);
                        detectedModuleName = Path.GetFileNameWithoutExtension(safeFileName);
                    }
                }

                await using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                }

                uploadedFilesResult.Add(new UploadedFileInfoResponseDto
                {
                    FileName = safeFileName,
                    Size = file.Length
                });
            }

            if (string.IsNullOrEmpty(detectedModuleName))
            {
                throw new InvalidOperationException("無法識別上傳的主模組 DLL 檔案名稱。");
            }

            try
            {
                _moduleRegistry.UnloadModule(detectedModuleName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "嘗試卸載既有模組 [{ModuleName}] 時發生非關鍵例外，系統將繼續執行覆蓋作業。", detectedModuleName);
            }

            var mainDllFileName = $"{detectedModuleName}.dll";
            var mainDllFullPath = Path.Combine(pluginsDirectory, mainDllFileName);

            if (File.Exists(mainDllFullPath))
            {
                await RegisterModuleMetadataInIsolationAsync(detectedModuleName, mainDllFullPath, cancellationToken).ConfigureAwait(false);
            }

            // 重新上傳即視為恢復：解除「已卸載」標記（註冊時亦會解除），清除攔截快取並通知線上使用者重載選單
            FunctionStatusRules.UnmarkModuleRemoved(detectedModuleName);
            _cache.Remove(FunctionStatusRules.DisabledFunctionsCacheKey);
            await _permissionChangeNotifier.BroadcastAsync("module-status-changed", cancellationToken).ConfigureAwait(false);

            // 外掛的 DI 註冊與資料庫遷移（含獨立資料庫建置）只在啟動時執行：排定重新啟動，新模組才會實際生效
            var restart = _options.Value.RestartAfterUpload;
            var delay = _options.Value.RestartDelaySeconds;
            if (restart)
            {
                ScheduleRestart(detectedModuleName, "上傳部署", TimeSpan.FromSeconds(delay));
            }

            return new ModuleUploadResponseDto
            {
                Success = true,
                Message = restart
                    ? $"模組 [{detectedModuleName}] 上傳成功，API 將於 {delay} 秒後自動重新啟動，以註冊模組服務並執行資料庫遷移。"
                    : $"模組 [{detectedModuleName}] 上傳成功，已完成元資料與控制器同步；請重新啟動 API 以註冊模組服務並執行資料庫遷移。",
                ModuleName = detectedModuleName,
                Version = detectedVersion,
                Files = uploadedFilesResult,
                RestartScheduled = restart,
                RestartDelaySeconds = restart ? delay : 0
            };
        }
        finally
        {
            if (!string.IsNullOrEmpty(mainDllTempPath) && File.Exists(mainDllTempPath))
            {
                try
                {
                    File.Delete(mainDllTempPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "清理上傳暫存檔 [{TempPath}] 失敗。", mainDllTempPath);
                }
            }
        }
    }

    /// <summary>
    /// 自 plugins 目錄移除模組檔案：{主組件}.dll / .pdb / .deps.json 與其 .Application / .Infrastructure 分層組件。
    /// 安全限制：僅處理「位於 plugins 目錄」的檔案（編譯進主程式資料夾的模組不屬於 plugins，不可刪除）；
    /// 不移除可能被其他模組共用的 *.Contracts.dll 與第三方相依套件。
    /// 檔案若被佔用無法刪除，改名為 *.removed（載入器只掃描 *.dll，不會再載入），並記錄警告。
    /// </summary>
    private void RemoveModuleFilesFromPlugins(string moduleName, string? assemblyPath)
    {
        var pluginsDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "plugins"));
        var baseName = !string.IsNullOrWhiteSpace(assemblyPath)
            ? Path.GetFileNameWithoutExtension(assemblyPath)
            : moduleName;

        // 防止路徑穿越：基底名稱不可含目錄成分
        if (string.IsNullOrWhiteSpace(baseName) || !string.Equals(Path.GetFileName(baseName), baseName, StringComparison.Ordinal))
        {
            _logger.LogWarning("模組 [{ModuleName}] 的檔案名稱無效，略過自 plugins 移除檔案。", moduleName);
            return;
        }

        var mainPath = Path.GetFullPath(Path.Combine(pluginsDirectory, baseName + ".dll"));
        if (!string.Equals(Path.GetDirectoryName(mainPath), pluginsDirectory, StringComparison.OrdinalIgnoreCase) || !File.Exists(mainPath))
        {
            _logger.LogInformation("模組 [{ModuleName}] 在 plugins 目錄內沒有對應檔案（可能編譯於主程式內），略過移除檔案。", moduleName);
            return;
        }

        string[] suffixes =
        [
            ".dll", ".pdb", ".deps.json",
            ".Application.dll", ".Application.pdb",
            ".Infrastructure.dll", ".Infrastructure.pdb"
        ];

        foreach (var suffix in suffixes)
        {
            var path = Path.Combine(pluginsDirectory, baseName + suffix);
            if (!File.Exists(path)) continue;

            try
            {
                File.Delete(path);
                _logger.LogInformation("已自 plugins 移除模組檔案：{File}", Path.GetFileName(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                try
                {
                    File.Move(path, path + ".removed", overwrite: true);
                    _logger.LogWarning(ex, "模組檔案被佔用無法刪除，已改名為 {File}.removed（重新啟動後可自行清除）。", Path.GetFileName(path));
                }
                catch (Exception moveEx)
                {
                    _logger.LogError(moveEx, "無法移除或改名模組檔案 {File}，下次啟動可能仍會載入此模組，請手動刪除。", path);
                }
            }
        }

        if (File.Exists(Path.Combine(pluginsDirectory, baseName + ".Contracts.dll")))
        {
            _logger.LogInformation("模組 [{ModuleName}] 的共用組件 {File}.Contracts.dll 未移除（可能被其他模組使用）。", moduleName, baseName);
        }
    }

    /// <summary>
    /// 於可卸載的隔離載入環境 (ModuleAssemblyLoadContext) 讀取新上傳的主組件，並同步模組 / 控制器中繼資料至資料庫。
    /// 不可使用 Assembly.LoadFrom：它會載入預設載入環境，而主程式若已參考同名組件（例如 SGS.Modules.ORG），
    /// 遞增版本號後同名不同版本無法並存，會拋出 0x80131040（manifest definition does not match the assembly reference）。
    /// 載入作法與啟動時載入器一致（記憶體串流，不鎖檔），完成後立即卸載；模組實際生效仍依賴重新啟動。
    /// </summary>
    private async Task RegisterModuleMetadataInIsolationAsync(string moduleName, string mainDllPath, CancellationToken cancellationToken)
    {
        var alc = new ModuleAssemblyLoadContext(mainDllPath);
        try
        {
            var directory = Path.GetDirectoryName(mainDllPath)!;
            var baseModuleName = Path.GetFileNameWithoutExtension(mainDllPath);

            // 先載入分層組件至同一隔離環境，主組件型別才能完整解析
            foreach (var suffix in new[] { ".Application.dll", ".Infrastructure.dll" })
            {
                var sidecarPath = Path.Combine(directory, baseModuleName + suffix);
                if (!File.Exists(sidecarPath)) continue;

                var sidecarBytes = await File.ReadAllBytesAsync(sidecarPath, cancellationToken).ConfigureAwait(false);
                using var sidecarStream = new MemoryStream(sidecarBytes);
                alc.LoadFromStream(sidecarStream);
            }

            var mainBytes = await File.ReadAllBytesAsync(mainDllPath, cancellationToken).ConfigureAwait(false);
            using var mainStream = new MemoryStream(mainBytes);
            var assembly = alc.LoadFromStream(mainStream);

            using var scope = _serviceProvider.CreateScope();
            await ModuleLoaderExtensions.RegisterModuleToDbAsync(assembly, moduleName, mainDllPath, scope.ServiceProvider).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                alc.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "卸載暫時性的模組載入環境失敗（不影響部署結果）。模組: {ModuleName}", moduleName);
            }
        }
    }

    /// <summary>
    /// 延遲後停止應用程式（讓上傳回應先送達）。IIS 會重新啟動應用程式，啟動時載入新模組並執行其資料庫遷移。
    /// </summary>
    private void ScheduleRestart(string moduleName, string reason, TimeSpan delay)
    {
        _logger.LogWarning("[模組部署] 模組 {ModuleName} 已{Reason}，API 將於 {Delay} 秒後重新啟動以套用模組。", moduleName, reason, delay.TotalSeconds);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, _lifetime.ApplicationStopping).ConfigureAwait(false);
                _lifetime.StopApplication();
            }
            catch (OperationCanceledException)
            {
                // 應用程式已在停止中，不需重複觸發
            }
        });
    }

    public async Task<ToggleStatusResponseDto> ToggleModuleStatusAsync(string moduleName, bool isActive, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        if (!isActive && FunctionStatusRules.IsProtectedModule(moduleName))
        {
            throw new NotSupportedException($"核心模組 [{moduleName}] 涵蓋模組管理與系統管理功能，不可停用。");
        }

        var module = await _moduleRepo.GetModuleByNameAsync(moduleName, cancellationToken).ConfigureAwait(false);
        var wasActive = module?.IsActive ?? false;
        if (module == null)
        {
            throw new KeyNotFoundException($"無法啟用模組 [{moduleName}]：資料庫中找不到對應的模組元資料紀錄。");
        }

        if (isActive)
        {
            if (string.IsNullOrEmpty(module.AssemblyPath) || !File.Exists(module.AssemblyPath))
            {
                var fallbackPath = Path.Combine(AppContext.BaseDirectory, "plugins", $"{moduleName}.dll");
                if (File.Exists(fallbackPath))
                {
                    module.AssemblyPath = fallbackPath;
                    await _moduleRepo.UpsertAsync(module, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw new FileNotFoundException($"無法啟用模組 [{moduleName}]：找不到對應的 DLL 檔案。", fallbackPath);
                }
            }
        }

        await _moduleRepo.ToggleModuleStatusAsync(moduleName, isActive, cancellationToken).ConfigureAwait(false);

        // 停用模組僅更新資料庫狀態 (組件仍載入於記憶體)：清除 API 攔截快取使其立即生效，並通知所有線上使用者重新載入選單
        _cache.Remove(FunctionStatusRules.DisabledFunctionsCacheKey);
        await _permissionChangeNotifier.BroadcastAsync("module-status-changed", cancellationToken).ConfigureAwait(false);
        _logger.LogWarning("模組 [{ModuleName}] 狀態已變更為 {IsActive}，已廣播通知線上使用者。", moduleName, isActive);

        // 重新啟用：停用期間模組的背景工作、排程與快取可能已過時，重新啟動 API 以完整重新初始化模組 (DI、遷移、HostedService)
        var restart = isActive && !wasActive && _options.Value.RestartAfterUpload;
        var delay = _options.Value.RestartDelaySeconds;
        if (restart)
        {
            ScheduleRestart(moduleName, "重新啟用", TimeSpan.FromSeconds(delay));
        }

        return new ToggleStatusResponseDto
        {
            Success = true,
            ModuleName = moduleName,
            IsActive = isActive,
            Message = restart
                ? $"模組 [{moduleName}] 已啟用，API 將於 {delay} 秒後自動重新啟動以重新初始化模組。"
                : isActive && !wasActive
                    ? $"模組 [{moduleName}] 已啟用；未設定自動重新啟動，請手動重新啟動 API 以完整重新初始化模組。"
                    : $"模組 [{moduleName}] 已{(isActive ? "啟用" : "停用")}。",
            RestartScheduled = restart,
            RestartDelaySeconds = restart ? delay : 0
        };
    }

    public async Task RemoveModuleAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        if (FunctionStatusRules.IsProtectedModule(moduleName))
        {
            throw new NotSupportedException($"核心模組 [{moduleName}] 涵蓋模組管理與系統管理功能，不可卸載。");
        }

        // 移除資料列前先取得其組件路徑，供後續自 plugins 移除檔案
        var existing = await _moduleRepo.GetModuleByNameAsync(moduleName, cancellationToken).ConfigureAwait(false);

        try
        {
            _moduleRegistry.UnloadModule(moduleName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "卸載記憶體中模組 [{ModuleName}] 時拋出例外，將繼續清理 DB 元資料。", moduleName);
        }

        await _moduleRepo.RemoveModuleAsync(moduleName, cancellationToken).ConfigureAwait(false);

        // 自 plugins 目錄移除模組檔案，避免下次啟動又被載入器重新載入
        RemoveModuleFilesFromPlugins(moduleName, existing?.AssemblyPath);

        // 卸載後選單與 API 必須立即停用：直接編譯進主程式的模組（如 ORG）控制器不會隨之消失，
        // 故以記憶體標記讓 FunctionStatusAuthorizationFilter / 選單視同停用，並通知線上使用者重載選單。
        FunctionStatusRules.MarkModuleRemoved(moduleName);
        _cache.Remove(FunctionStatusRules.DisabledFunctionsCacheKey);
        await _permissionChangeNotifier.BroadcastAsync("module-status-changed", cancellationToken).ConfigureAwait(false);
        _logger.LogWarning("模組 [{ModuleName}] 已卸載：模組資料列已移除、檔案已自 plugins 移除，其選單與功能資料列已軟停用，API 立即攔截（重新部署後恢復）。", moduleName);
    }
}