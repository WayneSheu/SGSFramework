using Microsoft.AspNetCore.Http;
using SGSFramework.ModulePlugin.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Abstractions
{
    /// <summary>
    /// 商業模組動態管理應用層服務介面
    public interface IModuleManagementApplicationService
    {
        /// <summary>
        /// 取得所有啟用的模組詳細資訊
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IEnumerable<ModuleDetailResponse>> GetActiveModulesDetailsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 上傳模組檔案
        /// </summary>
        /// <param name="files"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<ModuleUploadResponseDto> UploadModuleAsync(List<IFormFile> files, CancellationToken cancellationToken = default);

        /// <summary>
        /// 切換模組啟用狀態
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="isActive"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<ToggleStatusResponseDto> ToggleModuleStatusAsync(string moduleName, bool isActive, CancellationToken cancellationToken = default);

        /// <summary>
        /// 移除模組
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task RemoveModuleAsync(string moduleName, CancellationToken cancellationToken = default);
  
    }
}
