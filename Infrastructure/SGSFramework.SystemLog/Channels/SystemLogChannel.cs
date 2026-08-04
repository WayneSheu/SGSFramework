using Serilog.Events;
using SGSFramework.Core.Abstractions.Processors;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.Channels
{
    // 系統日誌專用強型別管道
    public class SystemLogChannel : GenericChannel<LogEvent>
    {
        public SystemLogChannel(int capacity) : base(capacity) { }
    }
}
