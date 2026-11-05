using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    /// <summary>
    /// 動態選單自動掃描與種子同步服務介面
    /// </summary>
    public interface IMenuSeedService
    {
        /// <summary>
        /// 執行動態選單種子同步，將 Attribute 宣告之 IsMenu 節點解析並組裝為 Section -> Group -> Page 三層樹狀結構寫入資料庫
        /// </summary>
        /// <param name="cancellationToken">取消權牌</param>
        /// <returns>非同步任務</returns>
        Task SeedAndSyncMenusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 依單一功能目前的 IsMenu 狀態立即同步其選單頁面節點：
        /// IsMenu = true 時建立（或恢復顯示）Section → Group → Page 節點；IsMenu = false 時隱藏該頁面節點，
        /// 並隱藏因此變空的自動產生群組。僅處理指定功能，不影響其他選單節點。
        /// </summary>
        /// <param name="controllerMetadataId">功能中繼資料識別碼</param>
        /// <param name="cancellationToken">取消權牌</param>
        /// <returns>非同步任務</returns>
        Task SyncFunctionMenuAsync(Guid controllerMetadataId, CancellationToken cancellationToken = default);
    }
}
