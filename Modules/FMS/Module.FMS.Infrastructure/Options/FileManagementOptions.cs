using System.ComponentModel.DataAnnotations;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.FMS.Infrastructure.Options;

/// <summary>
/// 檔案管理組態（區段 FileManagement）。
/// <para>
/// 儲存隔離：檔案一律寫入 <see cref="Targets"/> 指定的儲存體（NAS / UNC / 物件儲存），嚴禁指向 wwwroot 或 Web 伺服器本機目錄。
/// 業務分流：上傳時依 <see cref="Categories"/> 決定儲存目標、允許格式與大小上限，用戶端無法自行指定儲存目標。
/// </para>
/// 陣列預設值一律為空：.NET 組態繫結會「附加」到既有陣列，空白時改用程式內建清單。
/// </summary>
public sealed class FileManagementOptions
{
    public const string SectionName = "FileManagement";

    /// <summary>是否啟用檔案管理；關閉時所有操作回應 503，且不驗證儲存組態、不排入清除排程</summary>
    public bool Enabled { get; set; }

    /// <summary>各儲存目標的儲存體設定（Key = LocalPrivate / CloudPublic）</summary>
    public Dictionary<StorageTarget, StorageTargetOptions> Targets { get; set; } = [];

    /// <summary>業務分類（Key = 分類代碼，不分大小寫）；未設定任何分類時使用內建的 General（LocalPrivate）</summary>
    public Dictionary<string, FileCategoryOptions> Categories { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>上傳未指定分類時使用的分類代碼</summary>
    [Required(AllowEmptyStrings = false)]
    public string DefaultCategory { get; set; } = FileCategoryOptions.GeneralCategory;

    /// <summary>全域單檔大小上限（位元組），分類設定不可超過此值</summary>
    [Range(1, long.MaxValue)]
    public long MaxFileSizeBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>串流讀寫緩衝區大小（位元組）；預設 64KB，低於 85KB 避免配置於大型物件堆積 (LOH)</summary>
    [Range(4096, 81920)]
    public int StreamBufferSize { get; set; } = 64 * 1024;

    /// <summary>上傳暫存目錄（圖片重新編碼、不可定位串流緩衝）；空白時使用系統暫存目錄</summary>
    public string? TempPath { get; set; }

    public ImageProcessingOptions Image { get; set; } = new();

    public ImageCacheOptions ImageCache { get; set; } = new();

    public FilePurgeOptions Purge { get; set; } = new();

    /// <summary>未設定 AllowedExtensions 時的內建白名單（不含 SVG / HTML / 可執行檔 / 壓縮檔）</summary>
    public static readonly string[] DefaultAllowedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff",
        ".pdf", ".docx", ".xlsx", ".pptx", ".doc", ".xls", ".ppt", ".txt", ".csv"
    ];
}

/// <summary>儲存體實作種類</summary>
public enum StorageProviderKind
{
    /// <summary>檔案系統（本機磁碟、NAS 網路共享 UNC / SMB / NFS 掛載路徑）</summary>
    FileSystem = 0
}

public sealed class StorageTargetOptions
{
    public StorageProviderKind Provider { get; set; } = StorageProviderKind.FileSystem;

    /// <summary>
    /// 檔案系統根目錄（絕對路徑或 UNC，例如 \\nas01\physlims-private）。
    /// 該目錄須於作業系統 / IIS / Nginx 層級關閉「執行」權限，僅授予應用程式集區帳號讀寫。
    /// </summary>
    public string? RootPath { get; set; }

    /// <summary>未由分類指定容器時使用的容器名稱（檔案系統為子目錄）</summary>
    [Required(AllowEmptyStrings = false)]
    [RegularExpression("^[a-z0-9][a-z0-9-]{1,62}$", ErrorMessage = "容器名稱僅允許小寫英數與連字號（2~63 字元，相容 S3 Bucket 規則）。")]
    public string DefaultContainer { get; set; } = "files";
}

public sealed class FileCategoryOptions
{
    public const string GeneralCategory = "General";

    /// <summary>儲存目標（依安全等級分流）</summary>
    public StorageTarget Target { get; set; } = StorageTarget.LocalPrivate;

    /// <summary>容器名稱；空白時使用儲存目標的 DefaultContainer</summary>
    [RegularExpression("^[a-z0-9][a-z0-9-]{1,62}$", ErrorMessage = "容器名稱僅允許小寫英數與連字號（2~63 字元，相容 S3 Bucket 規則）。")]
    public string? Container { get; set; }

