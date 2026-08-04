using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Events
{
    // 定義系統錯誤事件 (跨模組通訊用)
    public record SystemErrorEvent(string Source, string ErrorMessage) : INotification;
}
