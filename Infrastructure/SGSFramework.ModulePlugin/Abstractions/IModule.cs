using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Abstractions
{
    public interface IModule
    {
        string Name { get; }
        Task InitializeAsync();
        ModuleHealthReport GetHealthStatus();
    }
}
