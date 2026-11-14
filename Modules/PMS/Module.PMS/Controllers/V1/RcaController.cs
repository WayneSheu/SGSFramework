using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;

namespace SGS.Modules.PMS.Controllers.V1;

/// <summary>
/// P1 重大異常分析報告 RCA（FORM-RCA-01，SOP 7.3）：Tech Lead 撰寫並於系統恢復後 48 小時內提交，PM 潤飾後寄送客戶。
/// 功能權限由 [RequiresPermission] 控管；誰能做哪個動作由專案角色（RACI）在服務層判斷，非專案內部成員一律 404。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/pms/rca")]
[ControllerTitle("RCA 根因分析", Icon = "fa-solid fa-magnifying-glass-chart", Order = 38, Description = "P1 重大異常分析報告")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class RcaController(RcaService rca) : PmsControllerBase
{
    /// <summary>取得問題單的 RCA（尚未建立時 exists=false，並回傳可選的觸發條件）</summary>
    [HttpGet("{ticketId:guid}")]
    [Function("GetRca", "RCA 詳情", Order = 1, Description = "問題單的 RCA 與可用觸發條件")]
    [RequiresPermission("PMS.TICKET.READ", "檢視問題單")]
    [ProducesResponseType(typeof(RcaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RcaDto>> Get(Guid ticketId, CancellationToken ct = default) =>
        FromResult(await rca.GetAsync(CurrentUserId, ticketId, ct).ConfigureAwait(false));

    /// <summary>建立或更新 RCA 草稿（提交期限＝系統恢復後 48 小時）</summary>
    [HttpPut("{ticketId:guid}")]
    [Function("SaveRca", "撰寫 RCA", Order = 2, Description = "Tech Lead 撰寫、PM 潤飾")]
    [RequiresPermission("PMS.RCA.WRITE", "撰寫 RCA", ActionCategory.Operational)]
    [ProducesResponseType(typeof(RcaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RcaDto>> Save(Guid ticketId, [FromBody] UpsertRcaRequest request, CancellationToken ct = default) =>
        FromResult(await rca.UpsertAsync(CurrentUserId, Actor, ticketId, request, ct).ConfigureAwait(false));

    /// <summary>提交 RCA（僅 Tech Lead；內容須完整）</summary>
    [HttpPost("{ticketId:guid}/submit")]
    [Function("SubmitRca", "提交 RCA", Order = 3, Description = "Tech Lead 提交 RCA")]
    [RequiresPermission("PMS.RCA.WRITE", "撰寫 RCA", ActionCategory.Operational)]
    [ProducesResponseType(typeof(RcaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RcaDto>> Submit(Guid ticketId, [FromBody] SubmitRcaRequest request, CancellationToken ct = default) =>
        FromResult(await rca.SubmitAsync(CurrentUserId, Actor, ticketId, request, ct).ConfigureAwait(false));

    /// <summary>
    /// RCA PDF 預覽／下載（專案內部成員）：已寄送者回傳當時存檔的正式版；未寄送者即時產生並標示「內部預覽」。
    /// </summary>
    [HttpGet("{ticketId:guid}/pdf")]
    [Produces(MediaTypeNames.Application.Pdf)]
    [Function("DownloadRcaPdf", "RCA PDF", Order = 5, Description = "預覽或下載 RCA PDF")]
    [RequiresPermission("PMS.TICKET.READ", "檢視問題單")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pdf(Guid ticketId, CancellationToken ct = default)
    {
        var result = await rca.GetPdfAsync(CurrentUserId, ticketId, ct).ConfigureAwait(false);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);

        Response.Headers.CacheControl = "private, no-store";
        ApplySecurityHeaders();
        return File(result.Value!.Content, MediaTypeNames.Application.Pdf, result.Value.FileName);
    }

    /// <summary>
    /// 寄送客戶（僅 PM；寄送後內容鎖定）：mode=Email 由系統產生正式版 PDF 並以附件寄給指定收件人，成功後才標記已寄送；
    /// mode=RecordOnly（預設）只記錄 PM 已自行寄出。
    /// </summary>
    [HttpPost("{ticketId:guid}/send")]
    [Function("SendRca", "寄送 RCA", Order = 4, Description = "PM 潤飾後寄送客戶")]
    [RequiresPermission("PMS.RCA.WRITE", "撰寫 RCA", ActionCategory.Critical)]
    [ProducesResponseType(typeof(RcaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RcaDto>> Send(Guid ticketId, [FromBody] SendRcaRequest request, CancellationToken ct = default) =>
        FromResult(await rca.SendAsync(CurrentUserId, Actor, ticketId, request, ct).ConfigureAwait(false));
}
