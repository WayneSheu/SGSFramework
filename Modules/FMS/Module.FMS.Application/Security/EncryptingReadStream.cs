using System.Buffers;
using System.Security.Cryptography;

namespace SGS.Modules.FMS.Application.Security;

/// <summary>
/// 拉取式加密串流：讀取時自明文來源逐區塊 AES-256-GCM 加密並輸出 SGSE 格式，供儲存體 SDK 直接上傳（記憶體內處理，不落地）。
/// 以前瞻讀取判定最後區塊，因此來源可為長度未知的網路串流。區塊緩衝取自 <see cref="ArrayPool{T}"/>。
/// </summary>
public sealed class EncryptingReadStream : Stream
{
    private readonly Stream _source;
    private readonly AesGcm _aes;
    private readonly byte[] _header;
    private readonly int _chunkSize;
    private readonly bool _leaveOpen;

    private readonly byte[] _plain;      // 目前區塊明文
    private readonly byte[] _lookahead;  // 下一區塊明文（前瞻）
    private readonly byte[] _output;     // 目前輸出（標頭或密文 + Tag）
    private int _lookaheadLength = -1;   // -1 = 尚未讀取
    private int _outputLength;
    private int _outputOffset;
    private long _chunkIndex;
    private bool _headerEmitted;
    private bool _completed;

    public EncryptingReadStream(Stream source, ReadOnlySpan<byte> key, int chunkSize = FileCipherFormat.DefaultChunkSize, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (key.Length != FileCipherFormat.KeySize) throw new ArgumentException("資料金鑰須為 256 位元。", nameof(key));

        _source = source;
        _chunkSize = chunkSize;
        _leaveOpen = leaveOpen;
        _header = FileCipherFormat.CreateHeader(chunkSize);
        _aes = new AesGcm(key, FileCipherFormat.TagSize);
        _plain = ArrayPool<byte>.Shared.Rent(chunkSize);
        _lookahead = ArrayPool<byte>.Shared.Rent(chunkSize);
        _output = ArrayPool<byte>.Shared.Rent(chunkSize + FileCipherFormat.TagSize);
    }

    /// <summary>已輸出的密文位元組數</summary>
    public long BytesWritten { get; private set; }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => BytesWritten; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty) return 0;

        while (_outputOffset >= _outputLength)
        {
            if (_completed) return 0;
            await ProduceNextAsync(cancellationToken).ConfigureAwait(false);
        }

        var count = Math.Min(buffer.Length, _outputLength - _outputOffset);
        _output.AsSpan(_outputOffset, count).CopyTo(buffer.Span);
        _outputOffset += count;
        BytesWritten += count;
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private async Task ProduceNextAsync(CancellationToken cancellationToken)
    {
        _outputOffset = 0;

        if (!_headerEmitted)
        {
            _header.CopyTo(_output, 0);
            _outputLength = FileCipherFormat.HeaderSize;
            _headerEmitted = true;
            return;
        }

        // 目前區塊：首次自來源讀取，之後取用前瞻緩衝
        int plainLength;
        if (_lookaheadLength < 0)
        {
            plainLength = await FillAsync(_plain, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            plainLength = _lookaheadLength;
            _lookahead.AsSpan(0, plainLength).CopyTo(_plain);
        }

        // 前瞻：目前區塊滿載時再讀下一區塊，讀不到資料即代表目前區塊為最後區塊
        _lookaheadLength = plainLength == _chunkSize
            ? await FillAsync(_lookahead, cancellationToken).ConfigureAwait(false)
            : 0;
        var isFinal = _lookaheadLength == 0;

        Span<byte> nonce = stackalloc byte[FileCipherFormat.NonceSize];
        FileCipherFormat.BuildNonce(_header, _chunkIndex, nonce);
        var aad = FileCipherFormat.BuildAad(_header, _chunkIndex, isFinal);

        _aes.Encrypt(nonce, _plain.AsSpan(0, plainLength), _output.AsSpan(0, plainLength),
            _output.AsSpan(plainLength, FileCipherFormat.TagSize), aad);

        _outputLength = plainLength + FileCipherFormat.TagSize;
        _chunkIndex++;
        _completed = isFinal;
    }

    /// <summary>盡量讀滿緩衝（網路串流單次讀取可能不足一個區塊）</summary>
    private async Task<int> FillAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < _chunkSize)
        {
            var read = await _source.ReadAsync(buffer.AsMemory(total, _chunkSize - total), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
        }

        return total;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _aes.Dispose();
            CryptographicOperations.ZeroMemory(_plain);
            CryptographicOperations.ZeroMemory(_lookahead);
            ArrayPool<byte>.Shared.Return(_plain);
            ArrayPool<byte>.Shared.Return(_lookahead);
            ArrayPool<byte>.Shared.Return(_output);
            if (!_leaveOpen) _source.Dispose();
        }

        base.Dispose(disposing);
    }
}
