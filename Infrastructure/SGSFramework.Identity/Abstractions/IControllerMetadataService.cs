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
        /// <param name="includeInactive">是否包含已停用的功能 (功能管理頁面需顯示停用項目以便重新啟用)</param>
        /// <param name="cancellationToken">非同步取消權牌</param>
        /// <returns>控制器中繼資料清單集合</returns>
        Task<IEnumerable<ControllerMetadataDto>> GetAllControllerMetadatasAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新指定功能之啟用狀態（停用時必填原因，並寫入稽核紀錄、廣播通知所有連線使用者）
        /// </summary>
        /// <param name="id">中繼資料識別碼 (Guid)</param>
        /// <param name="change">狀態變更內容（目標狀態、原因、公告、預計恢復時間、來源 IP）</param>
        /// <param name="cancellationToken">非同步取消權牌</param>
        /// <returns>更新後的中繼資料 DTO</returns>
        Task<ControllerMetadataDto> UpdateFunctionStatusAsync(Guid id, FunctionStatusChange change, CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得指定功能的狀態異動歷程（新到舊，最多 200 筆）
        /// </summary>
        Task<IReadOnlyList<FunctionStatusLogDto>> GetFunctionStatusLogsAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得目前所有手動停用功能的公告，供全體使用者查看
        /// </summary>
        Task<IReadOnlyList<FunctionNoticeDto>> GetActiveNoticesAsync(CancellationToken cancellationToken = default);


    }



}
