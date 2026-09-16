using SGSFramework.Identity.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Abstractions
{
    /// <summary>
    /// 系統控制器中繼資料服務介面
    /// </summary>
    public interface IControllerMetadataService
    {
        /// <summary>
        /// 取得系統所有控制器中繼資料清單
        /// </summary>
        /// <param name="cancellationToken">非同步取消權牌</param>
        /// <returns>控制器中繼資料清單集合</returns>
        Task<IEnumerable<ControllerMetadataDto>> GetAllControllerMetadatasAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新指定功能/控制器中繼資料之啟用狀態
        /// </summary>
        /// <param name="id">中繼資料識別碼 (Guid)</param>
        /// <param name="isActive">啟用狀態</param>
        /// <param name="reason">變更原因</param>
        /// <param name="cancellationToken">非同步取消權牌</param>
        /// <returns>更新後的中繼資料 DTO</returns>
        Task<ControllerMetadataDto> UpdateFunctionStatusAsync(Guid id, bool isActive, string? reason = null, CancellationToken cancellationToken = default);


    }



}
