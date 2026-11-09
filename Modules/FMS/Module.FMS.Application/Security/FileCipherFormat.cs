using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SGS.Modules.FMS.Application.Security;

/// <summary>
/// 檔案加密格式 SGSE v1：分塊 AES-256-GCM 串流加密（STREAM 構造），整檔不必一次載入記憶體且支援隨機存取（Range）。
/// <code>
/// 標頭 (20 bytes) : "SGSE" | 版本(1) | 保留(3) | 區塊大小 int32 LE | nonce 前綴(8)
/// 區塊 i          : 密文(≤ 區塊大小) | GCM Tag(16)
/// Nonce(12)       : nonce 前綴(8) ‖ 區塊序號 uint32 BE
/// AAD             : 標頭(20) ‖ 區塊序號 uint64 BE ‖ 最後區塊旗標(1)
/// </code>
/// 標頭納入 AAD 防止標頭被竄改；序號與最後區塊旗標防止區塊重排、截斷與跨檔拼接。
/// 每個檔案使用獨立的資料金鑰（DEK）與隨機 nonce 前綴，nonce 不會重複。
/// </summary>
public static class FileCipherFormat
{
    public const int Version = 1;
    public const int HeaderSize = 20;
    public const int TagSize = 16;
    public const int NonceSize = 12;
    public const int KeySize = 32;
    public const int DefaultChunkSize = 64 * 1024;

    private static ReadOnlySpan<byte> Magic => "SGSE"u8;
    private const int AadSize = HeaderSize + sizeof(ulong) + 1;

    /// <summary>單一區塊上限（防止惡意標頭要求超大緩衝）</summary>
    public const int MaxChunkSize = 4 * 1024 * 1024;

    public static byte[] CreateHeader(int chunkSize)
    {
        if (chunkSize is <= 0 or > MaxChunkSize) throw new ArgumentOutOfRangeException(nameof(chunkSize));

        var header = new byte[HeaderSize];
        Magic.CopyTo(header);
        header[4] = Version;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8, 4), chunkSize);
        RandomNumberGenerator.Fill(header.AsSpan(12, 8));
        return header;
    }

    /// <summary>解析標頭；格式或版本不符時拋出 <see cref="CryptographicException"/></summary>
    public static int ParseHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < HeaderSize || !header[..4].SequenceEqual(Magic) || header[4] != Version)
            throw new CryptographicException("不是有效的 SGSE 加密檔案或版本不支援。");

        var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(8, 4));
        if (chunkSize is <= 0 or > MaxChunkSize) throw new CryptographicException("SGSE 標頭的區塊大小不合法。");
        return chunkSize;
    }

    /// <summary>檢查內容是否以 SGSE 標頭開始</summary>
    public static bool HasHeader(ReadOnlySpan<byte> data) =>
        data.Length >= HeaderSize && data[..4].SequenceEqual(Magic);

    internal static void BuildNonce(ReadOnlySpan<byte> header, long chunkIndex, Span<byte> nonce)
    {
        if (chunkIndex is < 0 or > uint.MaxValue) throw new CryptographicException("檔案超過加密格式的區塊數上限。");

        header.Slice(12, 8).CopyTo(nonce);
        BinaryPrimitives.WriteUInt32BigEndian(nonce[8..], (uint)chunkIndex);
    }

    internal static byte[] BuildAad(ReadOnlySpan<byte> header, long chunkIndex, bool isFinal)
    {
        var aad = new byte[AadSize];
        header[..HeaderSize].CopyTo(aad);
        BinaryPrimitives.WriteUInt64BigEndian(aad.AsSpan(HeaderSize, sizeof(ulong)), (ulong)chunkIndex);
        aad[^1] = isFinal ? (byte)1 : (byte)0;
        return aad;
    }

    /// <summary>由明文長度計算密文長度（空檔仍有一個空的最後區塊）</summary>
    public static long GetCipherLength(long plainLength, int chunkSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(plainLength);
        var chunks = Math.Max(1, (plainLength + chunkSize - 1) / chunkSize);
        return HeaderSize + plainLength + chunks * TagSize;
    }

    /// <summary>由密文長度反推明文長度與區塊數；長度不合法時拋出 <see cref="CryptographicException"/></summary>
    public static (long PlainLength, long ChunkCount) GetPlainLength(long cipherLength, int chunkSize)
    {
        var body = cipherLength - HeaderSize;
        if (body < TagSize) throw new CryptographicException("SGSE 加密檔案長度不合法（可能遭截斷）。");

        var fullChunk = (long)chunkSize + TagSize;
        var chunks = (body + fullChunk - 1) / fullChunk;
        var lastCipher = body - (chunks - 1) * fullChunk;

        // 最後區塊至少含 Tag；多區塊時最後區塊不會是空的（寫入端以前瞻判定最後區塊）
        if (lastCipher < TagSize || (chunks > 1 && lastCipher == TagSize))
            throw new CryptographicException("SGSE 加密檔案長度不合法（可能遭截斷）。");

        return (body - chunks * TagSize, chunks);
    }
}
