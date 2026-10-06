// Path: src/SGSFramework/Infrastructure/SGSFramework.SPAModulePlugin/Infrastructure/Services/SPAModuleStorageService.cs
namespace SGSFramework.SPAModulePlugin.Infrastructure.Services;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SGSFramework.SPAModulePlugin.Application.Abstractions;
using SGSFramework.SPAModulePlugin.Application.DTOs;
using SGSFramework.SPAModulePlugin.Domain.Enums;
using SGSFramework.SPAModulePlugin.Domain.ValueObjects;
using System.IO.Compression;
using System.Text.Json;

public sealed class SPAModuleStorageService : ISPAModuleStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SPAModuleStorageService> _logger;

    public SPAModuleStorageService(IWebHostEnvironment env, ILogger<SPAModuleStorageService> logger)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SPAModuleUploadResponseDto> DeployModulePackageAsync(
        SPAFrameworkType frameworkType,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken = default)
    {
        if (files == null || files.Count == 0)
        {
            throw new ArgumentException("上傳檔案清單不可為空。", nameof(files));
        }

        var firstFile = files[0];
        // 模組名稱：若為 Zip 取 Zip 檔名，若為多個單檔取首個檔名（如 index）
        var moduleName = Path.GetFileNameWithoutExtension(firstFile.FileName);
        var targetPhysicalDir = GetTargetPhysicalDirectory(frameworkType, moduleName);

        try
        {
            if (Directory.Exists(targetPhysicalDir))
            {
                Directory.Delete(targetPhysicalDir, recursive: true);
            }
            Directory.CreateDirectory(targetPhysicalDir);

            int processedFilesCount = 0;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (file.Length == 0) continue;

                var fileExt = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (fileExt == ".zip")
                {
                    using var stream = file.OpenReadStream();
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue;

                        var destinationPath = Path.GetFullPath(Path.Combine(targetPhysicalDir, entry.FullName));
                        var baseDirPath = Path.GetFullPath(targetPhysicalDir);

                        if (!destinationPath.StartsWith(baseDirPath, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException($"安全違規：解壓縮檔案路徑超出目標根目錄 ({entry.FullName})");
                        }

                        var entryDir = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(entryDir) && !Directory.Exists(entryDir))
                        {
                            Directory.CreateDirectory(entryDir);
                        }

                        entry.ExtractToFile(destinationPath, overwrite: true);
                        processedFilesCount++;
                    }
                }
                else
                {
                    var destinationPath = Path.Combine(targetPhysicalDir, Path.GetFileName(file.FileName));
                    using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                    await file.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
                    processedFilesCount++;
                }
            }

            // 核心救援點：檢查資料夾內是否存在 manifest.json，若無則自動產生基礎 Manifest 檔
            var manifestPath = Path.Combine(targetPhysicalDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                var relativePath = GetRelativeWebPath(frameworkType, moduleName);
                var entryJs = Directory.GetFiles(targetPhysicalDir, "*.js").Select(Path.GetFileName).FirstOrDefault() ?? "index.js";
                var entryCss = Directory.GetFiles(targetPhysicalDir, "*.css").Select(Path.GetFileName).FirstOrDefault() ?? "index.css";
               
                // 建立自動補全的 fallbackManifest，需滿足所有 required 屬性
                var fallbackManifest = new SPAModuleManifest
                {
                    ModuleId = moduleName,
                    DisplayName = moduleName,
                    FrameworkType = frameworkType,
                    EntryPoint = entryJs,
                    RoutePath = $"/{moduleName}",
                    RequiredPermission = "SYSTEM.SPAMODULE.READ",
                    FileHash = string.Empty, // 若後續有校驗需求，可填入 SHA256 雜湊值
                    DependentAssets = string.IsNullOrEmpty(entryCss)
                        ? Array.Empty<string>()
                        : new[] { entryCss }
                };

                var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(fallbackManifest, options);
                await File.WriteAllBytesAsync(manifestPath, jsonBytes, cancellationToken).ConfigureAwait(false);
                processedFilesCount++;
            }

            var deployedRelativePath = GetRelativeWebPath(frameworkType, moduleName);
            _logger.LogInformation("SPA 模組 [{ModuleName}] ({Framework}) 部署成功（已補全 Manifest），共 {Count} 個檔案。",
                moduleName, frameworkType, processedFilesCount);

            return new SPAModuleUploadResponseDto
            {
                ModuleName = moduleName,
                FrameworkType = frameworkType,
                DeployedRelativePath = deployedRelativePath,
                ProcessedFilesCount = processedFilesCount,
                UploadedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "部署 SPA 模組 [{ModuleName}] 發生例外，執行清理。", moduleName);
            if (Directory.Exists(targetPhysicalDir))
            {
                try { Directory.Delete(targetPhysicalDir, recursive: true); } catch { }
            }
            throw;
        }
    }

    public Task<bool> DeleteModuleDirectoryAsync(
        string moduleName,
        SPAFrameworkType frameworkType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var targetPhysicalDir = GetTargetPhysicalDirectory(frameworkType, moduleName);

        if (!Directory.Exists(targetPhysicalDir))
        {
            _logger.LogWarning("欲刪除的 SPA 模組目錄不存在：{Path}", targetPhysicalDir);
            return Task.FromResult(false);
        }

        try
        {
            Directory.Delete(targetPhysicalDir, recursive: true);
            _logger.LogInformation("已成功移除 SPA 模組實體目錄：{Path}", targetPhysicalDir);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除 SPA 模組目錄 [{Path}] 時發生系統錯誤。", targetPhysicalDir);
            throw;
        }
    }

    private string GetTargetPhysicalDirectory(SPAFrameworkType frameworkType, string moduleName)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");

        return frameworkType switch
        {
            SPAFrameworkType.Vue3 => Path.Combine(webRoot, "modules", moduleName),
            SPAFrameworkType.BlazorWasm => Path.Combine(webRoot, "blazor", "dynamic-modules", moduleName),
            _ => throw new ArgumentOutOfRangeException(nameof(frameworkType), frameworkType, "不支援的前端框架類型。")
        };
    }

    private static string GetRelativeWebPath(SPAFrameworkType frameworkType, string moduleName)
    {
        return frameworkType switch
        {
            SPAFrameworkType.Vue3 => $"/modules/{moduleName}/",
            SPAFrameworkType.BlazorWasm => $"/blazor/dynamic-modules/{moduleName}/",
            _ => $"/{moduleName}/"
        };
    }
}