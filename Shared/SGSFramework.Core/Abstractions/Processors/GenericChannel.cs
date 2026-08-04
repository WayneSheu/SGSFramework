using System.Threading.Channels;

namespace SGSFramework.Core.Abstractions.Processors
{
    /// <summary>
    /// Provides a thread-safe, bounded channel for passing data of a specified type between producers and consumers.
    /// </summary>
    /// <remarks>This class encapsulates a bounded channel with a default capacity of 10,000 items. It exposes
    /// the underlying writer and reader for sending and receiving data. The channel blocks producers when full and
    /// supports concurrent access from multiple threads.</remarks>
    /// <typeparam name="T">The type of data stored in the channel.</typeparam>
    public class GenericChannel<T>
    {
        private readonly Channel<T> _channel;
        public GenericChannel(int capacity = 10000)
        {
            _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait });
        }

        public ChannelWriter<T> Writer => _channel.Writer;
        public ChannelReader<T> Reader => _channel.Reader;

        // 通知讀取端：資料已生產完畢
        public void Shutdown() => _channel.Writer.TryComplete();
    }
}
