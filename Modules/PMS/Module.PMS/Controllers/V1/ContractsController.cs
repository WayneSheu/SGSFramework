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
public sealed class ContractsController(ContractService contracts, ContractAttachmentService attachments, TimeProvider timeProvider) : PmsControllerBase
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

    private static async Task<string> ReadFormFieldAsync(MultipartSection section, CancellationToken ct)
    {
        var buffer = new char[MaxFormFieldLength + 1];
        using var reader = new StreamReader(section.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
        if (read > MaxFormFieldLength) throw new InvalidDataException("表單欄位內容過長。");
        return new string(buffer, 0, read).Trim();
    }
}
