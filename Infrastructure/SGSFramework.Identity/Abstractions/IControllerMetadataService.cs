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
        /// <param name="cancellationToken">異步取消權牌</param>
        /// <returns>控制器中繼資料集合</returns>
        Task<IEnumerable<ControllerMetadataDto>> GetAllControllerMetadatasAsync(CancellationToken cancellationToken = default);
    }
}
