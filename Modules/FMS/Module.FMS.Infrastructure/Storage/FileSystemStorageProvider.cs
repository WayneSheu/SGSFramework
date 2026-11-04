using Microsoft.Extensions.Logging;
using SGSFramework.Core.FileManagement;
using SGS.Modules.FMS.Infrastructure.Storage;

namespace SGS.Modules.FMS.Infrastructure.Storage;

/// <summary>
/// 檔案系統儲存體（本機磁碟 / NAS UNC / SMB / NFS 掛載路徑）。
/// <para>
/// 實體路徑 = RootPath / Container / fileKey；寫入先落地暫存檔（.uploading）再原子更名，避免讀到寫一半的檔案。
/// 所有路徑皆以 <see cref="Path.GetFullPath(string)"/> 正規化並確認仍位於根目錄內（防路徑遍歷）。
/// </para>
/// 檔案系統無法產生預簽名網址：公開資源亦由 API 串流代理下載。
/// </summary>
public sealed class FileSystemStorageProvider : IStorageProvider
{
    private readonly string _rootPath;
    private readonly int _bufferSize;
    private readonly ILogger _logger;

    public FileSystemStorageProvider(StorageTarget target, string rootPath, int bufferSize, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(logger);

        Target = target;
        _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath)) + Path.DirectorySeparatorChar;
        _bufferSize = bufferSize;
        _logger = logger;
    }

    public StorageTarget Target { get; }

    public bool SupportsPresignedUrl => false;

    public async Task UploadAsync(string containerName, string fileKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var path = ResolvePath(containerName, fileKey);
        if (File.Exists(path))
        {
            throw new IOException($"儲存鍵已存在：{containerName}/{fileKey}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = $"{path}.{Guid.NewGuid():N}.uploading";

        try
        {
            await using (var output = new FileStream(tempPath, new FileStreamOptions
                         {
                             Mode = FileMode.CreateNew,
                             Access = FileAccess.Write,
                             Share = FileShare.None,
                             BufferSize = _bufferSize,
                             Options = FileOptions.Asynchronous | FileOptions.SequentialScan
                         }))
            {
                await content.CopyToAsync(output, _bufferSize, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, path, overwrite: false);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string containerName, string fileKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(containerName, fileKey);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"實體檔案不存在：{containerName}/{fileKey}");
        }

        Stream stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read | FileShare.Delete,
            BufferSize = _bufferSize,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });
        return Task.FromResult(stream);
    }

    public Task<Uri> GetPresignedUrlAsync(string containerName, string fileKey, TimeSpan expiry, string? downloadFileName = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("檔案系統儲存體不支援預簽名網址，請改以串流代理下載。");

    public Task<bool> ExistsAsync(string containerName, string fileKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(ResolvePath(containerName, fileKey)));

    public Task<bool> DeleteAsync(string containerName, string fileKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(containerName, fileKey);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        return Task.FromResult(true);
    }

    /// <summary>組合並驗證實體路徑：容器與鍵不得含根目錄、上層目錄或跳出根目錄</summary>
    public string ResolvePath(string containerName, string fileKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);

        if (Path.IsPathRooted(containerName) || Path.IsPathRooted(fileKey) ||
            containerName.Contains("..", StringComparison.Ordinal) || fileKey.Contains("..", StringComparison.Ordinal) ||
            containerName.IndexOfAny(['/', '\\', ':']) >= 0 || fileKey.Contains(':', StringComparison.Ordinal))
        {
            _logger.LogWarning("[FileStorage] 拒絕不合法的儲存路徑（疑似路徑遍歷）：{Container}/{Key}", containerName, fileKey);
            throw new UnauthorizedAccessException("不合法的儲存路徑。");
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, containerName, fileKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(_rootPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            _logger.LogWarning("[FileStorage] 拒絕跳出根目錄的儲存路徑：{Container}/{Key}", containerName, fileKey);
            throw new UnauthorizedAccessException("不合法的儲存路徑。");
        }

        return fullPath;
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "[FileStorage] 無法刪除上傳暫存檔 {Path}", path);
        }
    }
}
