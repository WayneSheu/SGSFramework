using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>
    /// 生產級高吞吐量線程安全記憶體專用告警通道
    /// </summary>
    public sealed class AlertMemoryChannel
    {
        private readonly Channel<LogEvent> _channel;

        public AlertMemoryChannel(int capacity = 5000)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite, // 防禦性設計：若告警佇列爆滿，拋棄新進告警，不拖垮生產端進程
                SingleWriter = false,
                SingleReader = true
            };
            _channel = Channel.CreateBounded<LogEvent>(options);
        }

        public ChannelWriter<LogEvent> Writer => _channel.Writer;
        public ChannelReader<LogEvent> Reader => _channel.Reader;
    }
}
