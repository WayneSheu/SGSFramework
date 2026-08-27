using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Alert.Abstractions
{
    /// <summary>
    /// 告警發送管道策略介面 (Strategy Pattern)
    /// </summary>
    public interface IAlertNotificationChannel
    {
        /// <summary>
        /// 管道識別名稱
        /// </summary>
        string ChannelName { get; }

        /// <summary>
        /// 檢查當前管道組態是否有效
        /// </summary>
        bool IsConfigured();

        /// <summary>
        /// 非同步發送告警訊息
        /// </summary>
        Task<bool> SendAlertAsync(AlertMessageModel alert, CancellationToken cancellationToken);
    }
}
