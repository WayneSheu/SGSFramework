using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using SGS.Modules.QTN.Application.Contracts;
using SGS.Modules.QTN.Application.Services;
using SGS.Modules.QTN.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.QTN.Controllers.V1;

/// <summary>
/// 商機立案與收斂（SOP 階段一）：客戶、業務邊界、非功能性需求、計價模式與成員。非成員（且非審批人）一律 404。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/qtn/opportunities")]
[ControllerTitle("報價商機", Icon = "fa-solid fa-handshake", Order = 2, Description = "商機立案、成員與報價版本")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class OpportunitiesController(OpportunityService opportunities, QuoteService quotes) : QtnControllerBase
{
    [HttpPost("search")]
    [Function("SearchQtnOpportunities", "報價商機", Icon = "fa-solid fa-handshake", Order = 1, Description = "商機立案與報價", IsMenu = true, Path = "/sgs.modules.qtn/opportunities")]
    [RequiresPermission("QTN.OPPORTUNITY.READ", "檢視報價商機")]
    [ProducesResponseType(typeof(PagedResult<OpportunityListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OpportunityListDto>>> Search([FromBody] OpportunitySearchRequest request, CancellationToken ct = default) =>
        Ok(await opportunities.SearchAsync(await CallerAsync(ct).ConfigureAwait(false), request ?? new OpportunitySearchRequest(), ct).ConfigureAwait(false));

    [HttpGet("{id:guid}")]
    [Function("GetQtnOpportunity", "商機詳情", Order = 2, Description = "檢視商機與報價版本")]
    [RequiresPermission("QTN.OPPORTUNITY.READ", "檢視報價商機")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await opportunities.GetAsync(await CallerAsync(ct).ConfigureAwait(false), id, ct).ConfigureAwait(false));

    [HttpPost]
    [Function("CreateQtnOpportunity", "建立商機", Order = 3, Description = "建立商機（建立者為業務）")]
    [RequiresPermission("QTN.OPPORTUNITY.MANAGE", "維護報價商機", ActionCategory.Operational)]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityDto>> Create([FromBody] UpsertOpportunityRequest request, CancellationToken ct = default) =>
        FromResult(await opportunities.CreateAsync(await CallerAsync(ct).ConfigureAwait(false), request, ct).ConfigureAwait(false));

    [HttpPut("{id:guid}")]
    [Function("UpdateQtnOpportunity", "修改商機", Order = 4, Description = "修改商機資料")]
    [RequiresPermission("QTN.OPPORTUNITY.MANAGE", "維護報價商機", ActionCategory.Operational)]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityDto>> Update(Guid id, [FromBody] UpsertOpportunityRequest request, CancellationToken ct = default) =>
        FromResult(await opportunities.UpdateAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPut("{id:guid}/members")]
    [Function("SetQtnOpportunityMembers", "維護商機成員", Order = 5, Description = "指派業務、Tech Lead、PM、財務")]
    [RequiresPermission("QTN.OPPORTUNITY.MANAGE", "維護報價商機", ActionCategory.Operational)]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityDto>> SetMembers(Guid id, [FromBody] SetOpportunityMembersRequest request, CancellationToken ct = default) =>
        FromResult(await opportunities.SetMembersAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPost("{id:guid}/quotes")]
    [Function("CreateQtnQuote", "建立報價", Order = 6, Description = "建立第一版報價（可由標準模組匯入）")]
    [RequiresPermission(QuoteEdit, "編修報價", ActionCategory.Operational)]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteDto>> CreateQuote(Guid id, [FromBody] CreateQuoteRequest request, CancellationToken ct = default) =>
        FromResult(await quotes.CreateAsync(await CallerAsync(ct).ConfigureAwait(false), id, request ?? new CreateQuoteRequest(null, null), ct).ConfigureAwait(false));
}

/// <summary>
/// 報價版本：WBS 三點估時（QTN.ESTIMATE.EDIT）、定價與條款（QTN.QUOTE.EDIT）、技術複核、送審、多層級審批（QTN.QUOTE.APPROVE）、
/// 衍生版本、對外發出、成交、轉 PMS 合約、PDF 與附件。金額與成本依 QTN.PRICE.READ／QTN.COST.READ 於後端投影。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/qtn/quotes")]
[ControllerTitle("報價單", Icon = "fa-solid fa-file-invoice-dollar", Order = 3, Description = "報價版本、估時、定價與審批")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class QuotesController(
    QuoteService quotes, QuoteAttachmentService attachments, QuoteExportService exports, TimeProvider timeProvider) : QtnControllerBase
{
    /// <summary>附件 20MB（對應 FMS 分類 qtn-quotation）加 multipart 開銷。</summary>
    private const long MaxUploadBytes = 22L * 1024 * 1024;

    private const int MaxFormFieldLength = 64;
    private const string ContentSha256Header = "X-Content-SHA256";

    [HttpPost("search")]
    [Function("SearchQtnQuotes", "報價單", Icon = "fa-solid fa-file-invoice-dollar", Order = 1, Description = "報價版本清單", IsMenu = true, Path = "/sgs.modules.qtn/quotes")]
    [RequiresPermission("QTN.QUOTE.READ", "檢視報價單")]
    [ProducesResponseType(typeof(PagedResult<QuoteListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<QuoteListDto>>> Search([FromBody] QuoteSearchRequest request, CancellationToken ct = default) =>
        Ok(await quotes.SearchAsync(await CallerAsync(ct).ConfigureAwait(false), request ?? new QuoteSearchRequest(), ct).ConfigureAwait(false));

    /// <summary>待我審批（輪到我的關卡，已排除職責分離不可審者）。</summary>
    [HttpPost("approvals/search")]
    [Function("SearchQtnApprovals", "待我審批", Icon = "fa-solid fa-stamp", Order = 2, Description = "輪到我審批的報價", IsMenu = true, Path = "/sgs.modules.qtn/approvals")]
    [RequiresPermission("QTN.QUOTE.APPROVE", "審批報價", ActionCategory.Critical)]
    [ProducesResponseType(typeof(PagedResult<QuoteListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<QuoteListDto>>> Approvals([FromBody] QuoteSearchRequest request, CancellationToken ct = default)
    {
        var r = request ?? new QuoteSearchRequest();
        r = r with { Filter = (r.Filter ?? new QuoteFilter()) with { PendingMyApproval = true } };
        return Ok(await quotes.SearchAsync(await CallerAsync(ct).ConfigureAwait(false), r, ct).ConfigureAwait(false));
    }

    [HttpGet("{id:guid}")]
    [Function("GetQtnQuote", "報價詳情", Order = 3, Description = "檢視報價（依欄位級權限遮罩成本與金額）")]
    [RequiresPermission("QTN.QUOTE.READ", "檢視報價單")]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await quotes.GetAsync(await CallerAsync(ct).ConfigureAwait(false), id, ct).ConfigureAwait(false));

    /// <summary>儲存草稿（null 區段不變）。WBS 須 QTN.ESTIMATE.EDIT；金額／條款／里程碑須 QTN.QUOTE.EDIT 與 QTN.PRICE.READ。</summary>
    [HttpPut("{id:guid}")]
    [Function("EditQtnQuote", "編修報價草稿", Order = 4, Description = "編修 WBS、工時、折扣、費用、里程碑與條款")]
    [RequiresPermission("QTN.QUOTE.READ", "檢視報價單")]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteDto>> Edit(Guid id, [FromBody] QuoteEditRequest request, CancellationToken ct = default) =>
        FromResult(await quotes.EditAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, preview: false, ct).ConfigureAwait(false));

    /// <summary>即時試算：套用編修內容並回傳重算結果，不寫入。</summary>
    [HttpPost("{id:guid}/preview")]
    [Function("PreviewQtnQuote", "報價試算", Order = 5, Description = "不儲存，試算工時、金額與燈號")]
    [RequiresPermission("QTN.QUOTE.READ", "檢視報價單")]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteDto>> Preview(Guid id, [FromBody] QuoteEditRequest request, CancellationToken ct = default) =>
        FromResult(await quotes.EditAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, preview: true, ct).ConfigureAwait(false));

    [HttpPost("{id:guid}/import-template")]
    [Function("ImportQtnTemplate", "匯入標準模組", Order = 6, Description = "由標準 WBS 工時庫匯入模組")]
    [RequiresPermission(EstimateEdit, "編修工時估算", ActionCategory.Operational)]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteDto>> ImportTemplate(Guid id, [FromBody] ImportTemplateRequest request, CancellationToken ct = default) =>
        FromResult(await quotes.ImportTemplateAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPost("{id:guid}/tech-review")]
    [Function("TechReviewQtnQuote", "技術複核", Order = 7, Description = "Tech Lead 完成工時評估與技術可行性複核")]
    [RequiresPermission(EstimateEdit, "編修工時估算", ActionCategory.Operational)]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteDto>> TechReview(Guid id, [FromBody] TechReviewRequest request, CancellationToken ct = default) =>
        FromResult(await quotes.TechReviewAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPost("{id:guid}/submit")]
    [Function("SubmitQtnQuote", "送審報價", Order = 8, Description = "依毛利燈號產生審批路徑並鎖定版本")]
    [RequiresPermission(QuoteEdit, "編修報價", ActionCategory.Operational)]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteDto>> Submit(Guid id, [FromBody] SubmitQuoteRequest request, CancellationToken ct = default) =>
        FromResult(await quotes.SubmitAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPost("{id:guid}/decide")]
    [Function("DecideQtnQuote", "審批報價", Order = 9, Description = "核准或退件（職責分離、內容雜湊比對）")]
    [RequiresPermission("QTN.QUOTE.APPROVE", "審批報價", ActionCategory.Critical)]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteDto>> Decide(Guid id, [FromBody] DecideQuoteRequest request, CancellationToken ct = default) =>
        FromResult(await quotes.DecideAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPost("{id:guid}/revise")]
    [Function("ReviseQtnQuote", "衍生新版本", Order = 10, Description = "版本不可變：調整須衍生新版本")]
    [RequiresPermission("QTN.QUOTE.READ", "檢視報價單")]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteDto>> Revise(Guid id, [FromBody] ReviseQuoteRequest request, CancellationToken ct = default) =>
        FromResult(await quotes.ReviseAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPost("{id:guid}/issue")]
    [Function("IssueQtnQuote", "對外發出報價", Order = 11, Description = "登錄出具日並起算有效期")]
    [RequiresPermission(QuoteEdit, "編修報價", ActionCategory.Operational)]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteDto>> Issue(Guid id, [FromBody] IssueQuoteRequest request, CancellationToken ct = default) =>
        FromResult(await quotes.IssueAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPost("{id:guid}/outcome")]
    [Function("QtnQuoteOutcome", "登錄成交結果", Order = 12, Description = "成交（客戶簽約）或未得標")]
    [RequiresPermission(QuoteEdit, "編修報價", ActionCategory.Operational)]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteDto>> Outcome(Guid id, [FromBody] QuoteOutcomeRequest request, CancellationToken ct = default) =>
        FromResult(await quotes.OutcomeAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPost("{id:guid}/convert-to-contract")]
    [Function("ConvertQtnQuoteToContract", "建立 PMS 合約", Order = 13, Description = "已成交報價一鍵建立 PMS 合約與 WBS 執行基準")]
    [RequiresPermission(QuoteEdit, "編修報價", ActionCategory.Critical)]
    [ProducesResponseType(typeof(ConvertToContractResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConvertToContractResultDto>> ConvertToContract(Guid id, [FromBody] ConvertToContractRequest request, CancellationToken ct = default) =>
        FromResult(await quotes.ConvertToContractAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    /// <summary>匯出 PDF：customer＝對外報價單（需 QTN.PRICE.READ，不含成本）；estimate＝工時估算表；margin＝毛利率報告（需 QTN.COST.READ）。</summary>
    [HttpGet("{id:guid}/pdf/{kind}")]
    [Produces(MediaTypeNames.Application.Pdf)]
    [Function("ExportQtnQuotePdf", "匯出報價 PDF", Order = 14, Description = "對外報價單、工時估算表、毛利率報告")]
    [RequiresPermission("QTN.QUOTE.READ", "檢視報價單")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pdf(Guid id, string kind, CancellationToken ct = default)
    {
        var result = await exports.ExportAsync(await CallerAsync(ct).ConfigureAwait(false), id, kind, ct).ConfigureAwait(false);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);

        Response.Headers.CacheControl = "private, no-store";
        ApplySecurityHeaders();
        return File(result.Value!.Content, MediaTypeNames.Application.Pdf, result.Value.FileName);
    }

    /// <summary>排程：已發出且逾有效期者轉為逾期，並提醒 7 日內到期（SCH 以系統帳號 scope: qtn.quote.scan 每日呼叫）。</summary>
    [HttpPost("scan-expiry")]
    [Function("ScanQtnQuoteExpiry", "報價到期掃描", Order = 20, Description = "每日轉換逾期報價並提醒即將到期")]
    [RequiresSystemScope("qtn.quote.scan", Description = "報價有效期到期掃描", AllowUsers = true)]
    [RequiresPermission("QTN.SCHEDULE.RUN", "執行報價排程", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ScanResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScanResultDto>> ScanExpiry(CancellationToken ct = default) =>
        Ok(await quotes.ScanExpiryAsync(ct).ConfigureAwait(false));

    // ---------- 附件 ----------

    /// <summary>上傳附件（multipart/form-data 串流不落地；表單欄位 kind 須位於檔案之前；可附 X-Content-SHA256）。</summary>
    [HttpPost("{id:guid}/attachments")]
    [DisableFormValueModelBinding]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [Function("UploadQtnAttachment", "上傳報價附件", Order = 15, Description = "需求資料、折扣說明書、客戶簽回文件（FMS 加密）")]
    [RequiresPermission("QTN.QUOTE.READ", "檢視報價單")]
    [ProducesResponseType(typeof(QuoteAttachmentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<QuoteAttachmentDto>> UploadAttachment(Guid id, [FromHeader(Name = ContentSha256Header)] string? contentSha256, CancellationToken ct = default)
    {
        if (!MediaTypeHeaderValue.TryParse(Request.ContentType, out var mediaType) ||
            !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return ProblemFrom(new QtnError("QTN.FILE_REJECTED", 415, "Unsupported Media Type", "請以 multipart/form-data 上傳檔案。"));

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary)) return ProblemFrom(QtnError.Validation("multipart 邊界 (boundary) 不正確。"));

        var caller = await CallerAsync(ct).ConfigureAwait(false);
        var kind = QuoteAttachmentKind.Other;
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
                    var result = await attachments.UploadAsync(caller, id, kind, section.Body, fileName ?? string.Empty,
                        string.IsNullOrWhiteSpace(contentSha256) ? null : contentSha256.Trim(), ct).ConfigureAwait(false);
                    return FromResult(result, dto => Created($"/api/v1/qtn/quotes/{id}", dto));
                }

                if (disposition.IsFormDisposition() && string.Equals(disposition.Name.Value, "kind", StringComparison.OrdinalIgnoreCase))
                {
                    using var sr = new StreamReader(section.Body, Encoding.UTF8);
                    var buffer = new char[MaxFormFieldLength + 1];
                    var read = await sr.ReadBlockAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                    var raw = new string(buffer, 0, Math.Min(read, MaxFormFieldLength)).Trim();
                    if (!Enum.TryParse(raw, ignoreCase: true, out kind) || !Enum.IsDefined(kind)) return ProblemFrom(QtnError.Validation("附件類型不正確。"));
                }
            }

            return ProblemFrom(QtnError.Validation("請選擇要上傳的檔案。"));
        }
        catch (InvalidDataException ex)
        {
            return ProblemFrom(QtnError.Validation($"multipart 內容格式不正確：{ex.Message}"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(499);
        }
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/view")]
    [Produces(MediaTypeNames.Application.Pdf, "image/jpeg", "image/png")]
    [Function("ViewQtnAttachment", "檢視報價附件（浮水印）", Order = 16, Description = "以動態浮水印檢視報價附件")]
    [RequiresPermission("QTN.QUOTE.READ", "檢視報價單")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ViewAttachment(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        var result = await attachments.OpenViewAsync(await CallerAsync(ct).ConfigureAwait(false), id, attachmentId, BuildWatermarkText(timeProvider), ct).ConfigureAwait(false);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);

        Response.Headers.CacheControl = "no-store";
        ApplySecurityHeaders();
        Response.Headers["Content-Digest"] = $"sha-256=:{Convert.ToBase64String(SHA256.HashData(result.Value!.Content))}:";
        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/content")]
    [Produces(MediaTypeNames.Application.Octet)]
    [Function("DownloadQtnAttachment", "下載報價附件", Order = 17, Description = "下載報價附件原檔")]
    [RequiresPermission("QTN.QUOTE.READ", "檢視報價單")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        var result = await attachments.OpenDownloadAsync(await CallerAsync(ct).ConfigureAwait(false), id, attachmentId, ct).ConfigureAwait(false);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);

        var download = result.Value!;
        Response.Headers.CacheControl = "private, no-store";
        ApplySecurityHeaders();
        return File(download.Content!, download.Metadata.ContentType, download.Metadata.OriginalFileName);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [Function("DeleteQtnAttachment", "刪除報價附件", Order = 18, Description = "虛擬刪除附件（客戶簽回文件鎖定）")]
    [RequiresPermission("QTN.QUOTE.READ", "檢視報價單")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken ct = default) =>
        NoContentFrom(await attachments.DeleteAsync(await CallerAsync(ct).ConfigureAwait(false), id, attachmentId, ct).ConfigureAwait(false));
}
