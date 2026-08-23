using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Module.Services
{
    /// <summary>
    /// 定義插件文件存儲服務的介面，提供刪除模組相關文件的功能
    /// </summary>
    public interface IPluginFileStorageService
    {
        Task DeleteModuleFilesAsync(string moduleName, string? assemblyPath, CancellationToken cancellationToken = default);
    }
}
