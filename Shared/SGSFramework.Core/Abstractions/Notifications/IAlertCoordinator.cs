using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Notifications
{
    /// <summary>
    /// 告警通知調度協調器介面
    /// </summary>
    public interface IAlertCoordinator
    {
        /// <summary>
        /// 將標準告警訊息分發至所有已註冊的通知策略管道
        /// </summary>
        Task DispatchAsync(AlertMessage message, CancellationToken cancellationToken);
    }
}
