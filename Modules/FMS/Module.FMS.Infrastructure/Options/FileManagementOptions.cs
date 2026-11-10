using System.ComponentModel.DataAnnotations;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.FMS.Infrastructure.Options;

/// <summary>
/// 檔案管理組態（區段 FileManagement）。
/// <para>
/// 無狀態：檔案一律寫入 <see cref="Targets"/> 指定的儲存體（NAS / MinIO(S3) / Azure Blob），Web / API 節點不保存任何實體檔案或暫存檔，
/// 上傳、加解密、縮圖與浮水印全程於記憶體串流處理。
/// 業務分流：上傳時依 <see cref="Categories"/> 決定儲存目標、允許格式、大小上限、是否加密與是否僅供檢視，用戶端無法自行指定。
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

    public UploadOptions Upload { get; set; } = new();

    public ImageProcessingOptions Image { get; set; } = new();

    public ThumbnailCacheOptions ThumbnailCache { get; set; } = new();

    public WatermarkOptions Watermark { get; set; } = new();

    public FilePurgeOptions Purge { get; set; } = new();

    /// <summary>未設定 AllowedExtensions 時的內建白名單（不含 SVG / HTML / 可執行檔 / 壓縮檔）</summary>
    public static readonly string[] DefaultAllowedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff",
        ".pdf", ".docx", ".xlsx", ".pptx", ".doc", ".xls", ".ppt", ".txt", ".csv"
    ];
}

/// <summary>儲存體實作種類（環境切換只需變更 FileManagement:Targets:{Target}:Provider）</summary>
public enum StorageProviderKind
{
    /// <summary>檔案系統：地端 NAS（SMB / UNC、NFS 掛載路徑）；本機開發亦可指向非應用程式目錄</summary>
    FileSystem = 0,

    /// <summary>S3 相容物件儲存：MinIO（地端 / 本機 Docker）或 AWS S3</summary>
    S3 = 1,

    /// <summary>Azure Blob Storage（雲端；本機可用 Azurite 模擬）</summary>
    AzureBlob = 2
}

public sealed class StorageTargetOptions
{
    public StorageProviderKind Provider { get; set; } = StorageProviderKind.FileSystem;

    /// <summary>
    /// 檔案系統根目錄（絕對路徑或 UNC，例如 \\nas01\physlims-private）。
    /// 該目錄須於作業系統 / IIS / Nginx 層級關閉「執行」權限，僅授予應用程式集區帳號讀寫。
    /// </summary>
    public string? RootPath { get; set; }

    /// <summary>未由分類指定容器時使用的容器名稱（檔案系統為子目錄、S3 為 Bucket、Azure 為 Container）</summary>
    [Required(AllowEmptyStrings = false)]
    [RegularExpression("^[a-z0-9][a-z0-9-]{1,62}$", ErrorMessage = "容器名稱僅允許小寫英數與連字號（2~63 字元，相容 S3 Bucket 規則）。")]
    public string DefaultContainer { get; set; } = "files";

    public S3StorageOptions S3 { get; set; } = new();

    public AzureBlobStorageOptions AzureBlob { get; set; } = new();
}

/// <summary>S3 相容儲存設定；AccessKey / SecretKey 屬機密，僅能由環境變數或 claudesettings.json 注入</summary>
public sealed class S3StorageOptions
{
    /// <summary>服務端點（MinIO 例：https://minio.internal:9000）；空白表示 AWS S3（依 Region）</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>區域（AWS 必填；MinIO 可沿用 us-east-1）</summary>
    public string Region { get; set; } = "us-east-1";

    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    /// <summary>路徑式定址（MinIO 必須為 true）</summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>容器（Bucket）不存在時自動建立</summary>
    public bool AutoCreateBucket { get; set; }

    /// <summary>多段上傳分段大小（MB，S3 最小 5MB）；分段於記憶體緩衝</summary>
    [Range(5, 512)]
    public int PartSizeMb { get; set; } = 8;
}

