using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Controllers.V1;

/// <summary>
/// 需求變更單 CR（FORM-CR-01，SOP 第 4 章）：PM 建立、Tech Lead 影響評估、PM 決策（A 順延／B 範圍交換／C 駁回或暫緩）、
/// PM＋Tech Lead（＋客戶）簽核。功能權限由 [RequiresPermission] 控管；誰能做哪個動作由專案角色（RACI）在服務層判斷，
/// 專案資料列授權：非成員一律 404。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/pms/change-requests")]
[ControllerTitle("需求變更管理", Icon = "fa-solid fa-code-pull-request", Order = 36, Description = "需求變更單（CR）：評估、決策與簽核")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ChangeRequestsController(ChangeRequestService changeRequests, ChangeRequestWatchService watch) : PmsControllerBase
{
    /// <summary>查詢 CR（含影響評估期限燈號）</summary>
    [HttpPost("search")]
    [Function("SearchChangeRequests", "需求變更單", Icon = "fa-solid fa-code-pull-request", Order = 1, Description = "查詢需求變更單與評估期限", IsMenu = true, Path = "/sgs.modules.pms/change-requests")]
    [RequiresPermission("PMS.CR.READ", "檢視需求變更單")]
    [ProducesResponseType(typeof(PagedResult<CrListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CrListDto>>> Search([FromBody] CrSearchRequest request, CancellationToken ct = default) =>
        Ok(await changeRequests.SearchAsync(CurrentUserId, request ?? new CrSearchRequest(), ct).ConfigureAwait(false));

    /// <summary>建立 CR（僅專案 PM；問題單分流為「需求變更」時亦會自動建立）</summary>
    [HttpPost]
    [Function("CreateChangeRequest", "建立需求變更單", Order = 2, Description = "PM 建立客戶 CR 或內部 CR")]
    [RequiresPermission("PMS.CR.CREATE", "建立需求變更單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(CrDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CrDetailDto>> Create([FromBody] CreateCrRequest request, CancellationToken ct = default) =>
        FromResult(await changeRequests.CreateAsync(CurrentUserId, Actor, request, ct).ConfigureAwait(false),
            dto => Created($"/api/v1/pms/change-requests/{dto.Id}", dto));

    /// <summary>取得 CR 詳情（含評估、決策、簽核與事件時間軸）</summary>
    [HttpGet("{id:guid}")]
    [Function("GetChangeRequest", "需求變更單詳情", Order = 3, Description = "CR 詳情、評估、決策與簽核")]
    [RequiresPermission("PMS.CR.READ", "檢視需求變更單")]
    [ProducesResponseType(typeof(CrDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CrDetailDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await changeRequests.GetAsync(CurrentUserId, id, ct).ConfigureAwait(false));

    /// <summary>影響評估（僅專案 Tech Lead）：層級、API 契約、模組耦合、資安與工時</summary>
    [HttpPost("{id:guid}/evaluate")]
    [Function("EvaluateChangeRequest", "CR 影響評估", Order = 4, Description = "Tech Lead 填寫影響評估")]
    [RequiresPermission("PMS.CR.EVALUATE", "評估需求變更單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(CrDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CrDetailDto>> Evaluate(Guid id, [FromBody] EvaluateCrRequest request, CancellationToken ct = default) =>
        FromResult(await changeRequests.EvaluateAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>決策（僅專案 PM）：A 時程順延／B 範圍交換／C 駁回或暫緩；進入簽核時固化內容雜湊</summary>
    [HttpPost("{id:guid}/decide")]
    [Function("DecideChangeRequest", "CR 決策", Order = 5, Description = "PM 拍板決策方案")]
    [RequiresPermission("PMS.CR.APPROVE", "核准需求變更單", ActionCategory.Critical)]
    [ProducesResponseType(typeof(CrDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CrDetailDto>> Decide(Guid id, [FromBody] DecideCrRequest request, CancellationToken ct = default) =>
        FromResult(await changeRequests.DecideAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>簽核（專案 PM 或 Tech Lead）：須對目前內容雜湊簽核，簽核齊備即核准</summary>
    [HttpPost("{id:guid}/sign")]
    [Function("SignChangeRequest", "CR 簽核", Order = 6, Description = "PM、Tech Lead 簽核")]
    [RequiresPermission("PMS.CR.APPROVE", "核准需求變更單", ActionCategory.Critical)]
    [ProducesResponseType(typeof(CrDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CrDetailDto>> Sign(Guid id, [FromBody] SignCrRequest request, CancellationToken ct = default) =>
        FromResult(await changeRequests.SignAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>PM 代錄客戶簽核（例外路徑，僅客戶 CR；須附原因，稽核標示為代錄）</summary>
    [HttpPost("{id:guid}/customer-sign/proxy")]
    [Function("ProxyCustomerSignChangeRequest", "PM 代錄客戶簽核", Order = 7, Description = "PM 代錄客戶簽核並附原因")]
    [RequiresPermission("PMS.CR.APPROVE", "核准需求變更單", ActionCategory.Critical)]
    [ProducesResponseType(typeof(CrDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CrDetailDto>> ProxyCustomerSign(Guid id, [FromBody] ProxyCustomerSignRequest request, CancellationToken ct = default) =>
        FromResult(await changeRequests.ProxyCustomerSignAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>取消（僅專案 PM；限受理或待決策）</summary>
    [HttpPost("{id:guid}/cancel")]
    [Function("CancelChangeRequest", "CR 取消", Order = 8, Description = "取消尚未決策的 CR")]
    [RequiresPermission("PMS.CR.CREATE", "建立需求變更單", ActionCategory.Critical)]
    [ProducesResponseType(typeof(CrDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CrDetailDto>> Cancel(Guid id, [FromBody] CancelCrRequest request, CancellationToken ct = default) =>
        FromResult(await changeRequests.CancelAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>
    /// 掃描評估逾期的 CR 並寄提醒信給專案 Tech Lead／PM（排程以系統帳號 scope: pms.cr.scan 呼叫，亦可由具權限者手動執行）。
    /// 同一張 CR 每個台灣日最多提醒一次；非工作日不發。
    /// </summary>
    [HttpPost("scan-overdue")]
    [Function("ScanOverdueChangeRequests", "掃描 CR 評估逾期", Order = 10, Description = "掃描評估逾期的 CR 並寄提醒信")]
    [RequiresSystemScope("pms.cr.scan", Description = "掃描 CR 評估逾期並提醒", AllowUsers = true)]
    [RequiresPermission("PMS.SETTINGS.MANAGE", "維護 PMS 設定", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(CrEvalWatchResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<CrEvalWatchResult>> ScanOverdue(CancellationToken ct = default) =>
        Ok(await watch.ScanOverdueAsync(ct).ConfigureAwait(false));

    /// <summary>重啟暫緩的 CR（僅專案 PM）：回到受理並重新起算評估期限</summary>
    [HttpPost("{id:guid}/reopen")]
    [Function("ReopenChangeRequest", "CR 重啟", Order = 9, Description = "下期規劃時重啟暫緩的 CR")]
    [RequiresPermission("PMS.CR.CREATE", "建立需求變更單", ActionCategory.Operational)]
    [ProducesResponseType(typeof(CrDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CrDetailDto>> Reopen(Guid id, [FromBody] ReopenCrRequest request, CancellationToken ct = default) =>
        FromResult(await changeRequests.ReopenAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));
}
