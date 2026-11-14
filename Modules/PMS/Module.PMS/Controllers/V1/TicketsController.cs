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
/// 客戶問題單（SOP 6）：建單、PM 分流與 SLA、Tech Lead 指派（修復人≠驗證人）、修復回報、非作者驗證、PM 結案，
/// 以及附件（FMS 加密儲存、系統自動命名、待遮罩確認）。專案資料列授權由服務層套用（非成員一律 404）。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/pms/tickets")]
[ControllerTitle("問題單管理", Icon = "fa-solid fa-ticket", Order = 34, Description = "客戶問題單：受理、分流、SLA、修復與驗證、結案")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class TicketsController(
    TicketService tickets,
    TicketAttachmentService attachments,
    TimeProvider timeProvider) : PmsControllerBase
{
    /// <summary>附件 20MB（對應 FMS 分類 pms-issue）加表單欄位與 multipart 開銷。</summary>
    private const long MaxUploadBytes = 22L * 1024 * 1024;

    private const int MaxFormFieldLength = 512;

    public const string ContentSha256Header = "X-Content-SHA256";

    /// <summary>查詢問題單（含 SLA 燈號）</summary>
    [HttpPost("search")]
    [Function("SearchTickets", "問題單清單", Icon = "fa-solid fa-ticket", Order = 1, Description = "查詢客戶問題單與 SLA 燈號", IsMenu = true, Path = "/sgs.modules.pms/tickets")]
    [RequiresPermission("PMS.TICKET.READ", "檢視問題單")]
    [ProducesResponseType(typeof(PagedResult<TicketListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketListDto>>> Search([FromBody] TicketSearchRequest request, CancellationToken ct = default) =>
        Ok(await tickets.SearchAsync(CurrentUserId, request ?? new TicketSearchRequest(), ct).ConfigureAwait(false));

    /// <summary>建立問題單（取得單號 專案代號-流水號）</summary>
    [HttpPost]
    [Function("CreateTicket", "建立問題單", Order = 2, Description = "PM 收口建單；Stakeholder 亦可提出（僅見自己的單）")]
    [RequiresPermission("PMS.TICKET.CREATE", "建立問題單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketDetailDto>> Create([FromBody] CreateTicketRequest request, CancellationToken ct = default) =>
        FromResult(await tickets.CreateAsync(CurrentUserId, Actor, request, ct).ConfigureAwait(false),
            dto => Created($"/api/v1/pms/tickets/{dto.Id}", dto));

    /// <summary>取得問題單詳情（含事件時間軸、附件與可執行動作）</summary>
    [HttpGet("{id:guid}")]
    [Function("GetTicket", "問題單詳情", Order = 3, Description = "問題單詳情、SLA、事件時間軸與附件")]
    [RequiresPermission("PMS.TICKET.READ", "檢視問題單")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketDetailDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await tickets.GetAsync(CurrentUserId, id, ct).ConfigureAwait(false));

    /// <summary>分流（僅專案 PM）：諮詢／CR／系統異常（P1~P3）／資料撈取，並依 SLA 計算應解決期限</summary>
    [HttpPost("{id:guid}/triage")]
    [Function("TriageTicket", "問題單分流", Order = 4, Description = "PM 分流並計算 SLA 應解決期限")]
    [RequiresPermission("PMS.TICKET.TRIAGE", "分流問題單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketDetailDto>> Triage(Guid id, [FromBody] TriageTicketRequest request, CancellationToken ct = default) =>
        FromResult(await tickets.TriageAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>指派修復人與驗證人（僅專案 Tech Lead；修復人≠驗證人）</summary>
    [HttpPost("{id:guid}/assign")]
    [Function("AssignTicket", "問題單指派", Order = 5, Description = "Tech Lead 指派修復人與非作者驗證人")]
    [RequiresPermission("PMS.TICKET.TRIAGE", "分流問題單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketDetailDto>> Assign(Guid id, [FromBody] AssignTicketRequest request, CancellationToken ct = default) =>
        FromResult(await tickets.AssignAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>回報修復（僅被指派的修復人；須附重現測試）</summary>
    [HttpPost("{id:guid}/resolve")]
    [Function("ResolveTicket", "問題單修復回報", Order = 6, Description = "修復人回報修復，進入待驗證")]
    [RequiresPermission("PMS.TICKET.RESOLVE", "回報問題單修復", ActionCategory.Operational)]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketDetailDto>> Resolve(Guid id, [FromBody] ResolveTicketRequest request, CancellationToken ct = default) =>
        FromResult(await tickets.ResolveAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>驗證修復（僅被指派的驗證人，且不得為修復人）</summary>
    [HttpPost("{id:guid}/verify")]
    [Function("VerifyTicket", "問題單驗證", Order = 7, Description = "非作者驗證修復與回歸測試")]
    [RequiresPermission("PMS.TICKET.VERIFY", "驗證問題單修復", ActionCategory.Operational)]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketDetailDto>> Verify(Guid id, [FromBody] VerifyTicketRequest request, CancellationToken ct = default) =>
        FromResult(await tickets.VerifyAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>結案（僅專案 PM）：寫入實際解決時間並固化 SLA 結果</summary>
    [HttpPost("{id:guid}/close")]
    [Function("CloseTicket", "問題單結案", Order = 8, Description = "PM 確認客戶複測後結案")]
    [RequiresPermission("PMS.TICKET.TRIAGE", "分流問題單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketDetailDto>> Close(Guid id, [FromBody] CloseTicketRequest request, CancellationToken ct = default) =>
        FromResult(await tickets.CloseAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>重開（僅專案 PM；客戶複測未通過時退回處理中）</summary>
    [HttpPost("{id:guid}/reopen")]
    [Function("ReopenTicket", "問題單重開", Order = 9, Description = "客戶複測未通過，退回處理中")]
    [RequiresPermission("PMS.TICKET.TRIAGE", "分流問題單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketDetailDto>> Reopen(Guid id, [FromBody] ReopenTicketRequest request, CancellationToken ct = default) =>
        FromResult(await tickets.ReopenAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>取消（僅專案 PM；限受理或處理中）</summary>
    [HttpPost("{id:guid}/cancel")]
    [Function("CancelTicket", "問題單取消", Order = 10, Description = "取消受理或處理中的問題單")]
    [RequiresPermission("PMS.TICKET.TRIAGE", "分流問題單", ActionCategory.Critical)]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketDetailDto>> Cancel(Guid id, [FromBody] CancelTicketRequest request, CancellationToken ct = default) =>
        FromResult(await tickets.CancelAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    // ---------- 附件 ----------

    /// <summary>
    /// 上傳附件（multipart/form-data，串流不落地）：表單欄位 description（檔案說明，選填，預設取檔名）須位於檔案區段之前；
    /// 可附 X-Content-SHA256 標頭。檔名由系統產生（YYYYMMDD_[單號]_檔案說明.ext），上傳後為「待遮罩確認」。
    /// </summary>
    [HttpPost("{id:guid}/attachments")]
    [DisableFormValueModelBinding]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [Function("UploadTicketAttachment", "上傳問題單附件", Order = 11, Description = "上傳截圖或 Log 至 FMS（加密）並掛載於問題單")]
    [RequiresPermission("PMS.TICKET.CREATE", "建立問題單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(TicketAttachmentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketAttachmentDto>> UploadAttachment(
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
                    return FromResult(result, dto => Created($"/api/v1/pms/tickets/{id}", dto));
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
    [Function("ViewTicketAttachment", "檢視問題單附件（浮水印）", Order = 12, Description = "以動態浮水印檢視附件")]
    [RequiresPermission("PMS.TICKET.READ", "檢視問題單")]
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

    /// <summary>下載附件原檔（問題單可見者；寫入 FMS 存取稽核）</summary>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/content")]
    [Produces(MediaTypeNames.Application.Octet)]
    [Function("DownloadTicketAttachment", "下載問題單附件", Order = 13, Description = "下載附件原檔")]
    [RequiresPermission("PMS.TICKET.READ", "檢視問題單")]
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

    /// <summary>確認附件已遮罩個資／密碼／金鑰（SOP 8-4）</summary>
    [HttpPut("{id:guid}/attachments/{attachmentId:guid}/mask-confirm")]
    [Function("ConfirmTicketAttachmentMask", "確認附件已遮罩", Order = 14, Description = "確認截圖與 Log 已遮罩機敏資料")]
    [RequiresPermission("PMS.TICKET.CREATE", "建立問題單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(TicketAttachmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketAttachmentDto>> ConfirmMask(Guid id, Guid attachmentId, CancellationToken ct = default) =>
        FromResult(await attachments.ConfirmMaskAsync(CurrentUserId, Actor, id, attachmentId, ct).ConfigureAwait(false));

    /// <summary>刪除附件（虛擬刪除，僅專案 PM／Tech Lead）</summary>
    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [Function("DeleteTicketAttachment", "刪除問題單附件", Order = 15, Description = "虛擬刪除附件")]
    [RequiresPermission("PMS.TICKET.TRIAGE", "分流問題單", ActionCategory.Critical)]
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
