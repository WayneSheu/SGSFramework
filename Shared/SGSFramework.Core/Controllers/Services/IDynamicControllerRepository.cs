using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Modules;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Controllers.Services
{ 
    /// <summary>
    /// 動態控制器註冊倉儲介面
    /// </summary>
    /// <typeparam name="T">實作 IControllerMetadata 之實體類別</typeparam>
    public interface IDynamicControllerRepository<T> where T : class, IControllerMetadata
    {
        Task RegisterAsync(string moduleName, IEnumerable<T> controllers);
        Task UnregisterByModuleAsync(string moduleName);
        Task<IEnumerable<T>> GetActiveControllersAsync();
        Task<IEnumerable<T>> GetAllActiveAsync();
    }
}