/// <summary>Azure Blob 設定；ConnectionString 屬機密，僅能由環境變數或 claudesettings.json 注入</summary>
public sealed class AzureBlobStorageOptions
{
    /// <summary>連線字串（Azurite 例：UseDevelopmentStorage=true）；含帳戶金鑰時才能產生 SAS 預簽名網址</summary>
    public string? ConnectionString { get; set; }

    /// <summary>容器不存在時自動建立</summary>
    public bool AutoCreateContainer { get; set; }
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

    /// <summary>
    /// 儲存時是否加密（AES-256-GCM 信封加密）。預設 true（儲存必加密）；
    /// 僅公開且需 CDN / 預簽名網址直連的資源（例如公開商品圖）才設為 false。
    /// </summary>
    public bool Encrypt { get; set; } = true;

    /// <summary>僅供檢視：通用端點不提供原檔下載，只能經 /view 取得帶動態浮水印的內容（業務模組 Internal 存取不受限）</summary>
    public bool ViewOnly { get; set; }

    /// <summary>瀏覽器 / CDN 快取秒數；地端機密資源一律為 private 快取</summary>
    [Range(0, 31536000)]
    public int CacheMaxAgeSeconds { get; set; } = 86400;
}

/// <summary>上傳（區段 FileManagement:Upload）：無本機暫存，需完整內容的格式於記憶體緩衝並限制併發</summary>
public sealed class UploadOptions
{
    /// <summary>
    /// 需完整內容才能驗證 / 處理的格式（圖片重新編碼、OOXML 結構檢查）之記憶體緩衝上限（位元組）；
    /// 超過即拒絕（413）。其餘格式（PDF、文字、舊版 Office）以串流邊收邊加密，不受此限。
    /// </summary>
    [Range(1, long.MaxValue)]
    public long MaxInMemoryBufferBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>同時進行記憶體緩衝的上傳數上限（超過者排隊），避免高併發大檔耗盡記憶體</summary>
    [Range(1, 256)]
    public int MaxConcurrentBufferedUploads { get; set; } = 8;

    /// <summary>加密分塊大小（位元組）；影響 Range 讀取粒度</summary>
    [Range(4096, 4 * 1024 * 1024)]
    public int EncryptionChunkSize { get; set; } = 64 * 1024;
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

/// <summary>縮圖快取（區段 FileManagement:ThumbnailCache）：各節點記憶體快取（有容量上限），不寫入本機磁碟</summary>
public sealed class ThumbnailCacheOptions
{
    /// <summary>停用時每次請求即時產生縮圖（仍有 HTTP ETag / Cache-Control）</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>快取容量上限（位元組），超過時依存取頻率與時間淘汰</summary>
    [Range(1, long.MaxValue)]
    public long MaxMemoryBytes { get; set; } = 256L * 1024 * 1024;

    /// <summary>未被存取的縮圖保留時間（分鐘）</summary>
    [Range(1, 10080)]
    public int SlidingExpirationMinutes { get; set; } = 60;
}

/// <summary>動態浮水印檢視（區段 FileManagement:Watermark）：浮水印繪入像素（點陣化），無法以圖層移除</summary>
public sealed class WatermarkOptions
{
    /// <summary>PDF 逐頁渲染解析度（DPI）；越高越清晰但檔案越大、耗用越多 CPU</summary>
    [Range(72, 300)]
    public int Dpi { get; set; } = 120;

    /// <summary>單次檢視最多渲染頁數（超過拒絕，防 DoS）</summary>
    [Range(1, 500)]
    public int MaxPages { get; set; } = 50;

    /// <summary>浮水印不透明度（0~255）</summary>
    [Range(10, 255)]
    public int Opacity { get; set; } = 80;

    /// <summary>輸出頁面影像的 JPEG 品質</summary>
    [Range(30, 100)]
    public int JpegQuality { get; set; } = 80;

    /// <summary>浮水印字型檔（支援中文，例如 Noto Sans TC）；空白時使用內建搜尋路徑與系統字型</summary>
    public string? FontPath { get; set; }
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
