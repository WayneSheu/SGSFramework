using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Notifications
{
    /// <summary>
    /// 告警通知發送策略介面 (策略模式)
    /// </summary>
    public interface INotificationStrategy
    {
        string ChannelName { get; }
        Task SendAlertAsync(AlertMessage message, CancellationToken cancellationToken);
    }
}
