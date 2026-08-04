using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Abstractions
{
    /// <summary>
    /// 生產環境模組維護控管中心介面
    /// </summary>
    public interface IModuleRegistry
    {
        /// <summary>
        /// 取得目前所有已動態掛載的模組名稱清單
        /// </summary>
        IEnumerable<string> GetLoadedModules();

        /// <summary>
        /// 註冊模組，並將其加入監控列表
        /// </summary>
        /// <param name="module"></param>
        void RegisterModule(IModuleInitializer module);


        ///<summary>
        /// 動態卸載特定模組（釋放記憶體與解綁路由）
        /// </summary>
        /// <param name="moduleName">模組名稱</param>
        void UnloadModule(string moduleName);

        /// <summary>
        /// 取得所有模組的健康狀態報告
        /// </summary>
        /// <returns></returns>
        IEnumerable<ModuleHealthReport> GetAllStatuses();
   
    
    
    }
}
