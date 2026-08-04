using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Abstractions
{
    public interface IModuleUnloader
    {
        Task<bool> UnloadModuleAsync(string moduleName);
    }
}
