using Serilog.Events;
using System.Threading.Channels;

namespace SGSFramework.SystemLog.Channels
{
    public class LogChannel
    {
        // 使用 BoundedChannel 避免記憶體因日誌堆積而溢位 (OOM)
        private readonly Channel<LogEvent> _channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait // 緩衝區滿時等待，確保不掉資料
        });

        public ChannelWriter<LogEvent> Writer => _channel.Writer;
        public ChannelReader<LogEvent> Reader => _channel.Reader;
    }
}
