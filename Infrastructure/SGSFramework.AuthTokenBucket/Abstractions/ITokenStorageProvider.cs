using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.AuthTokenBucket.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    /// <summary>
    /// 這個介面定義了與 Token 存儲相關的操作，包括驗證和旋轉 Token、撤銷裝置會話、撤銷其他會話以及強制執行最大裝置限制等方法。
    /// </summary>
    public interface ITokenStorageProvider
    {

        /// <summary>
        /// 驗證用戶是否具有指定裝置的會話。這個方法通常在用戶嘗試訪問受保護資源時被調用，以確保用戶的身份和裝置的安全性。
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        Task<UserRefreshToken?> GetActiveSessionAsync(string userId, string deviceId);

        /// <summary>
        /// 保存初始會話資訊，當用戶首次登入或註冊時，將相關的 Token 資訊存儲起來，以便後續的驗證和管理。
        /// 登入保底/覆蓋機制：保存初始安全工作階段，若裝置已存在則執行 Upsert 覆蓋
        /// </summary>
        /// <param name="tokenEntity"></param>
        /// <returns></returns>
        Task SaveInitialSessionAsync(UserRefreshToken tokenEntity);

        /// <summary>
        /// 驗證舊的 Token 是否有效，並在驗證成功後將其旋轉為新的 Token。這個方法通常在用戶嘗試使用舊的 Token 進行身份驗證時被調用，以確保安全性和防止重放攻擊。
        ///  高併發雙向安全換票與自動旋轉校驗
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="deviceId"></param>
        /// <param name="oldTokenHash"></param>
        /// <param name="newTokenHash"></param>
        /// <param name="expiresAt"></param>
        /// <param name="gracePeriodSeconds">寬限期秒數，允許在此期間內的併發請求繼續使用舊的 Token</param>
        /// <returns></returns>
        Task<RotationResult?> ValidateAndRotateTokenAsync(
            string userId,
            string deviceId,
            string oldTokenHash,
            string newTokenHash,
            DateTime expiresAt,
            int gracePeriodSeconds);

        /// <summary>
        /// 用戶主動控制：一鍵凍結該用戶所有裝置的工作階段，並強制清空 TokenHash 與快取，使現存所有 Refresh Token 立即失效（提供給管理員或特殊情境使用，確保原子性更新）
        /// 資安主動防禦 - 緊急凍結並註銷該用戶所有裝置的工作階段
        /// 安緊急防禦熔斷：凍結並抹除所有線上儲存的長效換票雜湊
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="riskReason"></param>
        /// <returns></returns>
        Task<bool> FreezeAndRevokeAllSessionsAsync(string userId, string riskReason);

        /// <summary>
        /// 實名補償身分重驗/密碼變更完成與環境清理
        /// </summary>
        Task<bool> RemediateAndClearFrozenSessionsAsync(string userId);






        /// <summary>
        /// 撤銷特定裝置的會話，當用戶需要強制登出某個裝置或發現可疑活動時，可以調用這個方法來撤銷該裝置的會話，從而保護用戶帳戶的安全。
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        Task RevokeDeviceSessionAsync(string userId, string deviceId);

        /// <summary>
        /// 撤銷用戶的所有其他會話，除了當前裝置之外。這個方法通常在用戶需要強制登出所有其他裝置時被調用，以確保帳戶安全，特別是在用戶發現帳戶可能被盜用的情況下。
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="currentDeviceId"></param>
        /// <returns></returns>
        Task RevokeAllOtherSessionsAsync(string userId, string currentDeviceId);

        /// <summary>
        /// 強制執行最大裝置限制，當用戶嘗試登入新的裝置時，如果已經達到最大裝置數量限制，則可以調用這個方法來強制撤銷最舊的會話，以便為新的裝置騰出空間。
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="maxDeviceCount"></param>
        /// <returns></returns>
        Task EnforceMaxDeviceLimitAsync(string userId, int maxDeviceCount);

        /// <summary>
        /// 移除特定裝置的會話，這個方法可以用於清理過期或無效的會話，或者在用戶主動登出某個裝置時被調用，以確保資料庫中的會話資訊保持最新和準確。
        /// </summary>
        /// <param name = "userId" ></ param >
        /// < param name="deviceId"></param>
        /// <returns></returns>
        Task<bool> RemoveSessionAsync(string userId, string deviceId);

        /// <summary>
        /// 移除用戶的所有會話，這個方法可以用於在用戶主動登出所有裝置時被調用，或者在需要清理用戶的所有會話資訊時使用，以確保資料庫中的會話資訊被完全清除。
        /// </summary>
        /// <param name = "userId" ></ param >
        /// < returns ></ returns >
        Task<bool> RemoveAllSessionsAsync(string userId);


    }
}
