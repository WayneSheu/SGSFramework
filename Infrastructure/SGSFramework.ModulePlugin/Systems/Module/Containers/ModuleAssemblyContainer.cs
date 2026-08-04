using SGSFramework.ModulePlugin.Systems.Module.Contexts;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Module.Containers
{
    /// <summary>
    /// 模組組件容器，封裝模組的 Assembly 與對應的 ModuleAssemblyLoadContext
    /// </summary>
    public sealed class ModuleAssemblyContainer
    {
        public Assembly Assembly { get; }
        public ModuleAssemblyLoadContext Context { get; } // 關聯對應的 Context
        public string ModuleName { get; }

        public ModuleAssemblyContainer(Assembly assembly, ModuleAssemblyLoadContext context, string name)
        {
            Assembly = assembly;
            Context = context;
            ModuleName = name;
        }
    }
}
