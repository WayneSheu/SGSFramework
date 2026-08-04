using Microsoft.Extensions.DependencyModel;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Module.Loaders
{
    public static class SystemModuleScanner
    {
        /// <summary>
        /// 自動掃描目前專案所引用的所有 SGSFramework.* Assembly（排除動態外掛）
        /// </summary>
        public static List<Assembly> ScanSystemFrameworkAssemblies()
        {
            var assemblies = new List<Assembly>();
            var loadedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 策略 1: 從 DependencyContext 抓取引用的相依組件 (最完整)
            var context = DependencyContext.Default;
            if (context != null)
            {
                var targetRuntimeLibraries = context.RuntimeLibraries
                    .Where(lib => lib.Name.StartsWith("SGSFramework", StringComparison.OrdinalIgnoreCase) &&
                                  !lib.Name.StartsWith("SGS.Modules", StringComparison.OrdinalIgnoreCase));

                foreach (var library in targetRuntimeLibraries)
                {
                    try
                    {
                        var assembly = Assembly.Load(new AssemblyName(library.Name));
                        if (!loadedAssemblyNames.Contains(assembly.FullName!))
                        {
                            assemblies.Add(assembly);
                            loadedAssemblyNames.Add(assembly.FullName!);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, ">>> [SystemModuleScanner] 無法載入框架組件: {Name}", library.Name);
                    }
                }
            }

            // 策略 2: 從 AppDomain 補強目前已載入記憶體的 Assembly
            var currentDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic &&
                            !string.IsNullOrEmpty(a.FullName) &&
                            a.FullName.StartsWith("SGSFramework", StringComparison.OrdinalIgnoreCase) &&
                            !a.FullName.StartsWith("SGS.Modules", StringComparison.OrdinalIgnoreCase));

            foreach (var asm in currentDomainAssemblies)
            {
                if (asm.FullName != null && !loadedAssemblyNames.Contains(asm.FullName))
                {
                    assemblies.Add(asm);
                    loadedAssemblyNames.Add(asm.FullName);
                }
            }

            Log.Information(">>> [SystemModuleScanner] 自動掃描完畢，共找到 {Count} 個系統框架組件 (SGSFramework.*)。", assemblies.Count);
            return assemblies;
        }
    }
}
