using System.Threading.Channels;

namespace SGS.Modules.MTG.Application.Services;

/// <summary>佇列中的紀要作業：只帶識別碼，逐字稿由工作者從資料庫讀取（不在記憶體堆積內容）。</summary>
public sealed record MinutesWorkItem(Guid MeetingId, string ScenarioCode, string Actor);

/// <summary>紀要生成的記憶體佇列（Singleton，有界；滿了拒絕新作業而非無限堆積）。</summary>
public sealed class MinutesQueue
{
    private readonly Channel<MinutesWorkItem> _channel;

    public MinutesQueue(int capacity)
    {
        _channel = Channel.CreateBounded<MinutesWorkItem>(new BoundedChannelOptions(Math.Max(1, capacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    }

    /// <summary>嘗試加入佇列；已滿時回傳 false。</summary>
    public bool TryEnqueue(MinutesWorkItem item) => _channel.Writer.TryWrite(item);

    public ValueTask<MinutesWorkItem> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
