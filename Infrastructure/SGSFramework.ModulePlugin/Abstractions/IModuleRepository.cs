using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Utilities;
using SGSFramework.Core.Abstractions.Entities.Modules;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Abstractions
{
    /// <summary>
    /// 
    /// </summary>
    public interface IModuleRepository
    {
        /// <summary>
        /// 取得指定名稱的模組元資料
        /// </summary>
        /// <param name="moduleName"></param>
        /// <returns></returns>
        Task<ModuleMetadata?> GetModuleByNameAsync(string moduleName);
        
        /// <summary>
        /// 新增或更新模組元資料
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        Task UpsertAsync(ModuleMetadata module);
        
        /// <summary>
        /// 設定模組狀態
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="isActive"></param>
        /// <returns></returns>
        Task SetModuleStatusAsync(string moduleName, bool isActive);
        
        /// <summary>
        /// 切換模組狀態
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="isActive"></param>
        /// <returns></returns>
        Task ToggleModuleStatusAsync(string moduleName, bool isActive);
        
        /// <summary>
        /// 取得所有模組的清單資料
        /// </summary>
        Task<IEnumerable<ModuleMetadata>> GetAllModulesAsync();

        /// <summary>
        /// 移除指定名稱的模組元資料
        /// </summary>
        /// <param name="moduleName"></param>
        /// <returns></returns>
        Task RemoveModuleAsync(string moduleName);
    }
}
