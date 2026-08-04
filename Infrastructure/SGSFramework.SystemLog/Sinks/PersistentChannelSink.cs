using Serilog.Core;
using Serilog.Events;
using SGSFramework.Core.Abstractions.Processors;

namespace SGSFramework.SystemLog.Sinks
{
    public class PersistentChannelSink : ILogEventSink
    {
        private readonly GenericChannel<LogEvent> _channel;

        public PersistentChannelSink(GenericChannel<LogEvent> channel)
        {
            _channel = channel;
        }

        public void Emit(LogEvent logEvent)
        {
            // 嘗試寫入通道，若通道滿了會根據 BoundedChannelFullMode 處理
            // 在金融環境中，建議使用 TryWrite 以避免極端情況下阻塞主程式執行緒
            if (!_channel.Writer.TryWrite(logEvent))
            {
                // 選擇性處理：若通道滿了，可以輸出至 SelfLog 或備援檔案
                Serilog.Debugging.SelfLog.WriteLine("Log channel full, dropping event.");
            }
        }
    }
}
