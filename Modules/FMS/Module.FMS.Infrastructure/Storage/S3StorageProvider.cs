using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using SGS.Modules.FMS.Infrastructure.Options;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.FMS.Infrastructure.Storage;

/// <summary>
/// S3 相容物件儲存（MinIO 地端 / 本機 Docker、AWS S3）。容器 = Bucket。
/// <list type="bullet">
/// <item>上傳：串流長度未知（邊收邊加密），以多段上傳（分段於記憶體緩衝）寫入；小檔單次 PutObject。不落地本機。</item>
/// <item>讀取：以 Range 請求提供可定位串流，支援解密區塊隨機存取。</item>
/// <item>預簽名網址：GetPreSignedURL（僅未加密的公開分類會使用）。</item>
/// </list>
/// </summary>
public sealed class S3StorageProvider : IStorageProvider, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly S3StorageOptions _options;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, Task> _ensuredBuckets = new(StringComparer.Ordinal);

    public S3StorageProvider(StorageTarget target, S3StorageOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        Target = target;
        _options = options;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle,
            // MinIO 等相容實作不一定支援 SDK v4 預設的 CRC 檢查碼標頭，僅於必要時計算
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        if (string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }
        else
        {
            config.ServiceURL = options.ServiceUrl;
            config.AuthenticationRegion = options.Region;
        }

        _client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
    }

    public StorageTarget Target { get; }

    public bool SupportsPresignedUrl => true;

    public async Task UploadAsync(string containerName, string fileKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ValidateKey(fileKey);
        await EnsureBucketAsync(containerName, cancellationToken).ConfigureAwait(false);

        if (await ExistsAsync(containerName, fileKey, cancellationToken).ConfigureAwait(false))
            throw new IOException($"儲存鍵已存在：{containerName}/{fileKey}");

        var partSize = _options.PartSizeMb * 1024 * 1024;
        var buffer = ArrayPool<byte>.Shared.Rent(partSize);
        string? uploadId = null;
        try
        {
            var length = await FillAsync(content, buffer, partSize, cancellationToken).ConfigureAwait(false);

            // 單一分段即可容納：直接 PutObject
            if (length < partSize)
            {
                using var single = new MemoryStream(buffer, 0, length, writable: false);
                await _client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = containerName,
                    Key = fileKey,
                    InputStream = single,
                    ContentType = contentType,
                    AutoCloseStream = false
                }, cancellationToken).ConfigureAwait(false);
                return;
            }

            uploadId = (await _client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
            {
                BucketName = containerName,
                Key = fileKey,
                ContentType = contentType
            }, cancellationToken).ConfigureAwait(false)).UploadId;

            var parts = new List<PartETag>();
            var partNumber = 1;
            while (length > 0)
            {
                using var part = new MemoryStream(buffer, 0, length, writable: false);
                var response = await _client.UploadPartAsync(new UploadPartRequest
                {
                    BucketName = containerName,
                    Key = fileKey,
                    UploadId = uploadId,
                    PartNumber = partNumber,
                    PartSize = length,
                    InputStream = part
                }, cancellationToken).ConfigureAwait(false);

                parts.Add(new PartETag(partNumber++, response.ETag));
                length = await FillAsync(content, buffer, partSize, cancellationToken).ConfigureAwait(false);
            }

            await _client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
            {
                BucketName = containerName,
                Key = fileKey,
                UploadId = uploadId,
                PartETags = parts
            }, cancellationToken).ConfigureAwait(false);
            uploadId = null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (uploadId is not null) await AbortQuietlyAsync(containerName, fileKey, uploadId).ConfigureAwait(false);
        }
    }

    public async Task<Stream> OpenReadAsync(string containerName, string fileKey, CancellationToken cancellationToken = default)
    {
        ValidateKey(fileKey);

        long length;
        try
        {
            var head = await _client.GetObjectMetadataAsync(containerName, fileKey, cancellationToken).ConfigureAwait(false);
            length = head.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException($"實體檔案不存在：{containerName}/{fileKey}", ex);
        }

        return new RangeReadStream(length, async (from, ct) =>
        {
            var response = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = containerName,
                Key = fileKey,
                ByteRange = new ByteRange(from, length - 1)
            }, ct).ConfigureAwait(false);
            return response.ResponseStream;
        });
    }

    public Task<Uri> GetPresignedUrlAsync(string containerName, string fileKey, TimeSpan expiry, string? downloadFileName = null, CancellationToken cancellationToken = default)
    {
        ValidateKey(fileKey);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = containerName,
            Key = fileKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry)
        };
        if (!string.IsNullOrWhiteSpace(downloadFileName))
            request.ResponseHeaderOverrides.ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(downloadFileName)}";

        return Task.FromResult(new Uri(_client.GetPreSignedURL(request)));
    }

    public async Task<bool> ExistsAsync(string containerName, string fileKey, CancellationToken cancellationToken = default)
    {
        ValidateKey(fileKey);
        try
        {
            await _client.GetObjectMetadataAsync(containerName, fileKey, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string containerName, string fileKey, CancellationToken cancellationToken = default)
    {
        if (!await ExistsAsync(containerName, fileKey, cancellationToken).ConfigureAwait(false)) return false;

        await _client.DeleteObjectAsync(containerName, fileKey, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public void Dispose() => _client.Dispose();

    private async Task EnsureBucketAsync(string bucket, CancellationToken cancellationToken)
    {
        if (!_options.AutoCreateBucket) return;

        var task = _ensuredBuckets.GetOrAdd(bucket, async name =>
        {
            try
            {
                await _client.PutBucketAsync(new PutBucketRequest { BucketName = name, UseClientRegion = true }, CancellationToken.None).ConfigureAwait(false);
                _logger.LogInformation("[FileStorage:S3] 已建立 Bucket {Bucket}", name);
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
            {
                // 已存在
            }
        });

        try
        {
            await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch when (task.IsFaulted)
        {
            _ensuredBuckets.TryRemove(bucket, out _); // 失敗不快取，下次重試
            throw;
        }
    }

    private async Task AbortQuietlyAsync(string bucket, string key, string uploadId)
    {
        try
        {
            await _client.AbortMultipartUploadAsync(bucket, key, uploadId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FileStorage:S3] 中止多段上傳失敗 {Bucket}/{Key}（將由 Bucket 生命週期規則清除）", bucket, key);
        }
    }

    private static async Task<int> FillAsync(Stream source, byte[] buffer, int size, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < size)
        {
            var read = await source.ReadAsync(buffer.AsMemory(total, size - total), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
        }

        return total;
    }

    /// <summary>物件鍵由系統產生（yyyy/MM/{Guid}{副檔名}），仍拒絕上層目錄與控制字元</summary>
    private static void ValidateKey(string fileKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
        if (fileKey.Contains("..", StringComparison.Ordinal) || fileKey.StartsWith('/') || fileKey.Any(char.IsControl))
            throw new UnauthorizedAccessException("不合法的儲存路徑。");
    }
}
