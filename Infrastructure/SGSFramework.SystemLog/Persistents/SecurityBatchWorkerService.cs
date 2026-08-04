using Microsoft.Extensions.Logging;
using Serilog.Events;
using SGSFramework.Core.Abstractions.Processors;
using SGSFramework.SystemLog.BackgroundServices;
using SGSFramework.SystemLog.Channels;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.Persistents
{
    public class SecurityBatchWorkerService : BatchWorkerService<LogEvent>
    {
        public SecurityBatchWorkerService(
            SecurityLogChannel channel, // 💡 改為要求 SecurityLogChannel
            SqlServerSecurityProcessor processor,
            ILogger<SecurityBatchWorkerService> logger)
            : base(channel, processor, logger)
        {
        }
    }
}
