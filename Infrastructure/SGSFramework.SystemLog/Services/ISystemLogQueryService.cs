using SGSFramework.SystemLog.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>
    /// 數據庫日誌查詢服務介面
    /// </summary>
    public interface ISystemLogQueryService
    {
        /// <summary>
        /// 分頁查詢系統日誌 (core.SystemLogs)
        /// </summary>
        Task<PagedResult<SystemLogDto>> GetSystemLogsAsync(SystemLogQueryRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 分頁查詢安全性審計日誌 (core.SecurityLog)
        /// </summary>
        Task<PagedResult<SecurityLogDto>> GetSecurityLogsAsync(SecurityLogQueryRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 依 ID 取得系統日誌詳細內容
        /// </summary>
        Task<SystemLogDto?> GetSystemLogByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 依 ID 取得安全性日誌詳細內容
        /// </summary>
        Task<SecurityLogDto?> GetSecurityLogByIdAsync(int id, CancellationToken cancellationToken = default);
    }
}
