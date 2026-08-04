using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Module
{
    /// <summary>
    /// 服務註冊監控器，用於追蹤模組的服務註冊資訊。
    /// </summary>
    public sealed class ServiceRegistryMonitor
    {
        // 儲存格式：模組名稱 -> 註冊清單
        public ConcurrentDictionary<string, List<string>> ModuleRegistrations { get; } = new();

        public void RegisterModuleInfo(string moduleName, string info)
        {
            ModuleRegistrations.GetOrAdd(moduleName, _ => new List<string>()).Add(info);
        }
    }
}
