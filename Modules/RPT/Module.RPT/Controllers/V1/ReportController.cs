using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.RPT.Application.Dtos;
using SGS.Modules.RPT.Application.Services;
using SGS.Modules.RPT.Infrastructure.Domain;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;

namespace SGS.Modules.RPT.Controllers.V1;

/// <summary>
/// 檢驗報告：功能層權限由 [RequiresPermission] 控管，資料層（類別 × 實驗室 × 密級 × 狀態 × 指派 × 簽署資格）由報告服務判定。
/// 無瀏覽權限的報告一律回應 404，避免以 ID 探測報告存在（IDOR）。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/reports")]
[ControllerTitle("檢驗報告", Icon = "fa-solid fa-file-contract", Order = 1,Description = "檢驗報告建立、數據登錄、審核、簽署發行、作廢重發與下載")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ReportController(IReportService reportService) : ControllerBase
{
    private readonly IReportService _service = reportService ?? throw new ArgumentNullException(nameof(reportService));

    public sealed record ReasonRequest(string? Reason);
    public sealed record ReviewRequest(string? Comment);
    public sealed record RowVersionRequest(string? RowVersion);
    public sealed record TransitionBody(string? Reason, string? RowVersion);

    // ===== 查詢 =====

    /// <summary>查詢目前實驗室可見的報告</summary>
    [HttpGet]
    [Function("GetReports", "報告列表", Icon = "fa-solid fa-eye", Order = 1,IsMenu = true,Description = "查詢目前實驗室中您有權瀏覽的檢驗報告")]
    [RequiresPermission("RPT.REPORT.READ", "報告列表")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] ReportListBox box, [FromQuery] ReportLifecycleStatus? status,
        [FromQuery] string? categoryCode, [FromQuery] string? keyword, CancellationToken cancellationToken = default) =>
        Ok(await _service.ListAsync(new ReportListQuery(box, status, categoryCode, keyword), cancellationToken).ConfigureAwait(false));

    /// <summary>報告明細（盲樣依授權遮蔽，並記錄檢視）</summary>
    [HttpGet("{id:guid}")]
    [Function("GetReports", "報告明細", Icon = "fa-solid fa-eye", Order = 1, Description = "查詢報告明細（盲樣依授權遮蔽，並記錄檢視）")]
    [RequiresPermission("RPT.REPORT.READ", "報告明細")]
    [ProducesResponseType(typeof(ReportDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var detail = await _service.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return detail is null ? NotFound404() : Ok(detail);
    }

    /// <summary>狀態轉換軌跡</summary>
    [HttpGet("{id:guid}/history")]
    [Function("GetReportHistory", "檢視狀態轉換軌跡", Icon = "fa-solid fa-history", Order = 10, Description = "檢視報告的狀態轉換歷史")]
    [RequiresPermission("RPT.REPORT.READ", "檢視狀態轉換軌跡")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportStatusHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var history = await _service.GetHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        return history is null ? NotFound404() : Ok(history);
    }

    /// <summary>存取紀錄（檢視、下載、列印、查驗）</summary>
    [HttpGet("{id:guid}/access-logs")]
    [Function("GetReportAccessLogs", "檢視存取紀錄", Icon = "fa-solid fa-clock-rotate-left", Order = 20, Description = "檢視報告之檢視、下載、列印與查驗紀錄（品保 / 實驗室主管）")]
    [RequiresPermission("RPT.AUDIT.READ", "檢視報告稽核軌跡")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportAccessLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AccessLogs([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var (result, logs) = await _service.GetAccessLogsAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(result, () => Ok(logs));
    }

    // ===== 建立與編輯 =====

    /// <summary>建立報告草稿（目前實驗室）</summary>
    [HttpPost]
    [Function("CreateReport", "建立報告", Icon = "fa-solid fa-plus", Order = 2, Description = "於目前實驗室建立檢驗報告草稿")]
    [RequiresPermission("RPT.REPORT.CREATE", "建立報告")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ToResult(await _service.CreateAsync(request, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>更新報告基本資料（草稿）</summary>
    [HttpPut("{id:guid}")]
    [Function("EditReport", "編輯報告", Icon = "fa-solid fa-pen", Order = 3, Description = "草稿階段編輯報告基本資料與檢驗結果")]
    [RequiresPermission("RPT.REPORT.EDIT", "編輯報告")]
    public async Task<IActionResult> UpdateHeader([FromRoute] Guid id, [FromBody] UpdateReportHeaderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ToResult(await _service.UpdateHeaderAsync(id, request, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>儲存檢驗結果（草稿；儀器數據人工修改須填原因）</summary>
    [HttpPut("{id:guid}/results")]
    [Function("SaveReportResults", "儲存檢驗結果", Icon = "fa-solid fa-flask-vial", Order = 3, Description = "草稿階段儲存檢驗結果（儀器數據人工修改須填原因）")]
    [RequiresPermission("RPT.REPORT.EDIT", "編輯報告")]
    public async Task<IActionResult> SaveResults([FromRoute] Guid id, [FromBody] SaveReportResultsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ToResult(await _service.SaveResultsAsync(id, request, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>設定報告指派（分析員、審核者、簽署人、專案成員、負責業務）</summary>
    [HttpPut("{id:guid}/assignments")]
    [Function("AssignReport", "指派報告", Icon = "fa-solid fa-user-tag", Order = 4, Description = "指派報告之經手人員")]
    [RequiresPermission("RPT.REPORT.ASSIGN", "指派報告")]
    public async Task<IActionResult> SetAssignments([FromRoute] Guid id, [FromBody] SetReportAssignmentsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ToResult(await _service.SetAssignmentsAsync(id, request, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>目前實驗室中可建立報告的類別</summary>
    [HttpGet("creatable-categories")]
    [Function("GetCreatableReportCategories", "建立報告類別", Icon = "fa-solid fa-list", Order = 3, IsMenu =true,Description = "查詢目前實驗室中您有權建立的檢驗報告類別")]
    [RequiresPermission("RPT.REPORT.CREATE", "建立報告類別")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportCategoryOptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatableCategories(CancellationToken cancellationToken = default) =>
        Ok(await _service.GetCreatableCategoriesAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>搜尋可指派的使用者</summary>
    [HttpGet("assignable-users")]
    [Function("GetReports", "可指派使用者", Icon = "fa-solid fa-eye", Order = 1, IsMenu = true, Description = "查詢目前實驗室中您有權指派的使用者清單")]
    [RequiresPermission("RPT.REPORT.ASSIGN", "指派報告")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportUserOptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignableUsers([FromQuery] string? keyword, CancellationToken cancellationToken = default) =>
        Ok(await _service.SearchAssignableUsersAsync(keyword, cancellationToken).ConfigureAwait(false));

    // ===== 生命週期 =====

    /// <summary>送審</summary>
    [HttpPost("{id:guid}/submit")]
    [Function("SubmitReport", "送審", Icon = "fa-solid fa-paper-plane", Order = 5, Description = "草稿送交審核，數據即鎖定")]
    [RequiresPermission("RPT.REPORT.SUBMIT", "送審報告")]
    public Task<IActionResult> Submit([FromRoute] Guid id, [FromBody] TransitionBody body, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, ReportTransition.Submit, body, cancellationToken);

    /// <summary>審核核可（作者不得審核自己的報告）</summary>
    [HttpPost("{id:guid}/review/approve")]
    [Function("ApproveReportReview", "審核報告", Icon = "fa-solid fa-check-double", Order = 6, Description = "數據覆核：核可或退回")]
    [RequiresPermission("RPT.REPORT.REVIEW", "審核報告")]
    public Task<IActionResult> ApproveReview([FromRoute] Guid id, [FromBody] TransitionBody body, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, ReportTransition.ApproveReview, body, cancellationToken);

    /// <summary>審核退回（須填原因）</summary>
    [HttpPost("{id:guid}/review/reject")]
    [Function("RejectReportReview", "退回報告", Icon = "fa-solid fa-xmark", Order = 6, Description = "數據覆核：核可或退回")]
    [RequiresPermission("RPT.REPORT.REVIEW", "審核報告")]
    public Task<IActionResult> RejectReview([FromRoute] Guid id, [FromBody] TransitionBody body, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, ReportTransition.RejectReview, body, cancellationToken);

    /// <summary>簽署發行（需有效簽署人資格；作者與審核者不得簽署）</summary>
    [HttpPost("{id:guid}/sign")]
    [Function("SignReport", "簽署發行", Icon = "fa-solid fa-signature", Order = 7, Description = "報告簽署人簽署並產出正式 PDF")]
    [RequiresPermission("RPT.REPORT.SIGN", "簽署報告")]
    public Task<IActionResult> Sign([FromRoute] Guid id, [FromBody] TransitionBody body, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, ReportTransition.Sign, body, cancellationToken);

    /// <summary>簽署人退回（須填原因）</summary>
    [HttpPost("{id:guid}/sign/reject")]
    [Function("RejectReportSign", "退回報告", Icon = "fa-solid fa-xmark", Order = 7, Description = "報告簽署人退回報告，需填寫原因")]
    [RequiresPermission("RPT.REPORT.SIGN", "簽署報告")]
    public Task<IActionResult> RejectSign([FromRoute] Guid id, [FromBody] TransitionBody body, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, ReportTransition.RejectSign, body, cancellationToken);

    // ===== 作廢與重發 =====

    /// <summary>申請作廢（可選擇重發）</summary>
    [HttpPost("{id:guid}/void-requests")]
    [Function("RequestReportVoid", "申請作廢", Icon = "fa-solid fa-ban", Order = 8, Description = "申請作廢已發行報告，可選擇重發")]
    [RequiresPermission("RPT.REPORT.VOID_REQUEST", "申請作廢報告")]
    public async Task<IActionResult> RequestVoid([FromRoute] Guid id, [FromBody] RequestReportVoidRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ToResult(await _service.RequestVoidAsync(id, request, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>查詢作廢申請（預設僅待審核）</summary>
    [HttpGet("void-requests")]
    [Function("GetReportVoidRequests", "查詢作廢申請", Icon = "fa-solid fa-ban", Order = 9, Description = "查詢報告作廢（與重發）申請")]
    [RequiresPermission("RPT.REPORT.READ", "檢視報告")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportVoidRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VoidRequests([FromQuery] bool all = false, CancellationToken cancellationToken = default) =>
        Ok(await _service.ListVoidRequestsAsync(!all, cancellationToken).ConfigureAwait(false));

    /// <summary>核准作廢（申請人不得核准）</summary>
    [HttpPost("void-requests/{requestId:guid}/approve")]
    [Function("ApproveReportVoid", "核准作廢", Icon = "fa-solid fa-check", Order = 9, Description = "核准報告作廢（與重發）申請")]
    [RequiresPermission("RPT.REPORT.VOID_APPROVE", "核准作廢報告")]
    public async Task<IActionResult> ApproveVoid([FromRoute] Guid requestId, [FromBody] ReviewRequest request, CancellationToken cancellationToken = default) =>
        ToResult(await _service.ApproveVoidAsync(requestId, request?.Comment, cancellationToken).ConfigureAwait(false));

    /// <summary>駁回作廢（須填原因）</summary>
    [HttpPost("void-requests/{requestId:guid}/reject")]
    [Function("RejectReportVoid", "駁回作廢", Icon = "fa-solid fa-xmark", Order = 9, Description = "駁回報告作廢（與重發）申請")]
    [RequiresPermission("RPT.REPORT.VOID_APPROVE", "核准作廢報告")]
    public async Task<IActionResult> RejectVoid([FromRoute] Guid requestId, [FromBody] ReviewRequest request, CancellationToken cancellationToken = default) =>
        ToResult(await _service.RejectVoidAsync(requestId, request?.Comment, cancellationToken).ConfigureAwait(false));

    // ===== 檔案 =====

    /// <summary>下載正式報告 PDF（雜湊完整性檢核並記錄）</summary>
    [HttpGet("{id:guid}/pdf")]
    [Function("DownloadReport", "下載報告", Icon = "fa-solid fa-file-pdf", Order = 10, Description = "下載已發行之正式報告 PDF")]
    [RequiresPermission("RPT.REPORT.DOWNLOAD", "下載報告")]
    [Produces("application/pdf", "application/json")]
    public async Task<IActionResult> Download([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var (result, file) = await _service.DownloadAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(result, () => File(file!.Content, "application/pdf", file.FileName));
    }

    /// <summary>預覽 PDF（加註檢視者浮水印，不具效力）</summary>
    [HttpGet("{id:guid}/preview")]
    [Function("PreviewReport", "預覽報告", Icon = "fa-solid fa-file-pdf", Order = 11, Description = "預覽報告 PDF（加註檢視者浮水印，不具效力）")]
    [RequiresPermission("RPT.REPORT.READ", "檢視報告")]
    [Produces("application/pdf", "application/json")]
    public async Task<IActionResult> Preview([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var (result, file) = await _service.PreviewAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(result, () => File(file!.Content, "application/pdf", file.FileName));
    }

    private async Task<IActionResult> TransitionAsync(Guid id, ReportTransition transition, TransitionBody? body, CancellationToken cancellationToken) =>
        ToResult(await _service.TransitionAsync(id, new ReportTransitionRequest(transition, body?.Reason, body?.RowVersion), cancellationToken).ConfigureAwait(false));

    private IActionResult ToResult(ReportOpResult result, Func<IActionResult>? onSuccess = null)
    {
        if (result.NotFound) return NotFound404();
        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "操作失敗",
                Detail = result.Message,
                Instance = HttpContext.Request.Path
            });
        }

        return onSuccess?.Invoke() ?? Ok(new { Succeeded = true, result.Message, Id = result.EntityId });
    }

    private NotFoundObjectResult NotFound404() =>
        NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "資源不存在", Detail = "找不到指定的報告。", Instance = HttpContext.Request.Path });
}
