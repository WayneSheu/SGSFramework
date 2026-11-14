using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Controllers.V1;

/// <summary>
/// UAT 驗收簽核單（FORM-UAT-01，SOP 第 5 章）：PM 建立、客戶驗收（目前由 PM 代錄）。
/// 功能權限由 [RequiresPermission] 控管；誰能做哪個動作由專案角色（RACI）在服務層判斷，非專案內部成員一律 404。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/pms/uat-signoffs")]
[ControllerTitle("UAT 驗收", Icon = "fa-solid fa-clipboard-check", Order = 39, Description = "UAT 驗收簽核單")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class UatSignoffsController(UatService uat, UatAttachmentService attachments, TimeProvider timeProvider) : PmsControllerBase
{
    /// <summary>附件 20MB（對應 FMS 分類 pms-issue）加表單欄位與 multipart 開銷。</summary>
    private const long MaxUploadBytes = 22L * 1024 * 1024;

    private const int MaxFormFieldLength = 512;

    public const string ContentSha256Header = "X-Content-SHA256";

    /// <summary>查詢 UAT 驗收簽核單</summary>
    [HttpPost("search")]
    [Function("SearchUatSignoffs", "UAT 驗收簽核", Icon = "fa-solid fa-clipboard-check", Order = 1, Description = "查詢 UAT 驗收簽核單", IsMenu = true, Path = "/sgs.modules.pms/uat-signoffs")]
    [RequiresPermission("PMS.UAT.READ", "檢視 UAT 驗收簽核單")]
    [ProducesResponseType(typeof(PagedResult<UatListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UatListDto>>> Search([FromBody] UatSearchRequest request, CancellationToken ct = default) =>
        Ok(await uat.SearchAsync(CurrentUserId, request ?? new UatSearchRequest(), ct).ConfigureAwait(false));

    /// <summary>建立 UAT 驗收簽核單（僅專案 PM）</summary>
    [HttpPost]
    [Function("CreateUatSignoff", "建立 UAT 驗收簽核單", Order = 2, Description = "PM 建立驗收簽核單")]
    [RequiresPermission("PMS.UAT.CREATE", "建立與處理 UAT 驗收簽核單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(UatDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<UatDetailDto>> Create([FromBody] CreateUatRequest request, CancellationToken ct = default) =>
        FromResult(await uat.CreateAsync(CurrentUserId, Actor, request, ct).ConfigureAwait(false),
            dto => Created($"/api/v1/pms/uat-signoffs/{dto.Id}", dto));

    /// <summary>取得驗收簽核單詳情</summary>
    [HttpGet("{id:guid}")]
    [Function("GetUatSignoff", "UAT 驗收簽核單詳情", Order = 3, Description = "驗收範圍、雜湊與結果")]
    [RequiresPermission("PMS.UAT.READ", "檢視 UAT 驗收簽核單")]
    [ProducesResponseType(typeof(UatDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UatDetailDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await uat.GetAsync(CurrentUserId, id, ct).ConfigureAwait(false));

    /// <summary>記錄客戶驗收結果（客戶外部帳號開放前由專案 PM 代錄，須附原因與依據）</summary>
    [HttpPost("{id:guid}/result")]
    [Function("RecordUatResult", "記錄 UAT 驗收結果", Order = 4, Description = "PM 代錄客戶驗收通過／不通過")]
    [RequiresPermission("PMS.UAT.CREATE", "建立與處理 UAT 驗收簽核單", ActionCategory.Critical)]
    [ProducesResponseType(typeof(UatDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UatDetailDto>> RecordResult(Guid id, [FromBody] RecordUatResultRequest request, CancellationToken ct = default) =>
        FromResult(await uat.RecordResultAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>取消待簽核的驗收簽核單（僅專案 PM）</summary>
    [HttpPost("{id:guid}/cancel")]
    [Function("CancelUatSignoff", "取消 UAT 驗收簽核單", Order = 5, Description = "取消待簽核的驗收單")]
    [RequiresPermission("PMS.UAT.CREATE", "建立與處理 UAT 驗收簽核單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(UatDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UatDetailDto>> Cancel(Guid id, [FromBody] CancelUatRequest request, CancellationToken ct = default) =>
        FromResult(await uat.CancelAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    // ---------- 附件 ----------

    /// <summary>
    /// 上傳驗收附件（multipart/form-data，串流不落地；一次一檔，多檔由前端逐一上傳）：表單欄位 description（檔案說明，選填，預設取檔名）
    /// 須位於檔案區段之前；可附 X-Content-SHA256 標頭。檔名由系統產生，上傳後為「待遮罩確認」。僅專案 PM／Tech Lead。
    /// </summary>
    [HttpPost("{id:guid}/attachments")]
    [DisableFormValueModelBinding]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [Function("UploadUatAttachment", "上傳 UAT 驗收附件", Order = 6, Description = "上傳客戶回覆、簽署掃描檔或驗收截圖至 FMS（加密）")]
    [RequiresPermission("PMS.UAT.CREATE", "建立與處理 UAT 驗收簽核單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(UatAttachmentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<UatAttachmentDto>> UploadAttachment(
        Guid id, [FromHeader(Name = ContentSha256Header)] string? contentSha256, CancellationToken ct = default)
    {
        if (!MediaTypeHeaderValue.TryParse(Request.ContentType, out var mediaType) ||
            !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return ProblemFrom(new PmsError("PMS.FILE_REJECTED", 415, "Unsupported Media Type", "請以 multipart/form-data 上傳檔案。"));
        }

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary))
            return ProblemFrom(PmsError.Validation("multipart 邊界 (boundary) 不正確。"));

        string? description = null;
        try
        {
            var reader = new MultipartReader(boundary, Request.Body);
            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(ct).ConfigureAwait(false)) is not null)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)) continue;

                if (disposition.IsFileDisposition())
                {
                    var fileName = disposition.FileNameStar.HasValue ? disposition.FileNameStar.Value : disposition.FileName.Value;
                    var result = await attachments.UploadAsync(
                        CurrentUserId, Actor, id, description, section.Body, fileName ?? string.Empty,
                        string.IsNullOrWhiteSpace(contentSha256) ? null : contentSha256.Trim(), ct).ConfigureAwait(false);
                    return FromResult(result, dto => Created($"/api/v1/pms/uat-signoffs/{id}", dto));
                }

                if (disposition.IsFormDisposition() &&
                    string.Equals(disposition.Name.Value, "description", StringComparison.OrdinalIgnoreCase))
                {
                    description = await ReadFormFieldAsync(section, ct).ConfigureAwait(false);
                }
            }

            return ProblemFrom(PmsError.Validation("請選擇要上傳的檔案。"));
        }
        catch (InvalidDataException ex)
        {
            return ProblemFrom(PmsError.Validation($"multipart 內容格式不正確：{ex.Message}"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(499);
        }
    }

    /// <summary>附件浮水印檢視（PDF／圖片）；回應禁止快取</summary>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/view")]
    [Produces(MediaTypeNames.Application.Pdf, "image/jpeg", "image/png")]
    [Function("ViewUatAttachment", "檢視 UAT 驗收附件（浮水印）", Order = 7, Description = "以動態浮水印檢視附件")]
    [RequiresPermission("PMS.UAT.READ", "檢視 UAT 驗收簽核單")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ViewAttachment(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        var result = await attachments.OpenViewAsync(CurrentUserId, id, attachmentId, BuildWatermarkText(timeProvider), ct).ConfigureAwait(false);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);

        Response.Headers.CacheControl = "no-store";
        ApplySecurityHeaders();
        Response.Headers["Content-Digest"] = $"sha-256=:{Convert.ToBase64String(SHA256.HashData(result.Value!.Content))}:";
        return File(result.Value.Content, result.Value.ContentType);
    }

    /// <summary>下載附件原檔（專案內部成員；寫入 FMS 存取稽核）</summary>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/content")]
    [Produces(MediaTypeNames.Application.Octet)]
    [Function("DownloadUatAttachment", "下載 UAT 驗收附件", Order = 8, Description = "下載附件原檔")]
    [RequiresPermission("PMS.UAT.READ", "檢視 UAT 驗收簽核單")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        var result = await attachments.OpenDownloadAsync(CurrentUserId, id, attachmentId, ct).ConfigureAwait(false);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);

        var download = result.Value!;
        Response.Headers.CacheControl = "private, no-store";
        ApplySecurityHeaders();
        Response.Headers["Repr-Digest"] = $"sha-256=:{Convert.ToBase64String(Convert.FromHexString(download.Metadata.Sha256))}:";
        return File(download.Content!, download.Metadata.ContentType, download.Metadata.OriginalFileName);
    }

    /// <summary>確認附件已遮罩個資／密碼／金鑰（SOP 8-4）；僅專案 PM／Tech Lead</summary>
    [HttpPut("{id:guid}/attachments/{attachmentId:guid}/mask-confirm")]
    [Function("ConfirmUatAttachmentMask", "確認 UAT 附件已遮罩", Order = 9, Description = "確認驗收附件已遮罩機敏資料")]
    [RequiresPermission("PMS.UAT.CREATE", "建立與處理 UAT 驗收簽核單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(UatAttachmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UatAttachmentDto>> ConfirmMask(Guid id, Guid attachmentId, CancellationToken ct = default) =>
        FromResult(await attachments.ConfirmMaskAsync(CurrentUserId, Actor, id, attachmentId, ct).ConfigureAwait(false));

    /// <summary>刪除附件（虛擬刪除，僅專案 PM／Tech Lead，且僅限待簽核）</summary>
    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [Function("DeleteUatAttachment", "刪除 UAT 驗收附件", Order = 10, Description = "虛擬刪除附件（驗收結果記錄後鎖定）")]
    [RequiresPermission("PMS.UAT.CREATE", "建立與處理 UAT 驗收簽核單", ActionCategory.Critical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken ct = default) =>
        NoContentFrom(await attachments.DeleteAsync(CurrentUserId, Actor, id, attachmentId, ct).ConfigureAwait(false));

    private static async Task<string> ReadFormFieldAsync(MultipartSection section, CancellationToken ct)
    {
        var buffer = new char[MaxFormFieldLength + 1];
        using var reader = new StreamReader(section.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
        if (read > MaxFormFieldLength) throw new InvalidDataException("表單欄位內容過長。");
        return new string(buffer, 0, read).Trim();
    }
}