    /// <summary>允許的副檔名（含句點）；空白時使用內建白名單</summary>
    public string[] AllowedExtensions { get; set; } = [];

    /// <summary>單檔大小上限（位元組）；null 時使用全域上限</summary>
    [Range(1, long.MaxValue)]
    public long? MaxFileSizeBytes { get; set; }

    /// <summary>
    /// 是否允許經通用端點 api/v1/files 上傳 / 查詢 / 下載 / 刪除。需資料列授權的業務檔案（例如正式報告）應設為 false，
    /// 僅能由業務模組在完成自身授權後以 IFileManagementService 存取。
    /// </summary>
    public bool AllowGenericApi { get; set; } = true;

    /// <summary>瀏覽器 / CDN 快取秒數；地端機密資源一律為 private 快取</summary>
    [Range(0, 31536000)]
    public int CacheMaxAgeSeconds { get; set; } = 86400;
}

/// <summary>圖片處理（區段 FileManagement:Image）</summary>
public sealed class ImageProcessingOptions
{
    /// <summary>
    /// 上傳圖片落地前以影像處理器 (Magick.NET) 解碼後重新編碼（洗除 Polyglot 夾帶代碼與 EXIF / GPS 等中繼資料）。
    /// </summary>
    public bool ReencodeOnUpload { get; set; } = true;

    /// <summary>允許的最大寬 / 高（像素），防止解壓縮炸彈 (Decompression Bomb) 耗盡記憶體</summary>
    [Range(1, 65535)]
    public int MaxDimension { get; set; } = 12000;

    /// <summary>允許的最大總像素數（寬 × 高）</summary>
    [Range(1, long.MaxValue)]
    public long MaxPixels { get; set; } = 80_000_000;

    /// <summary>動畫圖片（GIF / WebP）最多解碼的影格數</summary>
    [Range(1, 1000)]
    public int MaxFrames { get; set; } = 200;

    /// <summary>JPEG 重新編碼品質</summary>
    [Range(1, 100)]
    public int JpegQuality { get; set; } = 85;

    /// <summary>
    /// 動態縮圖尺寸白名單（WxH，0 表示依比例自動，例如 256x0）；空白時使用內建清單。防 DoS：拒絕白名單外的任意尺寸。
    /// </summary>
    public string[] AllowedSizes { get; set; } = [];

    public static readonly string[] DefaultAllowedSizes =
        ["64x64", "128x128", "256x256", "512x512", "1024x1024", "1920x1080", "256x0", "512x0", "1024x0"];
}

/// <summary>縮圖快取（區段 FileManagement:ImageCache），採 Cache-Aside + LRU 水位清理</summary>
public sealed class ImageCacheOptions
{
    /// <summary>停用時每次請求即時產生縮圖（僅建議開發環境）</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>縮圖快取目錄（建議為伺服器本機高速 SSD；屬可重建的衍生資料，不需備份）</summary>
    public string? RootPath { get; set; }

    /// <summary>高水位（位元組）：快取總量超過時開始清理</summary>
    [Range(1, long.MaxValue)]
    public long HighWatermarkBytes { get; set; } = 40L * 1024 * 1024 * 1024;

    /// <summary>低水位（位元組）：依最後存取時間由舊到新刪除直到低於此值</summary>
    [Range(0, long.MaxValue)]
    public long LowWatermarkBytes { get; set; } = 30L * 1024 * 1024 * 1024;

    /// <summary>檢查週期（分鐘）</summary>
    [Range(1, 1440)]
    public int CheckIntervalMinutes { get; set; } = 10;
}

/// <summary>虛擬刪除檔案的實體清除（區段 FileManagement:Purge）</summary>
public sealed class FilePurgeOptions
{
    /// <summary>虛擬刪除後保留天數，期滿才清除實體檔案（保留期間可由 DBA 還原）</summary>
    [Range(0, 3650)]
    public int RetentionDays { get; set; } = 30;

    /// <summary>每批處理筆數</summary>
    [Range(10, 5000)]
    public int BatchSize { get; set; } = 200;

    /// <summary>單次執行最多處理批數（避免單次作業過久）</summary>
    [Range(1, 1000)]
    public int MaxBatchesPerRun { get; set; } = 50;
}
