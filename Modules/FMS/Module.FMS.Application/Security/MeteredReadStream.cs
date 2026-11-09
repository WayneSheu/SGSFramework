using System.Security.Cryptography;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.FMS.Application.Security;

/// <summary>
/// 計量串流：讀取時同步計算明文 SHA-256 與長度，超過上限立即中止（413），讓上傳可邊收邊驗證、不需先整檔緩衝。
/// </summary>
public sealed class MeteredReadStream(Stream inner, long maxBytes, bool leaveOpen = true) : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private byte[]? _digest;

    public long BytesRead { get; private set; }

    /// <summary>讀取完畢後的 SHA-256（十六進位小寫）；尚未讀完時呼叫會拋出例外</summary>
    public string Sha256Hex => Convert.ToHexStringLower(_digest ?? throw new InvalidOperationException("內容尚未讀取完畢。"));

    public byte[] Sha256Bytes => _digest ?? throw new InvalidOperationException("內容尚未讀取完畢。");

    public bool Completed => _digest is not null;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => BytesRead; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_digest is not null) return 0;

        var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            _digest = _hash.GetHashAndReset();
            return 0;
        }

        BytesRead += read;
        if (BytesRead > maxBytes)
            throw new FileRejectedException($"檔案大小超過上限 {maxBytes / 1024 / 1024:N0} MB。", 413);

        _hash.AppendData(buffer.Span[..read]);
        return read;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
            if (!leaveOpen) inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
