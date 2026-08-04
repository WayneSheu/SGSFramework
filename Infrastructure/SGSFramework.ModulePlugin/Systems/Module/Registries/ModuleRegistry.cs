using SGSFramework.ModulePlugin.Abstractions;
using System.Collections.Concurrent;

namespace SGSFramework.ModulePlugin.Systems.Module.Registries
{

    /// <summary>
    /// 模組註冊表，用於管理已加載的模組及其健康狀態
    /// </summary>
    public class ModuleRegistry : IModuleRegistry
    {
        // 使用 ConcurrentDictionary 確保執行緒安全
        private readonly ConcurrentDictionary<string, IModuleInitializer> _modules = new();
        private readonly ConcurrentDictionary<string, ModuleHealthReport> _healthReports = new();

        public void RegisterModule(IModuleInitializer module)
        {
            if (_modules.TryAdd(module.ModuleName, module))
            {
                // 在此處加入監控掛鉤 (Hook)
                _healthReports[module.ModuleName] = new ModuleHealthReport(module.ModuleName, TimeSpan.Zero, true, 0);
            }
        }

        public IEnumerable<string> GetLoadedModules() => _modules.Keys;

        public void UnloadModule(string name)
        {
            _modules.TryRemove(name, out _);
            _healthReports.TryRemove(name, out _);
        }

        // 提供給 Monitor 服務使用的唯讀資料
        public IEnumerable<ModuleHealthReport> GetHealthReports() => _healthReports.Values;

        // 提供給 Monitor 服務使用的唯讀資料，直接從模組取得最新狀態
        public IEnumerable<ModuleHealthReport> GetAllStatuses()
        {
            // 透過 .Values 取得字典中的模組實例，並呼叫其方法
            return _modules.Values.Select(m => m.GetHealthStatus());
        }
    }
}
