using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGSFramework.ModulePlugin.Abstractions;
using SGSFramework.ModulePlugin.DTOs;
using SGSFramework.ModulePlugin.Systems.Module.Loaders;

namespace SGSFramework.ModulePlugin.Services;

public class ModuleManagementApplicationService : IModuleManagementApplicationService
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IModuleRepository _moduleRepo;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModuleManagementApplicationService> _logger;

    public ModuleManagementApplicationService(
        IModuleRegistry moduleRegistry,
        IModuleRepository moduleRepo,
        IServiceProvider serviceProvider,
        ILogger<ModuleManagementApplicationService> logger)
    {
        _moduleRegistry = moduleRegistry ?? throw new ArgumentNullException(nameof(moduleRegistry));
        _moduleRepo = moduleRepo ?? throw new ArgumentNullException(nameof(moduleRepo));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<ModuleDetailResponse>> GetActiveModulesDetailsAsync(CancellationToken cancellationToken = default)
    {
        var loadedModuleNames = _moduleRegistry.GetLoadedModules()?.ToList() ?? new List<string>();
        var allDbModules = await _moduleRepo.GetAllModulesAsync(cancellationToken);

        return allDbModules
            .Where(m => loadedModuleNames.Contains(m.ModuleName, StringComparer.OrdinalIgnoreCase) || m.IsActive)
            .Select(m => new ModuleDetailResponse
            {
                ModuleName = m.ModuleName,
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
                        await file.CopyToAsync(tempStream, cancellationToken);
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
                    await file.CopyToAsync(fileStream, cancellationToken);
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
                var assembly = Assembly.LoadFrom(mainDllFullPath);
                using var scope = _serviceProvider.CreateScope();
                await ModuleLoaderExtensions.RegisterModuleToDbAsync(assembly, detectedModuleName, mainDllFullPath, scope.ServiceProvider);
            }

            return new ModuleUploadResponseDto
            {
                Success = true,
                Message = $"模組 [{detectedModuleName}] 相關檔案上傳成功，已完成元資料與控制器同步。",
                ModuleName = detectedModuleName,
                Version = detectedVersion,
                Files = uploadedFilesResult
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

    public async Task<ToggleStatusResponseDto> ToggleModuleStatusAsync(string moduleName, bool isActive, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var module = await _moduleRepo.GetModuleByNameAsync(moduleName, cancellationToken);
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
                    await _moduleRepo.UpsertAsync(module, cancellationToken);
                }
                else
                {
                    throw new FileNotFoundException($"無法啟用模組 [{moduleName}]：找不到對應的 DLL 檔案。", fallbackPath);
                }
            }
        }

        await _moduleRepo.ToggleModuleStatusAsync(moduleName, isActive, cancellationToken);

        return new ToggleStatusResponseDto
        {
            Success = true,
            ModuleName = moduleName,
            IsActive = isActive
        };
    }

    public async Task RemoveModuleAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        try
        {
            _moduleRegistry.UnloadModule(moduleName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "卸載記憶體中模組 [{ModuleName}] 時拋出例外，將繼續清理 DB 元資料。", moduleName);
        }

        await _moduleRepo.RemoveModuleAsync(moduleName, cancellationToken);
    }
}
