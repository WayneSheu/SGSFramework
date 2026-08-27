using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Alert.Abstractions
{
    /// <summary>
    /// 告警調度服務介面
    /// </summary>
    public interface IAlertDispatcherService
    {
        Task DispatchAsync(AlertMessageModel alert, CancellationToken cancellationToken);
    }
}
