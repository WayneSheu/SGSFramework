using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    public interface IUserRefreshTokenRepository
    {
        /// <summary>
        /// 依據活動觀測視窗，計算目前系統中的活躍在線不重複人數
        /// </summary>
        /// <param name="activityWindowMinutes">活動時間觀測視窗（分鐘）</param>
        /// <returns>活躍在線人數</returns>
        Task<int> GetActiveOnlineUserCountAsync(int activityWindowMinutes);
    }
}
