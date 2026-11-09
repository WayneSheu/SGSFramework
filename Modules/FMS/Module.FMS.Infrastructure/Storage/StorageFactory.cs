using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.FileManagement;
using SGS.Modules.FMS.Infrastructure.Options;

namespace SGS.Modules.FMS.Infrastructure.Storage;

/// <summary>
/// 儲存體多驅動工廠（Singleton）：依組態 FileManagement:Targets 為每個儲存目標建立對應驅動，
/// 環境切換（本機 MinIO / 地端 NAS / 雲端 Azure）只需變更 Targets:{Target}:Provider，呼叫端無感。
/// <para>
/// 驅動於第一次使用時才建立：檔案管理未啟用（Enabled = false、組態留白）時，注入 IFileManagementService 的服務
/// （例如 RPT）仍可正常建構，實際操作才回應 503。
/// </para>
/// </summary>
public sealed class StorageFactory(IOptions<FileManagementOptions> options, ILoggerFactory loggerFactory) : IStorageFactory, IDisposable
{
    private readonly Lazy<Dictionary<StorageTarget, (IStorageProvider Provider, string DefaultContainer)>> _providers =
        new(() => Build(options.Value, loggerFactory), LazyThreadSafetyMode.ExecutionAndPublication);

    public IStorageProvider GetProvider(StorageTarget target) =>
        _providers.Value.TryGetValue(target, out var entry)
            ? entry.Provider
            : throw new NotSupportedException($"未設定儲存目標 {target}（FileManagement:Targets:{target}）。");

    public string GetDefaultContainer(StorageTarget target) =>
        _providers.Value.TryGetValue(target, out var entry)
            ? entry.DefaultContainer
            : throw new NotSupportedException($"未設定儲存目標 {target}（FileManagement:Targets:{target}）。");

    public void Dispose()
    {
        if (!_providers.IsValueCreated) return;
        foreach (var (provider, _) in _providers.Value.Values)
        {
            (provider as IDisposable)?.Dispose();
        }
    }

    private static Dictionary<StorageTarget, (IStorageProvider, string)> Build(FileManagementOptions value, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        return value.Targets.ToDictionary(
            t => t.Key,
            t => (Create(t.Key, t.Value, value.StreamBufferSize, loggerFactory), t.Value.DefaultContainer));
    }

    private static IStorageProvider Create(StorageTarget target, StorageTargetOptions options, int bufferSize, ILoggerFactory loggerFactory) =>
        options.Provider switch
        {
            StorageProviderKind.FileSystem => new FileSystemStorageProvider(
                target, options.RootPath!, bufferSize, loggerFactory.CreateLogger<FileSystemStorageProvider>()),
            StorageProviderKind.S3 => new S3StorageProvider(target, options.S3, loggerFactory.CreateLogger<S3StorageProvider>()),
            StorageProviderKind.AzureBlob => new AzureBlobStorageProvider(target, options.AzureBlob, loggerFactory.CreateLogger<AzureBlobStorageProvider>()),
            _ => throw new NotSupportedException($"不支援的儲存體種類：{options.Provider}")
        };
}
