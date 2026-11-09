using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using SGS.Modules.FMS.Application.Security;
using SGSFramework.Core.FileManagement;
using Xunit;

namespace SGS.Modules.FMS.Tests;

public class FileEncryptionTests
{
    private const int Chunk = 1024;
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

    private static byte[] Plain(int length) => Enumerable.Range(0, length).Select(i => (byte)(i * 31 + 7)).ToArray();

    private static async Task<byte[]> EncryptAsync(byte[] plain, byte[]? key = null)
    {
        await using var enc = new EncryptingReadStream(new NonSeekableStream(plain), key ?? Key, Chunk);
        using var output = new MemoryStream();
        await enc.CopyToAsync(output);
        return output.ToArray();
    }

    private static async Task<byte[]> DecryptAsync(byte[] cipher, bool seekable, byte[]? key = null)
    {
        Stream inner = seekable ? new MemoryStream(cipher) : new NonSeekableStream(cipher);
        await using var dec = await DecryptingReadStream.OpenAsync(inner, key ?? Key);
        using var output = new MemoryStream();
        await dec.CopyToAsync(output);
        return output.ToArray();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(Chunk - 1)]
    [InlineData(Chunk)]
    [InlineData(Chunk + 1)]
    [InlineData(Chunk * 3)]
    [InlineData(200_000)]
    public async Task Roundtrip_restores_plaintext_in_both_read_modes(int length)
    {
        var plain = Plain(length);

        var cipher = await EncryptAsync(plain);

        cipher.Length.Should().Be((int)FileCipherFormat.GetCipherLength(length, Chunk));
        FileCipherFormat.HasHeader(cipher).Should().BeTrue();
        (await DecryptAsync(cipher, seekable: true)).Should().Equal(plain);
        (await DecryptAsync(cipher, seekable: false)).Should().Equal(plain);
    }

    [Fact]
    public async Task Ciphertext_does_not_contain_plaintext()
    {
        var plain = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("CONFIDENTIAL-機密報告 ", 200)));

        var cipher = await EncryptAsync(plain);

        cipher.AsSpan().IndexOf("CONFIDENTIAL"u8).Should().Be(-1);
    }

    [Fact]
    public async Task Same_plaintext_encrypts_differently_each_time()
    {
        var plain = Plain(5000);

        (await EncryptAsync(plain)).Should().NotEqual(await EncryptAsync(plain));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Tampered_chunk_is_detected(bool seekable)
    {
        var cipher = await EncryptAsync(Plain(Chunk * 3));
        cipher[FileCipherFormat.HeaderSize + Chunk + FileCipherFormat.TagSize + 10] ^= 0x01;

        var act = () => DecryptAsync(cipher, seekable);

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Truncated_at_chunk_boundary_is_detected(bool seekable)
    {
        var cipher = await EncryptAsync(Plain(Chunk * 3));
        var truncated = cipher[..^(Chunk + FileCipherFormat.TagSize)];

        var act = () => DecryptAsync(truncated, seekable);

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task Reordered_chunks_are_detected()
    {
        var cipher = await EncryptAsync(Plain(Chunk * 3));
        var full = Chunk + FileCipherFormat.TagSize;
        var first = cipher.AsSpan(FileCipherFormat.HeaderSize, full).ToArray();
        cipher.AsSpan(FileCipherFormat.HeaderSize + full, full).CopyTo(cipher.AsSpan(FileCipherFormat.HeaderSize));
        first.CopyTo(cipher.AsSpan(FileCipherFormat.HeaderSize + full));

        var act = () => DecryptAsync(cipher, seekable: true);

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task Tampered_header_is_detected()
    {
        var cipher = await EncryptAsync(Plain(3000));
        cipher[15] ^= 0xFF; // nonce 前綴

        var act = () => DecryptAsync(cipher, seekable: true);

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task Wrong_key_is_rejected()
    {
        var cipher = await EncryptAsync(Plain(3000));

        var act = () => DecryptAsync(cipher, seekable: true, RandomNumberGenerator.GetBytes(32));

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task Seek_reads_any_range_correctly()
    {
        var plain = Plain(Chunk * 5 + 123);
        var cipher = await EncryptAsync(plain);
        await using var dec = await DecryptingReadStream.OpenAsync(new MemoryStream(cipher), Key);

        dec.Length.Should().Be(plain.Length);
        foreach (var (offset, length) in new[] { (0, 10), (Chunk - 3, 10), (Chunk * 2, Chunk), (plain.Length - 50, 50), (3000, 1500) })
        {
            dec.Seek(offset, SeekOrigin.Begin);
            var buffer = new byte[length];
            await dec.ReadExactlyAsync(buffer);
            buffer.Should().Equal(plain.AsSpan(offset, length).ToArray());
        }
    }

    [Fact]
    public async Task Metered_stream_hashes_plaintext_and_enforces_limit()
    {
        var plain = Plain(10_000);
        await using (var metered = new MeteredReadStream(new MemoryStream(plain), 20_000))
        {
            await metered.CopyToAsync(Stream.Null);
            metered.Sha256Hex.Should().Be(Convert.ToHexStringLower(SHA256.HashData(plain)));
            metered.BytesRead.Should().Be(plain.Length);
        }

        await using var limited = new MeteredReadStream(new MemoryStream(plain), 5_000);
        var act = () => limited.CopyToAsync(Stream.Null);
        (await act.Should().ThrowAsync<FileRejectedException>()).Which.StatusCode.Should().Be(413);
    }

    [Fact]
    public void Key_protector_wraps_and_unwraps_with_data_protection()
    {
        var protector = new FileKeyProtector(new EphemeralDataProtectionProvider());

        var (key, wrapped) = protector.CreateKey();

        wrapped.Should().StartWith("fk:v1:");
        wrapped.Should().NotContain(Convert.ToBase64String(key));
        protector.Unwrap(wrapped).Should().Equal(key);
        var act = () => new FileKeyProtector(new EphemeralDataProtectionProvider()).Unwrap(wrapped);
        act.Should().Throw<CryptographicException>();
    }
}

/// <summary>模擬網路串流：不可定位且單次讀取量不固定</summary>
internal sealed class NonSeekableStream(byte[] data) : Stream
{
    private readonly MemoryStream _inner = new(data);
    private int _toggle;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) =>
        _inner.Read(buffer, offset, Math.Min(count, (_toggle++ % 3 + 1) * 700));

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
