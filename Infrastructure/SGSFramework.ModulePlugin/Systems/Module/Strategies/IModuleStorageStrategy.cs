using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Modules;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Module.Strategies
{
    /// <summary>
    /// 定義模組元資料儲存策略的介面，提供模組元資料的 CRUD 操作
    /// </summary>
    /// <typeparam name="TDbContext"></typeparam>
    public interface IModuleStorageStrategy<TDbContext>
    where TDbContext : DbContext
    {
        /// <summary>
        /// 確保建立模組元資料儲存空間。如果該儲存空間不存在，則會建立它。
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task EnsureStorageAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 透過模組名稱取得模組元資料
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<ModuleMetadata?> GetByNameAsync(string moduleName, CancellationToken cancellationToken = default);
        /// <summary>
        /// 取得所有模組元資料
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IReadOnlyList<ModuleMetadata>> GetAllAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 新增或更新模組元資料
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task UpsertAsync(ModuleMetadata module, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 設定模組的啟用狀態
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="isActive"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task SetStatusAsync(string moduleName, bool isActive, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 切換模組的啟用狀態
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task DeleteAsync(string moduleName, CancellationToken cancellationToken = default);
    
    }
}
