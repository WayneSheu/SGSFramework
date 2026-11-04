using SGSFramework.Core.FileManagement;

namespace SGS.Modules.FMS.Infrastructure.Storage;

/// <summary>
/// 儲存體抽象：每個 <see cref="StorageTarget"/> 對應一個實作（由 <see cref="IStorageFactory"/> 依組態建立）。
/// fileKey 一律由系統產生（yyyy/MM/{Guid}{副檔名}），實作仍須自行防範路徑遍歷。
/// </summary>
public interface IStorageProvider
{
    StorageTarget Target { get; }

    /// <summary>是否支援預簽名網址（支援時公開資源以 302 導向直連，釋放 Web 伺服器頻寬）</summary>
    bool SupportsPresignedUrl { get; }

    /// <summary>以串流寫入（不得整檔讀入記憶體）；同鍵已存在時拋出 <see cref="IOException"/></summary>
    Task UploadAsync(string containerName, string fileKey, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>開啟唯讀串流，由呼叫端負責 Dispose；檔案不存在時拋出 <see cref="FileNotFoundException"/></summary>
    Task<Stream> OpenReadAsync(string containerName, string fileKey, CancellationToken cancellationToken = default);

    /// <summary>產生具時效性的下載網址；不支援時拋出 <see cref="NotSupportedException"/></summary>
    Task<Uri> GetPresignedUrlAsync(string containerName, string fileKey, TimeSpan expiry, string? downloadFileName = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string containerName, string fileKey, CancellationToken cancellationToken = default);

    /// <summary>刪除實體檔案（冪等：不存在時回傳 false，不拋例外）</summary>
    Task<bool> DeleteAsync(string containerName, string fileKey, CancellationToken cancellationToken = default);
}

/// <summary>依儲存目標取得儲存體實作（工廠動態分流）</summary>
public interface IStorageFactory
{
    IStorageProvider GetProvider(StorageTarget target);

    /// <summary>該儲存目標未指定容器時使用的預設容器</summary>
    string GetDefaultContainer(StorageTarget target);
}
