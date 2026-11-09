namespace SGS.Modules.FMS.Infrastructure.Storage;

/// <summary>
/// 物件儲存的可定位讀取串流：以 HTTP Range 自指定位置開啟回應串流並順向讀取；Seek 到非連續位置時才重新發出 Range 請求。
/// 讓解密串流可隨機存取區塊（下載續傳），而循序讀取時只需一次請求（不逐區塊往返）。
/// </summary>
public sealed class RangeReadStream(long length, Func<long, CancellationToken, Task<Stream>> openFrom) : Stream
{
    private Stream? _response;
    private long _responsePosition = -1;
    private long _position;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => length;

    public override long Position
    {
        get => _position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _position = value;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        return _position;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty || _position >= length) return 0;

        if (_response is null || _responsePosition != _position)
        {
            if (_response is not null) await _response.DisposeAsync().ConfigureAwait(false);
            _response = await openFrom(_position, cancellationToken).ConfigureAwait(false);
            _responsePosition = _position;
        }

        var read = await _response.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        _position += read;
        _responsePosition += read;
        return read;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _response?.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_response is not null) await _response.DisposeAsync().ConfigureAwait(false);
        _response = null;
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
