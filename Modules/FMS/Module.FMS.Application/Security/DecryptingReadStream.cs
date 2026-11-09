using System.Buffers;
using System.Security.Cryptography;

namespace SGS.Modules.FMS.Application.Security;

/// <summary>
/// SGSE 解密串流：逐區塊驗證 GCM Tag 後解密（記憶體內就地處理，不落地）。
/// <list type="bullet">
/// <item>內層可定位（檔案系統、具 Range 能力的物件儲存串流）：支援 Seek / Length，下載可使用 HTTP Range 續傳。</item>
/// <item>內層不可定位：順向讀取，以前瞻一個位元組判定最後區塊。</item>
/// </list>
/// 任一區塊驗證失敗（竄改、截斷、重排、金鑰錯誤）一律拋出 <see cref="CryptographicException"/>。
/// </summary>
public sealed class DecryptingReadStream : Stream
{
    private readonly Stream _inner;
    private readonly AesGcm _aes;
    private readonly byte[] _header;
    private readonly int _chunkSize;
    private readonly int _fullChunk;
    private readonly bool _leaveOpen;
    private readonly long? _plainLength;
    private readonly long _chunkCount;

    private readonly byte[] _cipher;
    private readonly byte[] _plain;
    private long _cachedChunk = -1;
    private int _cachedLength;
    private long _position;

    // 順向模式：前瞻的單一位元組
    private int _carry = -1;
    private bool _forwardEnded;

    private DecryptingReadStream(Stream inner, ReadOnlySpan<byte> key, byte[] header, int chunkSize, long? plainLength, long chunkCount, bool leaveOpen)
    {
        _inner = inner;
        _header = header;
        _chunkSize = chunkSize;
        _fullChunk = chunkSize + FileCipherFormat.TagSize;
        _plainLength = plainLength;
        _chunkCount = chunkCount;
        _leaveOpen = leaveOpen;
        _aes = new AesGcm(key, FileCipherFormat.TagSize);
        _cipher = ArrayPool<byte>.Shared.Rent(_fullChunk);
        _plain = ArrayPool<byte>.Shared.Rent(chunkSize);
    }

    /// <summary>讀取並驗證標頭後建立解密串流（內層位置須位於檔案開頭）</summary>
    /// <param name="knownPlainLength">不可定位時由中繼資料提供明文長度（僅供 Length 屬性使用）</param>
    public static async Task<DecryptingReadStream> OpenAsync(Stream inner, byte[] key, long? knownPlainLength = null, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != FileCipherFormat.KeySize) throw new ArgumentException("資料金鑰須為 256 位元。", nameof(key));

        var header = new byte[FileCipherFormat.HeaderSize];
        if (inner.CanSeek) inner.Position = 0;
        var read = await inner.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        if (read < header.Length) throw new CryptographicException("SGSE 加密檔案長度不合法（可能遭截斷）。");

        var chunkSize = FileCipherFormat.ParseHeader(header);

        if (inner.CanSeek)
        {
            var (plain, chunks) = FileCipherFormat.GetPlainLength(inner.Length, chunkSize);
            return new DecryptingReadStream(inner, key, header, chunkSize, plain, chunks, leaveOpen);
        }

        return new DecryptingReadStream(inner, key, header, chunkSize, knownPlainLength, -1, leaveOpen);
    }

    public override bool CanRead => true;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;

    public override long Length => _plainLength ?? throw new NotSupportedException();

    public override long Position
    {
        get => _position;
        set
        {
            if (!CanSeek) throw new NotSupportedException();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _position = value;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!CanSeek) throw new NotSupportedException();
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
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
        if (buffer.IsEmpty) return 0;

        var chunkIndex = _position / _chunkSize;
        var offsetInChunk = (int)(_position % _chunkSize);

        if (_cachedChunk != chunkIndex)
        {
            var loaded = CanSeek
                ? await LoadSeekableChunkAsync(chunkIndex, cancellationToken).ConfigureAwait(false)
                : await LoadForwardChunkAsync(chunkIndex, cancellationToken).ConfigureAwait(false);
            if (!loaded) return 0;
        }

        if (offsetInChunk >= _cachedLength) return 0;

        var count = Math.Min(buffer.Length, _cachedLength - offsetInChunk);
        _plain.AsSpan(offsetInChunk, count).CopyTo(buffer.Span);
        _position += count;
        return count;
    }

    private async Task<bool> LoadSeekableChunkAsync(long chunkIndex, CancellationToken cancellationToken)
    {
        if (chunkIndex >= _chunkCount) return false;

        var isFinal = chunkIndex == _chunkCount - 1;
        var cipherStart = FileCipherFormat.HeaderSize + chunkIndex * _fullChunk;
        var cipherLength = (int)Math.Min(_fullChunk, _inner.Length - cipherStart);

        _inner.Position = cipherStart;
        var read = await _inner.ReadAtLeastAsync(_cipher.AsMemory(0, cipherLength), cipherLength, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        if (read != cipherLength) throw new CryptographicException("SGSE 加密檔案長度不合法（可能遭截斷）。");

        DecryptChunk(chunkIndex, cipherLength, isFinal);
        return true;
    }

    private async Task<bool> LoadForwardChunkAsync(long chunkIndex, CancellationToken cancellationToken)
    {
        if (_forwardEnded) return false;
        if (chunkIndex != _cachedChunk + 1) throw new NotSupportedException("不可定位的解密串流僅支援順向讀取。");

        var total = 0;
        if (_carry >= 0)
        {
            _cipher[0] = (byte)_carry;
            _carry = -1;
            total = 1;
        }

        total += await _inner.ReadAtLeastAsync(_cipher.AsMemory(total, _fullChunk - total), _fullChunk - total, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);

        bool isFinal;
        if (total < _fullChunk)
        {
            isFinal = true;
        }
        else
        {
            // 區塊滿載時前瞻一個位元組，判定是否為最後區塊
            var peek = new byte[1];
            var more = await _inner.ReadAsync(peek, cancellationToken).ConfigureAwait(false);
            isFinal = more == 0;
            if (!isFinal) _carry = peek[0];
        }

        if (total < FileCipherFormat.TagSize || (chunkIndex > 0 && total == FileCipherFormat.TagSize))
            throw new CryptographicException("SGSE 加密檔案長度不合法（可能遭截斷）。");

        DecryptChunk(chunkIndex, total, isFinal);
        _forwardEnded = isFinal;
        return _cachedLength > 0;
    }

    private void DecryptChunk(long chunkIndex, int cipherLength, bool isFinal)
    {
        var plainLength = cipherLength - FileCipherFormat.TagSize;
        Span<byte> nonce = stackalloc byte[FileCipherFormat.NonceSize];
        FileCipherFormat.BuildNonce(_header, chunkIndex, nonce);
        var aad = FileCipherFormat.BuildAad(_header, chunkIndex, isFinal);

        _aes.Decrypt(nonce, _cipher.AsSpan(0, plainLength), _cipher.AsSpan(plainLength, FileCipherFormat.TagSize),
            _plain.AsSpan(0, plainLength), aad);

        _cachedChunk = chunkIndex;
        _cachedLength = plainLength;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _aes.Dispose();
            CryptographicOperations.ZeroMemory(_plain);
            ArrayPool<byte>.Shared.Return(_cipher);
            ArrayPool<byte>.Shared.Return(_plain);
            if (!_leaveOpen) _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
