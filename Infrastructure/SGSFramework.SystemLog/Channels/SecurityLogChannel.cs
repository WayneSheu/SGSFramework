using Serilog.Events;
using SGSFramework.Core.Abstractions.Processors;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.Channels
{
    // 安全日誌專用強型別管道
    public class SecurityLogChannel : GenericChannel<LogEvent>
    {
        public SecurityLogChannel(int capacity) : base(capacity) { }
    }
}
