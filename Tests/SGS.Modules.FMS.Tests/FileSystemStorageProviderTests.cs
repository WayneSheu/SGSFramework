using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SGSFramework.Core.FileManagement;
using SGS.Modules.FMS.Infrastructure.Storage;
using Xunit;

namespace SGS.Modules.FMS.Tests;

public sealed class FileSystemStorageProviderTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly FileSystemStorageProvider _provider;

    public FileSystemStorageProviderTests() =>
        _provider = new FileSystemStorageProvider(StorageTarget.LocalPrivate, _root.Path, 64 * 1024, NullLogger.Instance);

    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task Upload_then_read_roundtrips_content()
    {
        var content = Enumerable.Range(0, 200_000).Select(i => (byte)i).ToArray();

        await _provider.UploadAsync("private", "2026/11/abc.bin", new MemoryStream(content), "application/octet-stream");
        await using var read = await _provider.OpenReadAsync("private", "2026/11/abc.bin");
        using var copy = new MemoryStream();
        await read.CopyToAsync(copy);

        copy.ToArray().Should().Equal(content);
        Directory.EnumerateFiles(_root.Path, "*.uploading", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_to_existing_key_is_refused()
    {
        await _provider.UploadAsync("private", "k.bin", new MemoryStream([1]), "application/octet-stream");

        var act = () => _provider.UploadAsync("private", "k.bin", new MemoryStream([2]), "application/octet-stream");

        await act.Should().ThrowAsync<IOException>();
    }

    [Theory]
    [InlineData("private", "../../windows/win.ini")]
    [InlineData("private", "..\\secret.txt")]
    [InlineData("..", "a.txt")]
    [InlineData("pri/vate", "a.txt")]
    [InlineData("private", "C:/Windows/win.ini")]
    public void Path_traversal_is_rejected(string container, string key)
    {
        var act = () => _provider.ResolvePath(container, key);

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Delete_is_idempotent()
    {
        await _provider.UploadAsync("private", "d.bin", new MemoryStream([1]), "application/octet-stream");

        (await _provider.DeleteAsync("private", "d.bin")).Should().BeTrue();
        (await _provider.DeleteAsync("private", "d.bin")).Should().BeFalse();
        (await _provider.ExistsAsync("private", "d.bin")).Should().BeFalse();
    }

    [Fact]
    public async Task Missing_file_throws_file_not_found()
    {
        var act = () => _provider.OpenReadAsync("private", "nope.bin");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}
