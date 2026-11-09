using SGSFramework.Core.Paginations;

namespace SGSFramework.Core.FileManagement;

// =====================================================================================
// 檔案管理跨模組契約（外掛模組 SGS.Modules.FMS 實作）
// 契約置於 Core（Default ALC 共享），使 RPT 等外掛與 FMS 外掛的型別一致；
// FMS 未安裝時由 NullFileManagementService 回應 503。
// =====================================================================================

/// <summary>
/// 檔案儲存目標（依安全等級實體隔離）。實際對應的儲存體由組態 FileManagement:Targets 決定。
/// </summary>
public enum StorageTarget
{
    /// <summary>地端機密資源（企業內網 NAS / MinIO），外網僅能經 API 串流代理存取</summary>
    LocalPrivate = 0,

    /// <summary>公開資源（雲端物件儲存 / CDN），支援預簽名網址時以 302 導向直連下載</summary>
    CloudPublic = 1
}

/// <summary>
/// 存取管道：Internal 為業務模組直接呼叫（已自行完成資料列授權）；GenericApi 為通用端點 api/v1/files，
/// 不可存取 AllowGenericApi = false 的分類（例如正式報告），避免繞過業務授權。
/// </summary>
public enum FileAccessScope
{
    Internal = 0,
    GenericApi = 1
}

/// <summary>檔案被拒絕（格式不符、超過大小、分類未定義、縮圖尺寸不在白名單、模組未安裝等），屬可預期的錯誤</summary>
public sealed class FileRejectedException(string message, int statusCode = 400) : Exception(message)
{
    /// <summary>建議的 HTTP 狀態碼（400 / 403 / 413 / 415 / 503）</summary>
    public int StatusCode { get; } = statusCode;
}

public interface IFileManagementService
{
    /// <summary>
    /// 上傳（無本機暫存）：分類分流 → Magic Number 驗證 → 圖片二次編碼 → 串流計算 SHA-256 並以 AES-256-GCM 信封加密
    /// → 直接寫入儲存體（Guid 檔名）→ 寫入中繼資料與存取稽核。
    /// 提供 <see cref="FileUploadRequest.ExpectedSha256"/> 時比對傳輸完整性；中繼資料寫入失敗時回補刪除實體檔案。
    /// 驗證失敗拋出 <see cref="FileRejectedException"/>。
    /// </summary>
    Task<FileMetadataDto> UploadAsync(FileUploadRequest request, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default);

    /// <summary>取得中繼資料；不存在回傳 null（includeDeleted = false 時已刪除亦回傳 null）</summary>
    Task<FileMetadataDto?> GetAsync(Guid id, bool includeDeleted = false, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default);

    Task<PagedResult<FileMetadataDto>> SearchAsync(FileQueryRequest request, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default);

    /// <summary>
    /// 開啟下載（記憶體內串流解密，可定位時支援 Range）；不存在或已刪除回傳 null。回傳串流由呼叫端 Dispose。
    /// 通用端點下載「僅供檢視」分類時拋出 403；解密 / 完整性驗證失敗時讀取串流會拋出 CryptographicException。
    /// </summary>
    Task<FileDownload?> OpenDownloadAsync(Guid id, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default);

    /// <summary>取得動態縮圖（尺寸 / 模式須在白名單內）；不存在、已刪除或非圖片回傳 null</summary>
    Task<FileImageContent?> GetImageAsync(Guid id, int? width, int? height, string? mode, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default);

    /// <summary>
    /// 帶動態浮水印的檢視內容（PDF 逐頁點陣化並將浮水印繪入像素、圖片直接繪製），全程於記憶體處理；
    /// 不存在或已刪除回傳 null，不支援的格式拋出 415。
    /// </summary>
    /// <param name="watermarkText">浮水印文字（例如「使用者 | UTC 時間 | IP」）</param>
    Task<FileViewContent?> OpenWatermarkedViewAsync(Guid id, string watermarkText, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default);

    /// <summary>虛擬刪除（僅標記 IsDeleted，實體檔案由排程於保留期滿後清除）；不存在或已刪除回傳 false</summary>
    Task<bool> DeleteAsync(Guid id, FileAccessScope scope = FileAccessScope.Internal, CancellationToken cancellationToken = default);

    /// <summary>清除保留期滿的已刪除檔案實體（供排程以系統帳號呼叫）</summary>
    Task<FilePurgeResult> PurgeDeletedAsync(CancellationToken cancellationToken = default);
}

/// <summary>上傳請求（與 ASP.NET 無關，業務模組可直接呼叫服務）</summary>
/// <param name="Content">檔案內容（可為不可定位的網路串流；全程記憶體處理，不落地本機）</param>
/// <param name="FileName">原始檔名</param>
/// <param name="Category">業務分類代碼（決定儲存目標與允許格式）；null 使用預設分類</param>
/// <param name="ReferenceType">關聯的業務實體類型</param>
/// <param name="ReferenceId">關聯的業務實體識別碼</param>
/// <param name="ExpectedSha256">傳輸簽章：用戶端計算的原始內容 SHA-256（十六進位）；提供時須與伺服器計算值相符，否則拒絕並移除</param>
public sealed record FileUploadRequest(
    Stream Content,
    string FileName,
    string? Category = null,
    string? ReferenceType = null,
    string? ReferenceId = null,
    string? ExpectedSha256 = null);

/// <summary>檔案中繼資料（不含實體儲存屬性：容器、儲存鍵不對外揭露）</summary>
public sealed record FileMetadataDto(
    Guid Id,
    string OriginalFileName,
    string Extension,
    string ContentType,
    long FileSize,
    string Sha256,
    string Category,
    StorageTarget StorageTarget,
    string? ReferenceType,
    string? ReferenceId,
    int? ImageWidth,
    int? ImageHeight,
    bool IsDeleted,
    DateTimeOffset? DeletedAtUtc,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    bool IsEncrypted = false);

/// <summary>檔案查詢條件</summary>
public record FileQueryFilter
{
    public string? Category { get; init; }
    public StorageTarget? StorageTarget { get; init; }
    public string? ReferenceType { get; init; }
    public string? ReferenceId { get; init; }

    /// <summary>上傳人員（精確比對）</summary>
    public string? CreatedBy { get; init; }

    /// <summary>內容類型前綴（例如 image/）</summary>
    public string? ContentTypePrefix { get; init; }

    /// <summary>是否包含已刪除檔案</summary>
    public bool IncludeDeleted { get; init; }

    public DateTimeOffset? StartTimestamp { get; init; }
    public DateTimeOffset? EndTimestamp { get; init; }
}

/// <summary>分頁查詢（SearchTerm 比對原始檔名）</summary>
public record FileQueryRequest : PagedQueryParameters<FileQueryFilter>;

/// <summary>下載內容：Content 與 RedirectUri 擇一（支援預簽名網址的公開資源回傳 RedirectUri）</summary>
public sealed record FileDownload(FileMetadataDto Metadata, Stream? Content, Uri? RedirectUri, int CacheMaxAgeSeconds);

/// <summary>帶浮水印的檢視內容（記憶體內產生）</summary>
public sealed record FileViewContent(FileMetadataDto Metadata, byte[] Content, string ContentType);

/// <summary>縮圖內容</summary>
public sealed record FileImageContent(FileMetadataDto Metadata, Stream Content, string ETag, int CacheMaxAgeSeconds);

public sealed record FilePurgeResult(int Purged, int Failed, int RetentionDays, int ThumbnailsRemoved, DateTimeOffset StartedAt, DateTimeOffset CompletedAt);
