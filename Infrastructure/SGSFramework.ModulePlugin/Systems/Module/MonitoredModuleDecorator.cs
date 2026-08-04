using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using SGSFramework.ModulePlugin.Abstractions;

namespace SGSFramework.ModulePlugin.Systems.Module
{
    /// <summary>
    /// 監控模組裝飾器，用於追蹤模組的初始化時間和記憶體使用情況。
    /// </summary>
    /// <param name="_inner"></param>
    public class MonitoredModuleDecorator(IModule _inner) : IModule
    {
        private TimeSpan _loadDuration;
        private long _memoryUsage;

        public string Name => _inner.Name;

        public async Task InitializeAsync()
        {
            var sw = Stopwatch.StartNew();
            await _inner.InitializeAsync();
            sw.Stop();

            _loadDuration = sw.Elapsed;
            _memoryUsage = GC.GetTotalMemory(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ModuleHealthReport GetHealthStatus()
        {
            var report = _inner.GetHealthStatus();
            return report with
            {
                LoadDuration = _loadDuration,
                MemoryUsageBytes = _memoryUsage
            };
        }
    }
}
