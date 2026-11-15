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
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Controllers.V1;

/// <summary>
/// 專案合約管理（乙方標準版）：合約主檔與條款、審約檢核表、PM→法務→高階主管核決、簽約登錄、附件一／二、綁定專案與附件。
/// 功能權限由 [RequiresPermission] 控管（READ 檢視／MANAGE 維護／REVIEW 審閱／APPROVE 核決）；誰能做什麼由合約成員角色在服務層判斷，
/// 非合約成員一律 404（客戶 Stakeholder 看不到任何合約）。
/// 與選單入口共用 PMS.CONTRACT.READ 的詳情、附件檢視與下載已登記 [Function]（讓權限總覽顯示功能名稱）；
/// 選單同步已加固：非選單功能不得認領選單頁面節點，因此不會再造成選單被隱藏。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/pms/contracts")]
[ControllerTitle("專案合約", Icon = "fa-solid fa-file-contract", Order = 43, Description = "合約審約、核決、簽約與履約管理")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ContractsController(
    ContractService contracts, ContractAttachmentService attachments, ContractFulfillmentService fulfillment, ContractWatchService watch,
    TimeProvider timeProvider) : PmsControllerBase
{
    /// <summary>附件 20MB（對應 FMS 分類 pms-contract）加表單欄位與 multipart 開銷。</summary>
    private const long MaxUploadBytes = 22L * 1024 * 1024;

    private const int MaxFormFieldLength = 512;

    public const string ContentSha256Header = "X-Content-SHA256";

    /// <summary>查詢我參與的合約</summary>
    [HttpPost("search")]
    [Function("SearchContracts", "專案合約", Icon = "fa-solid fa-file-contract", Order = 1, Description = "查詢我參與的合約與核決進度", IsMenu = true, Path = "/sgs.modules.pms/contracts")]
    [RequiresPermission("PMS.CONTRACT.READ", "檢視專案合約")]
    [ProducesResponseType(typeof(PagedResult<ContractListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ContractListDto>>> Search([FromBody] ContractSearchRequest request, CancellationToken ct = default) =>
        Ok(await contracts.SearchAsync(CurrentUserId, request ?? new ContractSearchRequest(), ct).ConfigureAwait(false));

    /// <summary>取得合約詳情（條款、審閱表、核決、附件一／二、專案、附件、事件）</summary>
    [HttpGet("{id:guid}")]
    [Function("GetContract", "合約詳情", Order = 3, Description = "合約條款、審閱、核決、附件與事件紀錄")]
    [RequiresPermission("PMS.CONTRACT.READ", "檢視專案合約")]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await contracts.GetAsync(CurrentUserId, id, ct).ConfigureAwait(false));

    /// <summary>建立合約（建立者自動成為業務）</summary>
    [HttpPost]
    [Function("CreateContract", "建立專案合約", Order = 2, Description = "建立合約並自動帶入審約檢核表、附件一與附件二預設內容")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ContractDto>> Create([FromBody] CreateContractRequest request, CancellationToken ct = default) =>
        FromResult(await contracts.CreateAsync(CurrentUserId, Actor, request, ct).ConfigureAwait(false),
            dto => Created($"/api/v1/pms/contracts/{dto.Id}", dto));

    /// <summary>更新合約條款（簽約前三階段；送審後鎖定）</summary>
    [HttpPut("{id:guid}")]
    [Function("UpdateContract", "更新專案合約條款", Order = 3, Description = "更新條款參數（簽約前）")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> Update(Guid id, [FromBody] UpdateContractRequest request, CancellationToken ct = default) =>
        FromResult(await contracts.UpdateAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>整組取代合約成員（業務、合約 PM、法務、核決主管）</summary>
    [HttpPut("{id:guid}/members")]
    [Function("SetContractMembers", "設定合約成員", Order = 4, Description = "指派業務、合約 PM、法務與核決主管")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> SetMembers(Guid id, [FromBody] SetContractMembersRequest request, CancellationToken ct = default) =>
        FromResult(await contracts.SetMembersAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>填寫審閱項目結果（合約 PM 或法務）</summary>
    [HttpPut("{id:guid}/review-items/{code}")]
    [Function("UpdateContractReviewItem", "填寫合約審閱項目", Order = 5, Description = "審約檢核表：甲方原條款、乙方立場與結果")]
    [RequiresPermission("PMS.CONTRACT.REVIEW", "審閱專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> UpdateReviewItem(Guid id, string code, [FromBody] UpdateReviewItemRequest request, CancellationToken ct = default) =>
        FromResult(await contracts.UpdateReviewItemAsync(CurrentUserId, Actor, id, code, request, ct).ConfigureAwait(false));

    /// <summary>手動切換狀態（審約／談判往返、已驗收、保固中、結案；流標、取消、終止須填原因）</summary>
    [HttpPost("{id:guid}/status")]
    [Function("ChangeContractStatus", "變更合約狀態", Order = 6, Description = "手動切換合約狀態")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Critical)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> ChangeStatus(Guid id, [FromBody] ChangeContractStatusRequest request, CancellationToken ct = default) =>
        FromResult(await contracts.ChangeStatusAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>送審：鎖定條款與審閱結果快照，進入 PM → 法務 → 高階主管核決</summary>
    [HttpPost("{id:guid}/submit")]
    [Function("SubmitContract", "合約送審", Order = 7, Description = "送審並鎖定條款與審閱結果")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Critical)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> Submit(Guid id, CancellationToken ct = default) =>
        FromResult(await contracts.SubmitAsync(CurrentUserId, Actor, id, ct).ConfigureAwait(false));

    /// <summary>撤回送審（業務／合約 PM；須填原因）：合約回到談判中，重新送審須重跑全部核決步驟</summary>
    [HttpPost("{id:guid}/withdraw")]
    [Function("WithdrawContract", "撤回合約送審", Order = 17, Description = "簽約前撤回送審，回到談判中")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Critical)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> Withdraw(Guid id, [FromBody] ContractDecisionRequest request, CancellationToken ct = default) =>
        FromResult(await contracts.WithdrawAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>PM 初審／法務審核：核准或退回</summary>
    [HttpPost("{id:guid}/review-decisions")]
    [Function("DecideContractReview", "合約初審與法務審核", Order = 8, Description = "PM 初審或法務審核：核准或退回")]
    [RequiresPermission("PMS.CONTRACT.REVIEW", "審閱專案合約", ActionCategory.Critical)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> ReviewDecision(Guid id, [FromBody] ContractReviewDecisionRequest request, CancellationToken ct = default) =>
        FromResult(await contracts.ReviewDecisionAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>高階主管核決：核准、退回修改或否決（否決＝合約取消）</summary>
    [HttpPost("{id:guid}/executive-decision")]
    [Function("DecideContractExecutive", "合約高階主管核決", Order = 9, Description = "高階主管核決：核准、退回或否決")]
    [RequiresPermission("PMS.CONTRACT.APPROVE", "核決專案合約", ActionCategory.Critical)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> ExecutiveDecision(Guid id, [FromBody] ContractDecisionRequest request, CancellationToken ct = default) =>
        FromResult(await contracts.ExecutiveDecisionAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>核決齊備後登錄簽約（簽約日、合約起日）；合約進入履約中，條款鎖定</summary>
    [HttpPost("{id:guid}/sign")]
    [Function("SignContract", "登錄合約簽約", Order = 10, Description = "核決齊備後登錄用印簽約")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Critical)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> Sign(Guid id, [FromBody] SignContractRequest request, CancellationToken ct = default) =>
        FromResult(await contracts.SignAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>整組取代附件一《交付項目清單》</summary>
    [HttpPut("{id:guid}/deliverable-list")]
    [Function("UpdateContractDeliverableList", "更新合約交付項目清單", Order = 11, Description = "附件一：正面表列交付項目")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> UpdateDeliverableList(Guid id, [FromBody] UpdateDeliverableListRequest request, CancellationToken ct = default) =>
        FromResult(await contracts.UpdateDeliverableListAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>整組取代附件二《SLA 異常分級與回應標準》</summary>
    [HttpPut("{id:guid}/sla-levels")]
    [Function("UpdateContractSlaLevels", "更新合約 SLA 分級", Order = 12, Description = "附件二：異常分級與回應標準")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> UpdateSlaLevels(Guid id, [FromBody] UpdateSlaLevelsRequest request, CancellationToken ct = default) =>
        FromResult(await contracts.UpdateSlaLevelsAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>綁定 PMS 專案（簽約後；一個專案至多屬於一份合約）</summary>
    [HttpPost("{id:guid}/projects")]
    [Function("BindContractProject", "綁定合約專案", Order = 13, Description = "將 PMS 專案綁定到合約")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> BindProject(Guid id, [FromBody] BindContractProjectRequest request, CancellationToken ct = default) =>
        FromResult(await contracts.BindProjectAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>解除專案綁定</summary>
    [HttpDelete("{id:guid}/projects/{projectId:guid}")]
    [Function("UnbindContractProject", "解除合約專案綁定", Order = 14, Description = "解除 PMS 專案與合約的綁定")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> UnbindProject(Guid id, Guid projectId, CancellationToken ct = default) =>
        FromResult(await contracts.UnbindProjectAsync(CurrentUserId, Actor, id, projectId, ct).ConfigureAwait(false));

    // ---------- 附件 ----------

    /// <summary>
    /// 上傳合約附件（multipart/form-data，串流不落地；一次一檔）：表單欄位 kind（Draft／Signed／Other）與 description（選填）須位於檔案區段之前；
    /// 可附 X-Content-SHA256 標頭。檔名由系統產生。僅業務／合約 PM。
    /// </summary>
    [HttpPost("{id:guid}/attachments")]
    [DisableFormValueModelBinding]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [Function("UploadContractAttachment", "上傳合約附件", Order = 15, Description = "上傳甲方草稿或用印正本至 FMS（加密）")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ContractAttachmentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ContractAttachmentDto>> UploadAttachment(
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
        var kind = ContractAttachmentKind.Other;
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
                        CurrentUserId, Actor, id, kind, description, section.Body, fileName ?? string.Empty,
                        string.IsNullOrWhiteSpace(contentSha256) ? null : contentSha256.Trim(), ct).ConfigureAwait(false);
                    return FromResult(result, dto => Created($"/api/v1/pms/contracts/{id}", dto));
                }

                if (!disposition.IsFormDisposition()) continue;

                var field = disposition.Name.Value;
                if (string.Equals(field, "description", StringComparison.OrdinalIgnoreCase))
                {
                    description = await ReadFormFieldAsync(section, ct).ConfigureAwait(false);
                }
                else if (string.Equals(field, "kind", StringComparison.OrdinalIgnoreCase))
                {
                    var raw = await ReadFormFieldAsync(section, ct).ConfigureAwait(false);
                    if (!Enum.TryParse(raw, ignoreCase: true, out kind) || !Enum.IsDefined(kind))
                        return ProblemFrom(PmsError.Validation("附件類型不正確。"));
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
    [Function("ViewContractAttachment", "檢視合約附件（浮水印）", Order = 18, Description = "以動態浮水印檢視合約附件")]
    [RequiresPermission("PMS.CONTRACT.READ", "檢視專案合約")]
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

    /// <summary>下載附件原檔（合約成員；寫入 FMS 存取稽核）</summary>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/content")]
    [Produces(MediaTypeNames.Application.Octet)]
    [Function("DownloadContractAttachment", "下載合約附件", Order = 19, Description = "下載合約附件原檔")]
    [RequiresPermission("PMS.CONTRACT.READ", "檢視專案合約")]
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

    /// <summary>刪除附件（虛擬刪除；用印正本鎖定不可刪除）</summary>
    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [Function("DeleteContractAttachment", "刪除合約附件", Order = 16, Description = "虛擬刪除附件（用印正本鎖定）")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Critical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken ct = default) =>
        NoContentFrom(await attachments.DeleteAsync(CurrentUserId, Actor, id, attachmentId, ct).ConfigureAwait(false));

    // ---------- 履約管理（第二期） ----------

    /// <summary>履約總覽：金額與交期、協力事項、遲延通知書、文件審查、驗收、合約變更與付款期程</summary>
    [HttpGet("{id:guid}/fulfillment")]
    [Function("GetContractFulfillment", "合約履約總覽", Order = 20, Description = "金額與交期、協力事項、審查、驗收、變更與付款")]
    [RequiresPermission("PMS.CONTRACT.READ", "檢視專案合約")]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> GetFulfillment(Guid id, CancellationToken ct = default) =>
        FromResult(await fulfillment.GetAsync(CurrentUserId, id, ct).ConfigureAwait(false));

    /// <summary>新增甲方協力事項（履約階段；業務／合約 PM）</summary>
    [HttpPost("{id:guid}/coop-items")]
    [Function("CreateContractCoopItem", "新增甲方協力事項", Order = 21, Description = "登錄甲方應提供的資料、環境或簽認及應完成日")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> CreateCoopItem(Guid id, [FromBody] CoopItemRequest request, CancellationToken ct = default) =>
        FromResult(await fulfillment.CreateCoopItemAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>更新協力事項（已寄通知書者不可改應完成日）</summary>
    [HttpPut("{id:guid}/coop-items/{itemId:guid}")]
    [Function("UpdateContractCoopItem", "更新甲方協力事項", Order = 22, Description = "更新協力事項內容與應完成日")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> UpdateCoopItem(Guid id, Guid itemId, [FromBody] CoopItemRequest request, CancellationToken ct = default) =>
        FromResult(await fulfillment.UpdateCoopItemAsync(CurrentUserId, Actor, id, itemId, request, ct).ConfigureAwait(false));

    /// <summary>記錄甲方已完成協力事項</summary>
    [HttpPost("{id:guid}/coop-items/{itemId:guid}/complete")]
    [Function("CompleteContractCoopItem", "完成甲方協力事項", Order = 23, Description = "記錄甲方實際完成日")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> CompleteCoopItem(Guid id, Guid itemId, [FromBody] CompleteCoopRequest request, CancellationToken ct = default) =>
        FromResult(await fulfillment.CompleteCoopItemAsync(CurrentUserId, Actor, id, itemId, request, ct).ConfigureAwait(false));

    /// <summary>免除協力事項（已寄通知書者不可免除）</summary>
    [HttpPost("{id:guid}/coop-items/{itemId:guid}/waive")]
    [Function("WaiveContractCoopItem", "免除甲方協力事項", Order = 24, Description = "甲方不需提供或乙方決定不需要")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> WaiveCoopItem(Guid id, Guid itemId, CancellationToken ct = default) =>
        FromResult(await fulfillment.WaiveCoopItemAsync(CurrentUserId, Actor, id, itemId, ct).ConfigureAwait(false));

    /// <summary>刪除協力事項（軟刪除；已寄通知書者不可刪除）</summary>
    [HttpDelete("{id:guid}/coop-items/{itemId:guid}")]
    [Function("DeleteContractCoopItem", "刪除甲方協力事項", Order = 25, Description = "軟刪除尚未寄出通知書的協力事項")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Critical)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> DeleteCoopItem(Guid id, Guid itemId, CancellationToken ct = default) =>
        FromResult(await fulfillment.DeleteCoopItemAsync(CurrentUserId, Actor, id, itemId, ct).ConfigureAwait(false));

    /// <summary>通知書預設主旨與內文（依目前遲延、順延與請甲方提供期限產生），供編輯器帶入</summary>
    [HttpPost("{id:guid}/coop-items/{itemId:guid}/notice-defaults")]
    [Function("GetContractNoticeDefaults", "取得展延通知書預設內容", Order = 39, Description = "依目前遲延與順延產生通知書預設主旨與內文")]
    [RequiresPermission("PMS.CONTRACT.READ", "檢視專案合約")]
    [ProducesResponseType(typeof(NoticeDefaultsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NoticeDefaultsDto>> GetNoticeDefaults(Guid id, Guid itemId, [FromBody] NoticePreviewRequest request, CancellationToken ct = default) =>
        FromResult(await fulfillment.GetNoticeDefaultsAsync(CurrentUserId, Actor, id, itemId, request ?? new NoticePreviewRequest(null, null), ct).ConfigureAwait(false));

    /// <summary>預覽《協力遲延與工期展延通知書》PDF（未寄出，標示預覽）；可帶編輯後的主旨與內文</summary>
    [HttpPost("{id:guid}/coop-items/{itemId:guid}/notice-preview")]
    [Produces(MediaTypeNames.Application.Pdf)]
    [Function("PreviewContractNotice", "預覽展延通知書", Order = 26, Description = "預覽協力遲延與工期展延通知書 PDF")]
    [RequiresPermission("PMS.CONTRACT.READ", "檢視專案合約")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewNotice(Guid id, Guid itemId, [FromBody] NoticePreviewRequest request, CancellationToken ct = default) =>
        ToPdf(await fulfillment.PreviewNoticeAsync(CurrentUserId, Actor, id, itemId, request ?? new NoticePreviewRequest(null, null), ct).ConfigureAwait(false));

    /// <summary>寄出展延通知書：Email 代寄（附正式 PDF，寄成功才登記展延）或僅記錄已自行寄出</summary>
    [HttpPost("{id:guid}/coop-items/{itemId:guid}/notices")]
    [Function("SendContractNotice", "寄出展延通知書", Order = 27, Description = "寄出或記錄協力遲延與工期展延通知書並登記工期展延")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Critical)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> SendNotice(Guid id, Guid itemId, [FromBody] SendNoticeRequest request, CancellationToken ct = default) =>
        FromResult(await fulfillment.SendNoticeAsync(CurrentUserId, Actor, id, itemId, request, ct).ConfigureAwait(false));

    /// <summary>下載已寄出的通知書 PDF（FMS 存檔的正式版）</summary>
    [HttpGet("{id:guid}/notices/{noticeId:guid}/pdf")]
    [Produces(MediaTypeNames.Application.Pdf)]
    [Function("DownloadContractNotice", "下載展延通知書", Order = 28, Description = "下載已寄出的展延通知書正式版")]
    [RequiresPermission("PMS.CONTRACT.READ", "檢視專案合約")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadNotice(Guid id, Guid noticeId, CancellationToken ct = default) =>
        ToPdf(await fulfillment.GetNoticePdfAsync(CurrentUserId, id, noticeId, ct).ConfigureAwait(false));

    /// <summary>送審文件給甲方（開始 7 個工作日回覆計時）</summary>
    [HttpPost("{id:guid}/review-rounds")]
    [Function("SubmitContractReviewRound", "文件送審計時", Order = 29, Description = "把需甲方簽認的文件送審並計算回覆期限")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> SubmitReviewRound(Guid id, [FromBody] SubmitReviewRoundRequest request, CancellationToken ct = default) =>
        FromResult(await fulfillment.SubmitReviewRoundAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>記錄甲方回覆：簽認通過或提出修改意見</summary>
    [HttpPost("{id:guid}/review-rounds/{roundId:guid}/respond")]
    [Function("RespondContractReviewRound", "記錄文件審查回覆", Order = 30, Description = "記錄甲方簽認通過或提出修改意見")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> RespondReviewRound(Guid id, Guid roundId, [FromBody] RespondReviewRoundRequest request, CancellationToken ct = default) =>
        FromResult(await fulfillment.RespondReviewRoundAsync(CurrentUserId, Actor, id, roundId, request, ct).ConfigureAwait(false));

    /// <summary>登錄交付（開始擬制驗收計時）；瑕疵修復後可重新交付</summary>
    [HttpPost("{id:guid}/acceptance/deliver")]
    [Function("DeliverContract", "登錄合約交付", Order = 31, Description = "登錄交付日並計算擬制驗收期限")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Critical)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> Deliver(Guid id, [FromBody] DeliverRequest request, CancellationToken ct = default) =>
        FromResult(await fulfillment.DeliverAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>記錄甲方於期限內提出的具體瑕疵清單</summary>
    [HttpPost("{id:guid}/acceptance/defects")]
    [Function("ReportContractDefects", "記錄驗收瑕疵清單", Order = 32, Description = "甲方於擬制驗收期限內提出具體瑕疵清單")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> ReportDefects(Guid id, [FromBody] ReportDefectsRequest request, CancellationToken ct = default) =>
        FromResult(await fulfillment.ReportDefectsAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>記錄驗收完成（雙方簽認／用印）：合約進入已驗收、保固起算、尾款放行</summary>
    [HttpPost("{id:guid}/acceptance/confirm")]
    [Function("ConfirmContractAcceptance", "記錄驗收完成", Order = 33, Description = "雙方簽認驗收完成，放行驗收後請款")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Critical)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> ConfirmAcceptance(Guid id, [FromBody] ConfirmAcceptanceRequest request, CancellationToken ct = default) =>
        FromResult(await fulfillment.ConfirmAcceptanceAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>手動登錄合約變更（追加金額、新交付日、保固延長）</summary>
    [HttpPost("{id:guid}/amendments")]
    [Function("CreateContractAmendment", "登錄合約變更", Order = 34, Description = "手動登錄合約變更並留痕")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Critical)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> CreateAmendment(Guid id, [FromBody] CreateAmendmentRequest request, CancellationToken ct = default) =>
        FromResult(await fulfillment.CreateAmendmentAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>刪除手動登錄的合約變更（CR 自動掛帳者不可刪除）</summary>
    [HttpDelete("{id:guid}/amendments/{amendmentId:guid}")]
    [Function("DeleteContractAmendment", "刪除合約變更", Order = 35, Description = "刪除手動登錄的合約變更")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Critical)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> DeleteAmendment(Guid id, Guid amendmentId, CancellationToken ct = default) =>
        FromResult(await fulfillment.DeleteAmendmentAsync(CurrentUserId, Actor, id, amendmentId, ct).ConfigureAwait(false));

    /// <summary>整組設定付款期程（簽約後；比例合計 100%；已請款後不可整組修改）</summary>
    [HttpPut("{id:guid}/payments")]
    [Function("SetContractPayments", "設定付款期程", Order = 36, Description = "設定付款期別、比例與觸發條件")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Operational)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> SetPayments(Guid id, [FromBody] SetPaymentsRequest request, CancellationToken ct = default) =>
        FromResult(await fulfillment.SetPaymentsAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>變更付款期別狀態：手動放行、登記請款（發票號碼）、登記收款</summary>
    [HttpPost("{id:guid}/payments/{paymentId:guid}/status")]
    [Function("MoveContractPayment", "變更付款期別狀態", Order = 37, Description = "放行、請款或收款登記")]
    [RequiresPermission("PMS.CONTRACT.MANAGE", "維護專案合約", ActionCategory.Critical)]
    [ProducesResponseType(typeof(FulfillmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FulfillmentDto>> MovePayment(Guid id, Guid paymentId, [FromBody] MovePaymentRequest request, CancellationToken ct = default) =>
        FromResult(await fulfillment.MovePaymentAsync(CurrentUserId, Actor, id, paymentId, request, ct).ConfigureAwait(false));

    /// <summary>
    /// 合約履約每日監看（排程以系統帳號 scope: pms.contract.watch 呼叫，亦可由具權限者手動執行）：協力遲延提醒、審查擬制通過、擬制驗收、付款放行與提醒。
    /// 同一事項每個台灣日最多提醒一次；非工作日不執行。
    /// </summary>
    [HttpPost("watch")]
    [Function("WatchContracts", "合約履約監看", Order = 38, Description = "掃描協力遲延、審查期限、擬制驗收與付款並提醒")]
    [RequiresSystemScope("pms.contract.watch", Description = "合約履約每日監看", AllowUsers = true)]
    [RequiresPermission("PMS.SETTINGS.MANAGE", "維護 PMS 設定", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ContractWatchResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractWatchResult>> Watch(CancellationToken ct = default) =>
        Ok(await watch.ScanAsync(ct).ConfigureAwait(false));

    private IActionResult ToPdf(PmsResult<NoticePdfFile> result)
    {
        if (!result.IsSuccess) return ProblemFrom(result.Error!);

        Response.Headers.CacheControl = "private, no-store";
        ApplySecurityHeaders();
        return File(result.Value!.Content, MediaTypeNames.Application.Pdf, result.Value.FileName);
    }

    private static async Task<string> ReadFormFieldAsync(MultipartSection section, CancellationToken ct)
    {
        var buffer = new char[MaxFormFieldLength + 1];
        using var reader = new StreamReader(section.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
        if (read > MaxFormFieldLength) throw new InvalidDataException("表單欄位內容過長。");
        return new string(buffer, 0, read).Trim();
    }
}
