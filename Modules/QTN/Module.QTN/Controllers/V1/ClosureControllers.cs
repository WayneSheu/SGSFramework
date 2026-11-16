using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.QTN.Application.Contracts;
using SGS.Modules.QTN.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.QTN.Controllers.V1;

/// <summary>報價儀表板：漏斗、燈號分佈、得標率與待辦。</summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/qtn/dashboard")]
[ControllerTitle("報價儀表板", Icon = "fa-solid fa-chart-pie", Order = 1, Description = "報價漏斗、燈號與得標率")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class QuoteDashboardController(QuoteDashboardService dashboard) : QtnControllerBase
{
    [HttpPost("search")]
    [Function("QtnDashboard", "報價儀表板", Icon = "fa-solid fa-chart-pie", Order = 1, Description = "報價漏斗、燈號與得標率", IsMenu = true, Path = "/sgs.modules.qtn/dashboard")]
    [RequiresPermission("QTN.DASHBOARD.READ", "檢視報價儀表板")]
    [ProducesResponseType(typeof(QuoteDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteDashboardDto>> Get([FromBody] DashboardRequest request, CancellationToken ct = default) =>
        Ok(await dashboard.GetAsync(await CallerAsync(ct).ConfigureAwait(false), request ?? new DashboardRequest(null), ct).ConfigureAwait(false));
}

/// <summary>CR 變更影響評估單（SOP 4.3 / 附錄 B）：3 個工作日內評估、衍生 vX.Y-CR 報價、客戶書面簽署後方得排程。</summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/qtn/change-assessments")]
[ControllerTitle("CR 變更評估", Icon = "fa-solid fa-code-compare", Order = 5, Description = "專案變更影響評估單與 CR 報價")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ChangeAssessmentsController(ChangeAssessmentService assessments) : QtnControllerBase
{
    [HttpPost("search")]
    [Function("SearchQtnChangeAssessments", "CR 變更評估", Icon = "fa-solid fa-code-compare", Order = 1, Description = "變更影響評估單", IsMenu = true, Path = "/sgs.modules.qtn/change-requests")]
    [RequiresPermission("QTN.CR.READ", "檢視 CR 變更評估")]
    [ProducesResponseType(typeof(PagedResult<ChangeAssessmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ChangeAssessmentDto>>> Search([FromBody] ChangeAssessmentSearchRequest request, CancellationToken ct = default) =>
        Ok(await assessments.SearchAsync(await CallerAsync(ct).ConfigureAwait(false), request ?? new ChangeAssessmentSearchRequest(), ct).ConfigureAwait(false));

    [HttpGet("{id:guid}")]
    [Function("GetQtnChangeAssessment", "評估單詳情", Order = 2, Description = "檢視變更影響評估單")]
    [RequiresPermission("QTN.CR.READ", "檢視 CR 變更評估")]
    [ProducesResponseType(typeof(ChangeAssessmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChangeAssessmentDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await assessments.GetAsync(await CallerAsync(ct).ConfigureAwait(false), id, ct).ConfigureAwait(false));

    [HttpPost]
    [Function("CreateQtnChangeAssessment", "建立評估單", Order = 3, Description = "登錄變更請求（期限 3 個工作日）")]
    [RequiresPermission("QTN.CR.MANAGE", "維護 CR 變更評估", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ChangeAssessmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChangeAssessmentDto>> Create([FromBody] CreateChangeAssessmentRequest request, CancellationToken ct = default) =>
        FromResult(await assessments.CreateAsync(await CallerAsync(ct).ConfigureAwait(false), request, ct).ConfigureAwait(false));

    [HttpPut("{id:guid}")]
    [Function("UpdateQtnChangeAssessment", "評估變更影響", Order = 4, Description = "量化新增、重工、回歸、管理工時與時程衝擊")]
    [RequiresPermission("QTN.CR.MANAGE", "維護 CR 變更評估", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ChangeAssessmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChangeAssessmentDto>> Update(Guid id, [FromBody] UpdateChangeAssessmentRequest request, CancellationToken ct = default) =>
        FromResult(await assessments.UpdateAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPost("{id:guid}/create-quote")]
    [Function("CreateQtnCrQuote", "衍生 CR 報價", Order = 5, Description = "以評估工時衍生 vX.Y-CR 報價版本")]
    [RequiresPermission("QTN.CR.MANAGE", "維護 CR 變更評估", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ChangeAssessmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChangeAssessmentDto>> CreateQuote(Guid id, [FromBody] CreateCrQuoteRequest request, CancellationToken ct = default) =>
        FromResult(await assessments.CreateQuoteAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPost("{id:guid}/customer-sign")]
    [Function("SignQtnChangeAssessment", "登錄客戶簽署", Order = 6, Description = "客戶書面簽署追加費用後方得排程")]
    [RequiresPermission("QTN.CR.MANAGE", "維護 CR 變更評估", ActionCategory.Critical)]
    [ProducesResponseType(typeof(ChangeAssessmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChangeAssessmentDto>> CustomerSign(Guid id, [FromBody] CustomerSignChangeRequest request, CancellationToken ct = default) =>
        FromResult(await assessments.CustomerSignAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPost("{id:guid}/cancel")]
    [Function("CancelQtnChangeAssessment", "取消評估單", Order = 7, Description = "取消變更請求")]
    [RequiresPermission("QTN.CR.MANAGE", "維護 CR 變更評估", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ChangeAssessmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChangeAssessmentDto>> Cancel(Guid id, CancellationToken ct = default) =>
        FromResult(await assessments.CancelAsync(await CallerAsync(ct).ConfigureAwait(false), id, ct).ConfigureAwait(false));

    [HttpPost("scan-overdue")]
    [Function("ScanQtnCrOverdue", "CR 評估逾期掃描", Order = 20, Description = "每日提醒逾 3 個工作日未完成的評估單")]
    [RequiresSystemScope("qtn.cr.scan", Description = "CR 變更評估逾期提醒", AllowUsers = true)]
    [RequiresPermission("QTN.SCHEDULE.RUN", "執行報價排程", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ScanResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScanResultDto>> ScanOverdue(CancellationToken ct = default) =>
        Ok(await assessments.ScanOverdueAsync(ct).ConfigureAwait(false));
}

/// <summary>結案復盤（SOP 第七章）：預估與實際工時對比、±15% 偏差復盤、標準工時庫校準。</summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/qtn/variance-reviews")]
[ControllerTitle("結案工時復盤", Icon = "fa-solid fa-scale-balanced", Order = 6, Description = "預估與實際工時對比、標準工時庫校準")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class VarianceReviewsController(VarianceReviewService reviews) : QtnControllerBase
{
    [HttpPost("search")]
    [Function("SearchQtnVarianceReviews", "結案工時復盤", Icon = "fa-solid fa-scale-balanced", Order = 1, Description = "預估與實際工時對比", IsMenu = true, Path = "/sgs.modules.qtn/variance-reviews")]
    [RequiresPermission("QTN.REVIEW.READ", "檢視結案復盤")]
    [ProducesResponseType(typeof(PagedResult<VarianceReviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<VarianceReviewDto>>> Search([FromBody] VarianceReviewSearchRequest request, CancellationToken ct = default) =>
        Ok(await reviews.SearchAsync(await CallerAsync(ct).ConfigureAwait(false), request ?? new VarianceReviewSearchRequest(), ct).ConfigureAwait(false));

    [HttpGet("{id:guid}")]
    [Function("GetQtnVarianceReview", "復盤詳情", Order = 2, Description = "檢視工時偏差報告")]
    [RequiresPermission("QTN.REVIEW.READ", "檢視結案復盤")]
    [ProducesResponseType(typeof(VarianceReviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VarianceReviewDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await reviews.GetAsync(await CallerAsync(ct).ConfigureAwait(false), id, ct).ConfigureAwait(false));

    [HttpPost]
    [Function("CreateQtnVarianceReview", "建立結案復盤", Order = 3, Description = "結案後 14 個工作日內產出對比報告")]
    [RequiresPermission("QTN.REVIEW.MANAGE", "維護結案復盤", ActionCategory.Operational)]
    [ProducesResponseType(typeof(VarianceReviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VarianceReviewDto>> Create([FromBody] CreateVarianceReviewRequest request, CancellationToken ct = default) =>
        FromResult(await reviews.CreateAsync(await CallerAsync(ct).ConfigureAwait(false), request, ct).ConfigureAwait(false));

    [HttpPut("{id:guid}")]
    [Function("UpdateQtnVarianceReview", "填寫實際工時", Order = 4, Description = "填寫實際人天、偏差原因與復盤結論")]
    [RequiresPermission("QTN.REVIEW.MANAGE", "維護結案復盤", ActionCategory.Operational)]
    [ProducesResponseType(typeof(VarianceReviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VarianceReviewDto>> Update(Guid id, [FromBody] UpdateVarianceReviewRequest request, CancellationToken ct = default) =>
        FromResult(await reviews.UpdateAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPost("{id:guid}/calibrate")]
    [Function("CalibrateQtnTemplates", "校準標準工時庫", Order = 5, Description = "以實際工時校準標準 WBS 模組（版本 +1）")]
    [RequiresPermission("QTN.TEMPLATE.MANAGE", "維護標準工時庫", ActionCategory.Operational)]
    [ProducesResponseType(typeof(VarianceReviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VarianceReviewDto>> Calibrate(Guid id, [FromBody] CalibrateRequest request, CancellationToken ct = default) =>
        FromResult(await reviews.CalibrateAsync(await CallerAsync(ct).ConfigureAwait(false), id, request, ct).ConfigureAwait(false));

    [HttpPost("scan-due")]
    [Function("ScanQtnVarianceDue", "復盤逾期掃描", Order = 20, Description = "每日提醒逾期的對比報告與校準")]
    [RequiresSystemScope("qtn.review.scan", Description = "結案復盤逾期提醒", AllowUsers = true)]
    [RequiresPermission("QTN.SCHEDULE.RUN", "執行報價排程", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ScanResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScanResultDto>> ScanDue(CancellationToken ct = default) =>
        Ok(await reviews.ScanDueAsync(ct).ConfigureAwait(false));
}
