using System.Threading.Channels;
using SGS.Modules.MAN.Application.Contracts;

namespace SGS.Modules.MAN.Application.Services;

/// <summary>佇列中的生成工作：截圖只存在記憶體，處理完即丟棄。</summary>
public sealed record GenerationWorkItem(Guid JobId, GenerateTopicRequest Request, byte[] Image, string Actor);

/// <summary>生成作業的記憶體佇列（Singleton，有界；滿了拒絕新作業而非無限堆積）。</summary>
public sealed class GenerationQueue
{
    private readonly Channel<GenerationWorkItem> _channel;
    private int _length;

    public GenerationQueue(int capacity)
    {
        _channel = Channel.CreateBounded<GenerationWorkItem>(new BoundedChannelOptions(Math.Max(1, capacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    }

    public int Length => Volatile.Read(ref _length);

    /// <summary>嘗試加入佇列；已滿時回傳 false。</summary>
    public bool TryEnqueue(GenerationWorkItem item)
    {
        if (!_channel.Writer.TryWrite(item)) return false;
        Interlocked.Increment(ref _length);
        return true;
    }

    public async ValueTask<GenerationWorkItem> DequeueAsync(CancellationToken cancellationToken)
    {
        var item = await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Decrement(ref _length);
        return item;
    }
}
