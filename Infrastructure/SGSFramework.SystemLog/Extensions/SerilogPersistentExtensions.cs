using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using SGSFramework.Core.Abstractions.Processors;
using SGSFramework.SystemLog.Sinks;

namespace SGSFramework.SystemLog.Extensions
{
    public static class SerilogPersistentExtensions
    {
        /// <summary>
        /// Adds a persistent SQL sink to the Serilog configuration.
        /// </summary>
        /// <param name="sinkConfiguration"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        public static LoggerConfiguration PersistentSql(
            this LoggerSinkConfiguration sinkConfiguration,
            GenericChannel<LogEvent> channel)
        {
            return sinkConfiguration.Sink(new PersistentChannelSink(channel));
        }
    }
}
