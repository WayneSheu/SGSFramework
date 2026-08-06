using Microsoft.AspNetCore.Http;
using SGSFramework.ModulePlugin.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Abstractions
{
    /// <summary>
    /// 商業模組動態管理應用層服務介面
    /// </summary>
    public interface IModuleManagementApplicationService
    {
        /// <summary>
        /// 獲取所有已載入模組之詳細資訊
        /// </summary>
        Task<IEnumerable<ModuleDetailResponse>> GetActiveModulesDetailsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 處理模組檔案上傳、檔案寫入、動態卸載與控制器元資料同步
        /// </summary>
        Task<ModuleUploadResponseDto> UploadModuleAsync(List<IFormFile> files, CancellationToken cancellationToken = default);

        /// <summary>
        /// 切換指定模組之啟用狀態
        /// </summary>
        Task<ToggleStatusResponseDto> ToggleModuleStatusAsync(string moduleName, bool isActive, CancellationToken cancellationToken = default);

        /// <summary>
        /// 動態卸載並刪除指定模組元資料
        /// </summary>
        Task RemoveModuleAsync(string moduleName, CancellationToken cancellationToken = default);
    }
}
