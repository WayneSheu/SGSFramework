using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.FileManagement;
using SGS.Modules.FMS.Infrastructure.Storage;
using SGS.Modules.FMS.Infrastructure.Options;

namespace SGS.Modules.FMS.Infrastructure.Storage;

/// <summary>
/// 依組態 FileManagement:Targets 為每個儲存目標建立儲存體實作（Singleton；組態於啟動時驗證）。
/// 新增儲存體（例如 S3 / MinIO、Azure Blob）時，於 <see cref="StorageProviderKind"/> 加入種類並於 <see cref="Create"/> 分派即可，呼叫端不受影響。
/// </summary>
public sealed class StorageFactory : IStorageFactory
{
    private readonly Dictionary<StorageTarget, (IStorageProvider Provider, string DefaultContainer)> _providers;

    public StorageFactory(IOptions<FileManagementOptions> options, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var value = options.Value;
        _providers = value.Targets.ToDictionary(
            t => t.Key,
            t => (Create(t.Key, t.Value, value.StreamBufferSize, loggerFactory), t.Value.DefaultContainer));
    }

    public IStorageProvider GetProvider(StorageTarget target) =>
        _providers.TryGetValue(target, out var entry)
            ? entry.Provider
            : throw new NotSupportedException($"未設定儲存目標 {target}（FileManagement:Targets:{target}）。");

    public string GetDefaultContainer(StorageTarget target) =>
        _providers.TryGetValue(target, out var entry)
            ? entry.DefaultContainer
            : throw new NotSupportedException($"未設定儲存目標 {target}（FileManagement:Targets:{target}）。");

    private static IStorageProvider Create(StorageTarget target, StorageTargetOptions options, int bufferSize, ILoggerFactory loggerFactory) =>
        options.Provider switch
        {
            StorageProviderKind.FileSystem => new FileSystemStorageProvider(
                target, options.RootPath!, bufferSize, loggerFactory.CreateLogger<FileSystemStorageProvider>()),
            _ => throw new NotSupportedException($"不支援的儲存體種類：{options.Provider}")
        };
}
