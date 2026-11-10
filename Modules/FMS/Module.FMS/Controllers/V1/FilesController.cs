using System.Net.Mime;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using SGS.Modules.FMS.Application.Auditing;
using SGS.Modules.FMS.Application.Services;
using SGS.Modules.FMS.Infrastructure.Options;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Core.Abstractions.Permissions.Enums;
using SGSFramework.Core.FileManagement;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.FMS.Controllers.V1;

/// <summary>可上傳分類資訊</summary>
/// <param name="Code">分類代碼</param>
/// <param name="IsDefault">是否為預設分類</param>
/// <param name="AllowedExtensions">允許的副檔名（含句點）</param>
/// <param name="MaxFileSizeBytes">單檔大小上限（位元組）</param>
/// <param name="Encrypt">是否加密儲存</param>
/// <param name="ViewOnly">是否僅供線上檢視（不提供原檔下載）</param>
public sealed record FileCategoryDto(string Code, bool IsDefault, string[] AllowedExtensions, long MaxFileSizeBytes, bool Encrypt, bool ViewOnly);

/// <summary>
/// 檔案管理（無狀態安全儲存）：串流上傳（Magic Number 驗證、圖片二次編碼、AES-256-GCM 加密、傳輸簽章）、
/// 串流解密下載、浮水印檢視、記憶體縮圖、虛擬刪除與排程清除；所有存取寫入 Ledger 稽核紀錄。
/// 業務模組需要「依資料列授權」時，應自行注入 <see cref="IFileManagementService"/> 並先完成業務授權，而非開放此通用端點。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/files")]
[ControllerTitle("檔案管理", Icon = "fa-solid fa-folder-open", Order = 27, Description = "上傳、下載、浮水印檢視、縮圖與刪除系統檔案（加密儲存、存取稽核）")]
[Produces(MediaTypeNames.Application.Json)]
[FileRejectedExceptionFilter]
public sealed class FilesController(
    IFileManagementService fileService,
    IFileAccessLogQueryService accessLogQuery,
    IFileMaintenanceService maintenanceService,
    TimeProvider timeProvider,
    ILogger<FilesController> logger) : ControllerBase
{
    /// <summary>傳輸簽章標頭：用戶端計算的原始內容 SHA-256（十六進位或 Base64）</summary>
    public const string ContentSha256Header = "X-Content-SHA256";

    private const int MaxFormFieldLength = 1024;

    /// <summary>
    /// 上傳檔案（multipart/form-data，串流處理不落地）。分類與關聯可由查詢字串或「位於檔案之前」的表單欄位
    /// （category / referenceType / referenceId）提供；可附 X-Content-SHA256 標頭驗證傳輸完整性。
    /// </summary>
    [HttpPost]
    [DisableFormValueModelBinding]
    [Consumes("multipart/form-data")]
    [Function("UploadFile", "上傳檔案", Icon = "fa-solid fa-upload", Order = 1, Description = "驗證檔案簽章後加密上傳至分類對應的儲存體")]
    [RequiresPermission("FMS.FILE.UPLOAD", "上傳檔案", ActionCategory.Operational)]
    [ProducesResponseType(typeof(FileMetadataDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> Upload(
        [FromQuery] string? category,
        [FromQuery] string? referenceType,
        [FromQuery] string? referenceId,
        [FromHeader(Name = ContentSha256Header)] string? contentSha256,
        CancellationToken cancellationToken = default)
    {
        if (!MediaTypeHeaderValue.TryParse(Request.ContentType, out var mediaType) ||
            !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return Rejected(new FileRejectedException("請以 multipart/form-data 上傳檔案。", 415));
        }

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary)) return Rejected(new FileRejectedException("multipart 邊界 (boundary) 不正確。"));

        try
        {
            var reader = new MultipartReader(boundary, Request.Body);
            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)) continue;

                if (disposition.IsFileDisposition())
                {
                    // 檔案內容直接以請求串流交給服務：邊收邊驗證、雜湊、加密並寫入儲存體
                    var fileName = disposition.FileNameStar.HasValue ? disposition.FileNameStar.Value : disposition.FileName.Value;
                    var result = await fileService.UploadAsync(
                        new FileUploadRequest(section.Body, fileName ?? string.Empty, category, referenceType, referenceId, contentSha256),
                        FileAccessScope.GenericApi, cancellationToken).ConfigureAwait(false);

                    return CreatedAtAction(nameof(Get), new { id = result.Id, version = "1" }, result);
                }

                if (disposition.IsFormDisposition())
                {
                    var value = await ReadFormFieldAsync(section, cancellationToken).ConfigureAwait(false);
                    switch (disposition.Name.Value?.ToLowerInvariant())
                    {
                        case "category": category ??= value; break;
                        case "referencetype": referenceType ??= value; break;
                        case "referenceid": referenceId ??= value; break;
                    }
                }
            }

            return Rejected(new FileRejectedException("請選擇要上傳的檔案。"));
        }
        catch (FileRejectedException ex)
        {
            return Rejected(ex);
        }
        catch (InvalidDataException ex)
        {
            return Rejected(new FileRejectedException($"multipart 內容格式不正確：{ex.Message}"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
    }

    /// <summary>可由通用端點上傳的檔案分類（供上傳介面動態顯示允許格式與大小上限）</summary>
    [HttpGet("categories")]
    [Function("GetFileCategories", "上傳分類", Icon = "fa-solid fa-tags", Order = 11, Description = "取得可上傳的檔案分類、允許格式與大小上限")]
    [RequiresPermission("FMS.FILE.UPLOAD", "上傳檔案", ActionCategory.Operational)]
    [ProducesResponseType(typeof(IReadOnlyList<FileCategoryDto>), StatusCodes.Status200OK)]
    public IActionResult GetCategories([FromServices] IOptionsMonitor<FileManagementOptions> options)
    {
        var settings = options.CurrentValue;
        var categories = settings.Categories.Count == 0
            ? new Dictionary<string, FileCategoryOptions>(StringComparer.OrdinalIgnoreCase) { [FileCategoryOptions.GeneralCategory] = new() }
            : settings.Categories;

        IReadOnlyList<FileCategoryDto> result = categories
            .Where(c => c.Value.AllowGenericApi)
            .Select(c => new FileCategoryDto(
                c.Key,
                c.Key.Equals(settings.DefaultCategory, StringComparison.OrdinalIgnoreCase),
                c.Value.AllowedExtensions is { Length: > 0 } exts
                    ? exts.Select(e => e.Trim().ToLowerInvariant()).Select(e => e.StartsWith('.') ? e : "." + e).ToArray()
                    : FileManagementOptions.DefaultAllowedExtensions,
                Math.Min(c.Value.MaxFileSizeBytes ?? settings.MaxFileSizeBytes, settings.MaxFileSizeBytes),
                c.Value.Encrypt,
                c.Value.ViewOnly))
            .OrderByDescending(c => c.IsDefault).ThenBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Ok(result);
    }

    /// <summary>分頁查詢檔案中繼資料</summary>
    [HttpPost("search")]
    [Consumes(MediaTypeNames.Application.Json)]
    [Function("SearchFiles", "檔案清單", Icon = "fa-solid fa-folder-open", Order = 1, IsMenu = true, Path = "/sgs.modules.fms/files", Description = "分頁查詢檔案中繼資料，支援分類、關聯與上傳人員篩選")]
    [RequiresPermission("FMS.FILE.READ", "查詢 / 下載檔案")]
    [ProducesResponseType(typeof(PagedResult<FileMetadataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] FileQueryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await fileService.SearchAsync(request, FileAccessScope.GenericApi, cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (ArgumentException ex)
        {
            return Rejected(new FileRejectedException(ex.Message));
        }
    }

    /// <summary>取得檔案中繼資料</summary>
    [HttpGet("{id:guid}")]
    [Function("GetFile", "檔案明細", Icon = "fa-solid fa-file", Order = 3)]
    [RequiresPermission("FMS.FILE.READ", "查詢 / 下載檔案")]
    [ProducesResponseType(typeof(FileMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await fileService.GetAsync(id, scope: FileAccessScope.GenericApi, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 下載原檔：未加密的公開資源且儲存體支援預簽名網址時 302 導向；其餘於記憶體串流解密後代理輸出（支援 Range 續傳與 ETag）。
    /// 回應附 Repr-Digest（RFC 9530）供用戶端驗證內容完整性；「僅供檢視」分類回應 403。
    /// </summary>
    /// <param name="inline">圖片 / PDF 是否以 inline 顯示（預設為附件下載）</param>
    [HttpGet("{id:guid}/content")]
    [Produces(MediaTypeNames.Application.Octet)]
    [Function("DownloadFile", "下載檔案", Icon = "fa-solid fa-download", Order = 4)]
    [RequiresPermission("FMS.FILE.READ", "查詢 / 下載檔案")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, [FromQuery] bool inline = false, CancellationToken cancellationToken = default)
    {
        var download = await fileService.OpenDownloadAsync(id, FileAccessScope.GenericApi, cancellationToken).ConfigureAwait(false);
        if (download is null) return NotFound();

        if (download.RedirectUri is not null)
        {
            Response.Headers.CacheControl = "no-store"; // 預簽名網址具時效，導向回應本身不可快取
            return Redirect(download.RedirectUri.ToString());
        }

        var metadata = download.Metadata;
        ApplySecurityHeaders(metadata.StorageTarget, download.CacheMaxAgeSeconds);
        Response.Headers["Repr-Digest"] = $"sha-256=:{Convert.ToBase64String(Convert.FromHexString(metadata.Sha256))}:";

        var canInline = inline && (metadata.ContentType.StartsWith("image/", StringComparison.Ordinal) || metadata.ContentType == MediaTypeNames.Application.Pdf);
        var etag = new EntityTagHeaderValue($"\"{metadata.Sha256}\"");
        var content = download.Content!;

        // FileStreamResult 以 64KB 緩衝區非同步複製串流（解密逐區塊進行），不整檔讀入記憶體；串流可定位時支援 Range
        return canInline
            ? File(content, metadata.ContentType, metadata.CreatedAtUtc, etag, enableRangeProcessing: content.CanSeek)
            : File(content, metadata.ContentType, metadata.OriginalFileName, metadata.CreatedAtUtc, etag, enableRangeProcessing: content.CanSeek);
    }

    /// <summary>
    /// 線上檢視（動態浮水印）：浮水印內容為「使用者 | UTC 時間 | IP」，以點陣化方式繪入每頁像素，無法以圖層或網頁編輯移除。
    /// 支援 PDF 與圖片；回應禁止快取。
    /// </summary>
    [HttpGet("{id:guid}/view")]
    [Produces(MediaTypeNames.Application.Pdf, "image/jpeg", "image/png")]
    [Function("ViewFile", "檢視檔案（浮水印）", Icon = "fa-solid fa-eye", Order = 5, Description = "以動態浮水印線上檢視 PDF / 圖片")]
    [RequiresPermission("FMS.FILE.READ", "查詢 / 下載檔案")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> View(Guid id, CancellationToken cancellationToken = default)
    {
        var view = await fileService.OpenWatermarkedViewAsync(id, BuildWatermarkText(), FileAccessScope.GenericApi, cancellationToken).ConfigureAwait(false);
        if (view is null) return NotFound();

        // 浮水印內容因人、因時而異：一律不可快取
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self' data:; style-src 'unsafe-inline'; sandbox";
        Response.Headers["Content-Digest"] = $"sha-256=:{Convert.ToBase64String(SHA256.HashData(view.Content))}:";
        return File(view.Content, view.ContentType);
    }

    /// <summary>
    /// 動態縮圖：尺寸須在 FileManagement:Image:AllowedSizes 白名單內（防 DoS），成品快取於伺服器記憶體（不落地磁碟）。
    /// </summary>
    /// <param name="w">寬度（0 或省略表示依比例）</param>
    /// <param name="h">高度（0 或省略表示依比例）</param>
    /// <param name="m">模式：max（預設，不放大）、crop、pad</param>
    [HttpGet("images/{id:guid}")]
    [Produces("image/jpeg", "image/png", "image/gif", "image/webp")]
    [Function("GetImageThumbnail", "圖片縮圖", Icon = "fa-solid fa-image", Order = 6)]
    [RequiresPermission("FMS.FILE.READ", "查詢 / 下載檔案")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id, [FromQuery] int? w, [FromQuery] int? h, [FromQuery] string? m, CancellationToken cancellationToken = default)
    {
        try
        {
            var image = await fileService.GetImageAsync(id, w, h, m, FileAccessScope.GenericApi, cancellationToken).ConfigureAwait(false);
            if (image is null) return NotFound();

            ApplySecurityHeaders(image.Metadata.StorageTarget, image.CacheMaxAgeSeconds);
            return File(image.Content, image.Metadata.ContentType, image.Metadata.CreatedAtUtc, new EntityTagHeaderValue(image.ETag));
        }
        catch (FileRejectedException ex)
        {
            return Rejected(ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
    }

    /// <summary>刪除檔案（虛擬刪除；實體檔案於保留期滿後由排程清除）</summary>
    [HttpDelete("{id:guid}")]
    [Function("DeleteFile", "刪除檔案", Icon = "fa-solid fa-trash", Order = 7, Description = "虛擬刪除檔案，保留期滿後由排程清除實體檔案")]
    [RequiresPermission("FMS.FILE.DELETE", "刪除檔案", ActionCategory.Critical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default) =>
        await fileService.DeleteAsync(id, FileAccessScope.GenericApi, cancellationToken).ConfigureAwait(false) ? NoContent() : NotFound();

    /// <summary>分頁查詢檔案存取稽核紀錄（Ledger，區分人員與系統帳號）</summary>
    [HttpPost("access-logs/search")]
    [Consumes(MediaTypeNames.Application.Json)]
    [Function("SearchFileAccessLogs", "檔案存取紀錄", Icon = "fa-solid fa-clipboard-list", Order = 2, IsMenu = true, Path = "/sgs.modules.fms/accesslog", Description = "查詢檔案上傳、下載、檢視、刪除與完整性失敗等存取稽核紀錄")]
    [RequiresPermission("FMS.FILE.AUDIT", "檢視檔案存取紀錄")]
    [ProducesResponseType(typeof(PagedResult<FileAccessLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAccessLogs([FromBody] FileAccessLogQueryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await accessLogQuery.SearchAsync(request, cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (ArgumentException ex)
        {
            return Rejected(new FileRejectedException(ex.Message));
        }
    }

    /// <summary>清除保留期滿的已刪除檔案實體（排程以系統帳號 scope: files.purge 呼叫，亦可由具權限的管理者手動執行）</summary>
    [HttpPost("purge")]
    [Function("PurgeDeletedFiles", "清除已刪除檔案", Icon = "fa-solid fa-broom", Order = 9, Description = "刪除保留期滿的虛擬刪除檔案實體")]
    [RequiresSystemScope("files.purge", Description = "清除已刪除檔案實體", AllowUsers = true)]
    [RequiresPermission("FMS.FILE.PURGE", "清除已刪除檔案", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(FilePurgeResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Purge(CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await fileService.PurgeDeletedAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
    }

    /// <summary>
    /// 將舊有明文檔案轉為加密儲存（每次最多 <paramref name="maxFiles"/> 筆，可重複執行直到 Remaining = 0）。
    /// 供系統帳號（scope: files.maintenance）或具權限的管理者執行。
    /// </summary>
    [HttpPost("maintenance/encrypt-legacy")]
    [Function("EncryptLegacyFiles", "舊檔加密", Icon = "fa-solid fa-lock", Order = 10, Description = "將改版前的明文檔案轉為 AES-256-GCM 加密儲存")]
    [RequiresSystemScope("files.maintenance", Description = "檔案儲存維護（舊檔加密）", AllowUsers = true)]
    [RequiresPermission("FMS.FILE.PURGE", "清除已刪除檔案", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(EncryptLegacyResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> EncryptLegacy([FromQuery] int maxFiles = 200, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await maintenanceService.EncryptLegacyAsync(maxFiles, cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
    }

    /// <summary>浮水印文字：人員以帳號名稱、系統帳號以 client_id 識別，加上 UTC 時間與來源 IP</summary>
    private string BuildWatermarkText()
    {
        var who = User.IsSystemAccount()
            ? User.FindFirst(SystemAccountClaims.ClientId)?.Value ?? User.Identity?.Name ?? "SystemAccount"
            : User.Identity?.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "未知使用者";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-";
        return $"{who} | {timeProvider.GetUtcNow():yyyy-MM-dd HH:mm:ss} UTC | {ip}";
    }

    /// <summary>
    /// 快取與內容安全標頭：地端機密資源僅允許瀏覽器私有快取（禁止 CDN / 代理快取）；
    /// 一律 nosniff 並以 CSP sandbox 阻止瀏覽器將檔案當作網頁執行。
    /// </summary>
    private void ApplySecurityHeaders(StorageTarget target, int maxAgeSeconds)
    {
        Response.Headers.CacheControl = target == StorageTarget.CloudPublic
            ? $"public, max-age={maxAgeSeconds}, immutable"
            : $"private, max-age={maxAgeSeconds}";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self' data:; style-src 'unsafe-inline'; sandbox";
    }

    private static async Task<string?> ReadFormFieldAsync(MultipartSection section, CancellationToken cancellationToken)
    {
        var buffer = new char[MaxFormFieldLength + 1];
        using var reader = new StreamReader(section.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        if (read > MaxFormFieldLength) throw new FileRejectedException("表單欄位內容過長。");

        var value = new string(buffer, 0, read).Trim();
        return value.Length == 0 ? null : value;
    }

    private ObjectResult Rejected(FileRejectedException ex)
    {
        logger.LogInformation("[FilesController] 請求被拒絕（{Status}）：{Reason}", ex.StatusCode, ex.Message);
        return StatusCode(ex.StatusCode, new ProblemDetails
        {
            Status = ex.StatusCode,
            Title = "檔案請求被拒絕",
            Detail = ex.Message,
            Instance = HttpContext.Request.Path
        });
    }
}
