using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace SGSFramework.SystemLog.Sinks
{
    /// <summary>
    /// Serilog 核心橋接 Sink，將符合條件的日誌秒級非阻塞推入記憶體通道
    /// </summary>
    public sealed class AlertProxySink : ILogEventSink
    {
        private readonly ChannelWriter<LogEvent> _writer;

        public AlertProxySink(ChannelWriter<LogEvent> writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public void Emit(LogEvent logEvent)
        {
            ArgumentNullException.ThrowIfNull(logEvent);
            _writer.TryWrite(logEvent); // 非阻塞式寫入，Channel 滿載時會依據 DropWrite 機制自動防禦
        }
    }
}
