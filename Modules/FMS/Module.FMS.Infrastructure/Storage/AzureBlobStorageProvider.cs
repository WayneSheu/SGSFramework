using System.Collections.Concurrent;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using SGS.Modules.FMS.Infrastructure.Options;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.FMS.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage（雲端；本機以 Azurite 模擬）。容器 = Blob Container。
/// 上傳由 SDK 分塊傳送（長度未知的串流亦可），以 If-None-Match:* 防止覆寫；讀取以 Range 提供可定位串流；
/// 連線字串含帳戶金鑰時可產生 SAS 預簽名網址。
/// </summary>
public sealed class AzureBlobStorageProvider : IStorageProvider
{
    private readonly BlobServiceClient _service;
    private readonly AzureBlobStorageOptions _options;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, Task> _ensuredContainers = new(StringComparer.Ordinal);

    public AzureBlobStorageProvider(StorageTarget target, AzureBlobStorageOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);

        Target = target;
        _options = options;
        _logger = logger;
        _service = new BlobServiceClient(options.ConnectionString);
    }

    public StorageTarget Target { get; }

    public bool SupportsPresignedUrl => _service.CanGenerateAccountSasUri;

    public async Task UploadAsync(string containerName, string fileKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var blob = await GetBlobAsync(containerName, fileKey, ensureContainer: true, cancellationToken).ConfigureAwait(false);

        try
        {
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            throw new IOException($"儲存鍵已存在：{containerName}/{fileKey}", ex);
        }
    }

    public async Task<Stream> OpenReadAsync(string containerName, string fileKey, CancellationToken cancellationToken = default)
    {
        var blob = await GetBlobAsync(containerName, fileKey, ensureContainer: false, cancellationToken).ConfigureAwait(false);

        long length;
        try
        {
            length = (await blob.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Value.ContentLength;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException($"實體檔案不存在：{containerName}/{fileKey}", ex);
        }

        return new RangeReadStream(length, async (from, ct) =>
        {
            var response = await blob.DownloadStreamingAsync(new BlobDownloadOptions { Range = new HttpRange(from) }, ct).ConfigureAwait(false);
            return response.Value.Content;
        });
    }

    public async Task<Uri> GetPresignedUrlAsync(string containerName, string fileKey, TimeSpan expiry, string? downloadFileName = null, CancellationToken cancellationToken = default)
    {
        var blob = await GetBlobAsync(containerName, fileKey, ensureContainer: false, cancellationToken).ConfigureAwait(false);
        if (!blob.CanGenerateSasUri) throw new NotSupportedException("Azure 連線未使用帳戶金鑰，無法產生 SAS 網址。");

        var sas = new BlobSasBuilder(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(expiry))
        {
            BlobContainerName = containerName,
            BlobName = fileKey,
            Resource = "b",
            ContentDisposition = string.IsNullOrWhiteSpace(downloadFileName)
                ? null
                : $"attachment; filename*=UTF-8''{Uri.EscapeDataString(downloadFileName)}"
        };
        return blob.GenerateSasUri(sas);
    }

    public async Task<bool> ExistsAsync(string containerName, string fileKey, CancellationToken cancellationToken = default)
    {
        var blob = await GetBlobAsync(containerName, fileKey, ensureContainer: false, cancellationToken).ConfigureAwait(false);
        return (await blob.ExistsAsync(cancellationToken).ConfigureAwait(false)).Value;
    }

    public async Task<bool> DeleteAsync(string containerName, string fileKey, CancellationToken cancellationToken = default)
    {
        var blob = await GetBlobAsync(containerName, fileKey, ensureContainer: false, cancellationToken).ConfigureAwait(false);
        return (await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken).ConfigureAwait(false)).Value;
    }

    private async Task<BlobClient> GetBlobAsync(string containerName, string fileKey, bool ensureContainer, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
        if (fileKey.Contains("..", StringComparison.Ordinal) || fileKey.StartsWith('/') || fileKey.Any(char.IsControl))
            throw new UnauthorizedAccessException("不合法的儲存路徑。");

        var container = _service.GetBlobContainerClient(containerName);
        if (ensureContainer && _options.AutoCreateContainer)
        {
            var task = _ensuredContainers.GetOrAdd(containerName, async _ =>
            {
                var created = await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                if (created is not null) _logger.LogInformation("[FileStorage:Azure] 已建立容器 {Container}", containerName);
            });

            try
            {
                await task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch when (task.IsFaulted)
            {
                _ensuredContainers.TryRemove(containerName, out _); // 失敗不快取，下次重試
                throw;
            }
        }

        return container.GetBlobClient(fileKey);
    }
}
