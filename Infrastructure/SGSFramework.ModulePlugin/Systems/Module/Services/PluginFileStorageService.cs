using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Module.Services
{
    /// <summary>
    /// PluginFileStorageService 提供了插件文件存儲服務的實現，負責刪除模組相關的 DLL 和 PDB 文件。
    /// </summary>
    public class PluginFileStorageService : IPluginFileStorageService
    {
        private readonly ILogger<PluginFileStorageService> _logger;

        public PluginFileStorageService(ILogger<PluginFileStorageService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task DeleteModuleFilesAsync(string moduleName, string? assemblyPath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

            var pluginsDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");
            if (!Directory.Exists(pluginsDirectory))
            {
                return Task.CompletedTask;
            }

            try
            {
                if (!string.IsNullOrEmpty(assemblyPath))
                {
                    var fullPath = Path.IsPathFullyQualified(assemblyPath)
                        ? assemblyPath
                        : Path.Combine(AppContext.BaseDirectory, assemblyPath);

                    DeleteFileIfExists(fullPath);
                    DeleteFileIfExists(Path.ChangeExtension(fullPath, ".pdb"));
                }

                var searchPatternDll = $"{moduleName}*.dll";
                var matchingDlls = Directory.GetFiles(pluginsDirectory, searchPatternDll, SearchOption.TopDirectoryOnly);

                var searchPatternPdb = $"{moduleName}*.pdb";
                var matchingPdbs = Directory.GetFiles(pluginsDirectory, searchPatternPdb, SearchOption.TopDirectoryOnly);

                if (matchingDlls.Length == 0 && moduleName.Contains('.'))
                {
                    var shortName = moduleName.Split('.').Last();
                    matchingDlls = Directory.GetFiles(pluginsDirectory, $"*{shortName}*.dll", SearchOption.TopDirectoryOnly);
                    matchingPdbs = Directory.GetFiles(pluginsDirectory, $"*{shortName}*.pdb", SearchOption.TopDirectoryOnly);
                }

                var allFilesToDelete = matchingDlls.Concat(matchingPdbs).Distinct();

                foreach (var file in allFilesToDelete)
                {
                    DeleteFileIfExists(file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ">>> [Plugin File Storage] 移除模組檔案失敗: {ModuleName}", moduleName);
            }

            return Task.CompletedTask;
        }

        private void DeleteFileIfExists(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    File.Delete(filePath);
                    _logger.LogInformation(">>> [Plugin File Storage] 成功刪除檔案: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, ">>> [Plugin File Storage] 檔案刪除受阻（可能仍被載入鎖定）: {FilePath}", filePath);
            }
        }
    }
}
