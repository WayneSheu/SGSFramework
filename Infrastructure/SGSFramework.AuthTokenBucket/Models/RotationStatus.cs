using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Models
{
    /// <summary>
    /// 雙票輪轉的結果狀態枚舉，表示當前輪轉操作的 outcome
    /// </summary>
    public enum RotationStatus
    {


        /// <summary>
        /// 成功輪轉，生成並核發全新 Token
            /// </summary>
         Success,

        /// <summary>
        /// 處於寬限期內，允許複用上一次快取的 Token，用以應對網絡併發
        /// </summary>
        GracePeriodMatch,

        /// <summary>
        /// 🚨 偵測到惡意重放攻擊（Token 被盜用並重複嘗試換票）
        /// </summary>
        ReplayAttackDetected
    }
}
