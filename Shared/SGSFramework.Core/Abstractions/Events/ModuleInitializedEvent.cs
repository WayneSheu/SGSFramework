using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Events
{
   //定義初始化完成的事件
   public record ModuleInitializedEvent(string ModuleName, bool IsSuccess, string? Message = null) : INotification;
}
