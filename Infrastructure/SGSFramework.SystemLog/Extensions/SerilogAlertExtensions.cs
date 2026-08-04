using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using SGSFramework.SystemLog.Sinks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace SGSFramework.SystemLog.Extensions
{
    public static class SerilogAlertExtensions
    {
        /// <summary>
        /// 掛載非阻塞高吞吐自訂告警管道代理 Sink
        /// </summary>
        public static LoggerConfiguration AlertingSink(
            this LoggerSinkConfiguration loggerSinkConfiguration,
            ChannelWriter<LogEvent> writer)
        {
            ArgumentNullException.ThrowIfNull(loggerSinkConfiguration);
            ArgumentNullException.ThrowIfNull(writer);

            return loggerSinkConfiguration.Sink(new AlertProxySink(writer));
        }
    }
}