using Microsoft.Extensions.Logging;
using SGSFramework.ModulePlugin.Abstractions;
using System;
using System.Collections.Generic;
using System.Runtime.Loader;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Module.Loaders
{
    public class ModuleUnloader : IModuleUnloader
    {
        private readonly IModuleRepository _moduleRepo;
        private readonly ILogger<ModuleUnloader> _logger;
        private readonly IEnumerable<AssemblyLoadContext> _loadContexts; 

        public ModuleUnloader(IModuleRepository moduleRepo, ILogger<ModuleUnloader> logger)
        {
            _moduleRepo = moduleRepo;
            _logger = logger;
        }

        public async Task<bool> UnloadModuleAsync(string moduleName)
        {
            try
            {
                _logger.LogInformation(">>> 開始卸載模組: {ModuleName}", moduleName);

                // 1. 原子化停用：更新 DB 狀態
                await _moduleRepo.SetModuleStatusAsync(moduleName, false);

                // 2. 執行記憶體卸載 (針對 AssemblyLoadContext)
                var context = AssemblyLoadContext.All
                    .FirstOrDefault(c => c.Name == moduleName);

                if (context != null)
                {
                    context.Unload();
                    _logger.LogInformation(">>> 模組 {ModuleName} 記憶體已卸載。", moduleName);
                }
                else
                {
                    _logger.LogWarning(">>> 未找到模組 {ModuleName} 的載入上下文。", moduleName);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ">>> 卸載模組 {ModuleName} 時發生錯誤。", moduleName);
                return false;
            }
        }
    }
}
