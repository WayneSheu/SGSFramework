using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Controller.Services
{
    public interface IControllerScannerService<TDbContext> where TDbContext : DbContext
    {
        Task ScanAndRegisterAsync(IEnumerable<Assembly> assemblies);
    }
}
