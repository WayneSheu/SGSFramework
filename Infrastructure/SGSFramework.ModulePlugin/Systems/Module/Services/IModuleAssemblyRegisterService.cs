using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Module.Services
{
    public interface IModuleAssemblyRegisterService
    {
        /// <summary>
        /// 運行期（Runtime）熱上傳並註冊模組，執行數位簽章驗證、ALC 載入、DB 中繼資料同步與 MVC 路由刷新
        /// </summary>
        Task RegisterAndRefreshModuleAsync(string mainDllPath, CancellationToken cancellationToken = default);
    }
}
