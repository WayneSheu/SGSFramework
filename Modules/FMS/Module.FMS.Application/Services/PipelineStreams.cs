using System.Security.Cryptography;

namespace SGS.Modules.FMS.Application.Services;

/// <summary>
/// 前綴串流：先輸出已預讀的檔頭位元組，再接續原始串流。上傳時只預讀檔頭做簽章檢查，其餘內容仍以串流直送儲存體（不整檔緩衝）。
/// </summary>
internal sealed class PrefixedStream(byte[] prefix, int prefixLength, Stream inner) : Stream
{
    private int _prefixOffset;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_prefixOffset < prefixLength)
        {
            var count = Math.Min(buffer.Length, prefixLength - _prefixOffset);
            prefix.AsSpan(_prefixOffset, count).CopyTo(buffer.Span);
            _prefixOffset += count;
            return ValueTask.FromResult(count);
        }

        return inner.ReadAsync(buffer, cancellationToken);
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
/// 完整性守衛串流：解密過程中任一區塊驗證失敗（疑遭竄改）時，先觸發稽核回呼再拋出例外。
/// 回應標頭可能已送出，無法再改狀態碼，因此於串流層記錄 IntegrityFailure 並中斷連線。
/// </summary>
internal sealed class IntegrityGuardStream(Stream inner, Func<CryptographicException, Task> onFailure) : Stream
{
    private bool _reported;

    public override bool CanRead => true;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (CryptographicException ex) when (!_reported)
        {
            _reported = true;
            await onFailure(ex).ConfigureAwait(false);
            throw;
        }
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>需完整內容的上傳（圖片重新編碼、OOXML）之記憶體緩衝併發上限（Singleton）</summary>
public sealed class UploadBufferLimiter(int maxConcurrent) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(maxConcurrent, maxConcurrent);

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(_semaphore);
    }

    public void Dispose() => _semaphore.Dispose();

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) semaphore.Release();
        }
    }
}
