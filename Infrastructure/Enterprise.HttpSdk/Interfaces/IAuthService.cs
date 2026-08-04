using System;
using System.Collections.Generic;
using System.Text;

namespace Enterprise.HttpSdk.Interfaces
{
    public interface IAuthService
    {
        /// <summary>
        /// 嘗試刷新存取權杖（Access Token）
        /// </summary>
        /// <returns>回傳刷新後的新 Access Token；若刷新失敗則回傳 null</returns>
        Task<string?> RefreshAccessTokenAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 處理權杖完全失效時的強制登出與轉導頁面
        /// </summary>
        Task HandleLogoutAsync();
    }
}
