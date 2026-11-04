using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.FileManagement;
using SGSFramework.Core.Abstractions.Permissions.Enums;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.FMS.Controllers.V1;

/// <summary>
/// 檔案管理：上傳（Magic Number 驗證 + 圖片二次編碼）、串流下載、動態縮圖、虛擬刪除與排程清除。
/// 業務模組需要「依資料列授權」時，應自行注入 <see cref="IFileManagementService"/> 並先完成業務授權，而非開放此通用端點。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/files")]
[ControllerTitle("檔案管理", Icon = "fa-solid fa-folder-open", Order = 27, Description = "上傳、下載、縮圖與刪除系統檔案（地端機密 / 公開資源分流儲存）")]
[Produces(MediaTypeNames.Application.Json)]
[FileRejectedExceptionFilter]
public sealed class FilesController(IFileManagementService fileService, ILogger<FilesController> logger) : ControllerBase
{
    public sealed class FileUploadForm
    {
        /// <summary>上傳的檔案</summary>
        public IFormFile? File { get; set; }

        /// <summary>業務分類代碼（FileManagement:Categories）；空白使用預設分類</summary>
        public string? Category { get; set; }

        public string? ReferenceType { get; set; }

        public string? ReferenceId { get; set; }
    }

    /// <summary>上傳檔案（multipart/form-data）</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [Function("UploadFile", "上傳檔案", Icon = "fa-solid fa-upload", Order = 1, Description = "驗證檔案簽章後上傳至分類對應的儲存體")]
    [RequiresPermission("FMS.FILE.UPLOAD", "上傳檔案", ActionCategory.Operational)]
    [ProducesResponseType(typeof(FileMetadataDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> Upload([FromForm] FileUploadForm form, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (form.File is null || form.File.Length == 0)
        {
            return Rejected(new FileRejectedException("請選擇要上傳的檔案。"));
        }

        try
        {
            await using var content = form.File.OpenReadStream();
            var result = await fileService.UploadAsync(
                new FileUploadRequest(content, form.File.FileName, form.Category, form.ReferenceType, form.ReferenceId),
                FileAccessScope.GenericApi, cancellationToken).ConfigureAwait(false);

            return CreatedAtAction(nameof(Get), new { id = result.Id, version = "1" }, result);
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

    /// <summary>分頁查詢檔案中繼資料</summary>
    [HttpPost("search")]
    [Consumes(MediaTypeNames.Application.Json)]
    [Function("SearchFiles", "查詢檔案", Icon = "fa-solid fa-magnifying-glass", Order = 2, Description = "分頁查詢檔案中繼資料，支援分類、關聯與上傳人員篩選")]
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
    /// 下載檔案：公開資源且儲存體支援預簽名網址時 302 導向；其餘以 64KB 緩衝串流代理（支援 Range 續傳與 ETag）。
    /// </summary>
    /// <param name="inline">圖片 / PDF 是否以 inline 顯示（預設為附件下載）</param>
    [HttpGet("{id:guid}/content")]
    [Produces(MediaTypeNames.Application.Octet)]
    [Function("DownloadFile", "下載檔案", Icon = "fa-solid fa-download", Order = 4)]
    [RequiresPermission("FMS.FILE.READ", "查詢 / 下載檔案")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
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

        var canInline = inline && (metadata.ContentType.StartsWith("image/", StringComparison.Ordinal) || metadata.ContentType == MediaTypeNames.Application.Pdf);
        var etag = new EntityTagHeaderValue($"\"{metadata.Sha256}\"");

        // FileStreamResult 以 64KB 緩衝區非同步複製串流，不整檔讀入記憶體
        return canInline
            ? File(download.Content!, metadata.ContentType, metadata.CreatedAtUtc, etag, enableRangeProcessing: true)
            : File(download.Content!, metadata.ContentType, metadata.OriginalFileName, metadata.CreatedAtUtc, etag, enableRangeProcessing: true);
    }

    /// <summary>
    /// 動態縮圖：尺寸須在 FileManagement:Image:AllowedSizes 白名單內（防 DoS），成品快取於本機磁碟並以 LRU 清理。
    /// </summary>
    /// <param name="w">寬度（0 或省略表示依比例）</param>
    /// <param name="h">高度（0 或省略表示依比例）</param>
    /// <param name="m">模式：max（預設，不放大）、crop、pad</param>
    [HttpGet("images/{id:guid}")]
    [Produces("image/jpeg", "image/png", "image/gif", "image/webp")]
    [Function("GetImageThumbnail", "圖片縮圖", Icon = "fa-solid fa-image", Order = 5)]
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
    [Function("DeleteFile", "刪除檔案", Icon = "fa-solid fa-trash", Order = 6, Description = "虛擬刪除檔案，保留期滿後由排程清除實體檔案")]
    [RequiresPermission("FMS.FILE.DELETE", "刪除檔案", ActionCategory.Critical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default) =>
        await fileService.DeleteAsync(id, FileAccessScope.GenericApi, cancellationToken).ConfigureAwait(false) ? NoContent() : NotFound();

    /// <summary>清除保留期滿的已刪除檔案實體（排程以系統帳號 scope: files.purge 呼叫，亦可由具權限的管理者手動執行）</summary>
    [HttpPost("purge")]
    [Function("PurgeDeletedFiles", "清除已刪除檔案", Icon = "fa-solid fa-broom", Order = 7, Description = "刪除保留期滿的虛擬刪除檔案實體")]
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
