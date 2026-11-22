using System.Threading.Channels;

namespace SGS.Modules.MTG.Application.Services;

/// <summary>佇列中的轉錄作業：只帶識別碼，音檔由工作者從 FMS 讀取（不在記憶體堆積內容）。</summary>
public sealed record TranscriptionWorkItem(Guid AudioId, Guid MeetingId, string Actor);

/// <summary>音檔轉錄的記憶體佇列（Singleton，有界；滿了拒絕新作業而非無限堆積）。</summary>
public sealed class TranscriptionQueue
{
    private readonly Channel<TranscriptionWorkItem> _channel;

    public TranscriptionQueue(int capacity)
    {
        _channel = Channel.CreateBounded<TranscriptionWorkItem>(new BoundedChannelOptions(Math.Max(1, capacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    }

    /// <summary>嘗試加入佇列；已滿時回傳 false。</summary>
    public bool TryEnqueue(TranscriptionWorkItem item) => _channel.Writer.TryWrite(item);

    public ValueTask<TranscriptionWorkItem> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
