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

        /// <summary>
        /// 撤銷指定使用者在特定裝置的工作階段（強制登出）
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="deviceId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task RevokeSessionAsync(string userId, string deviceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 撤銷指定使用者的所有工作階段（強制登出）
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task RevokeAllUserSessionsAsync(string userId, CancellationToken cancellationToken = default);


    }
}
