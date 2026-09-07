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
    }
}
