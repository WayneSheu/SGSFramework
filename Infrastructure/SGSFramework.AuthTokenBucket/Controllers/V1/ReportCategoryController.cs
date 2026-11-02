using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Entities;

namespace SGSFramework.AuthTokenBucket.Controllers.V1;

/// <summary>
/// 報告類別權限管理：類別主檔（檢驗領域）、承作實驗室、類別授權、簽署人資格與敏感授權雙重審批。
/// 權限依職責分離：檢視 / 維護類別 / 送出授權（Maker）/ 審核授權（Checker）。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/report-categories")]
[ControllerTitle("報告類別權限", Icon = "fa-solid fa-file-shield", Order = 18, Description = "報告類別（檢驗領域）、承作實驗室、類別授權與簽署人資格管理")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ReportCategoryController(IReportCategoryManagementService managementService) : ControllerBase
{
    private readonly IReportCategoryManagementService _service = managementService ?? throw new ArgumentNullException(nameof(managementService));

    public sealed record ReasonRequest(string? Reason);
    public sealed record ReviewRequest(string? Comment);

    // ===== 類別主檔 =====

    /// <summary>查詢報告類別（含承作實驗室）</summary>
    [HttpGet]
    [Function("GetReportCategories", "檢視報告類別", Icon = "fa-solid fa-eye", Order = 1, Description = "查詢報告類別主檔與承作實驗室")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.READ", "檢視報告類別權限")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default) =>
        Ok(await _service.ListCategoriesAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>新增或更新報告類別</summary>
    [HttpPost]
    [Function("SaveReportCategory", "維護報告類別", Icon = "fa-solid fa-pen", Order = 2, Description = "新增或更新報告類別主檔")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.MANAGE", "維護報告類別")]
    [ProducesResponseType(typeof(ReportCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Save([FromBody] SaveReportCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (ok, message, category) = await _service.SaveCategoryAsync(request, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(category) : Problem400("儲存報告類別失敗", message);
    }

    /// <summary>設定類別的承作實驗室（整批取代）</summary>
    [HttpPut("{code}/labs")]
    [Function("SetReportCategoryLabs", "設定承作實驗室", Icon = "fa-solid fa-building", Order = 3, Description = "設定可承作此報告類別的區域實驗室")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.MANAGE", "維護報告類別")]
    public async Task<IActionResult> SetLabs([FromRoute] string code, [FromBody] SetCategoryLabsRequest request, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _service.SetCategoryLabsAsync(code, request?.TenantLabIds ?? [], cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("設定承作實驗室失敗", message);
    }

    /// <summary>區域實驗室選項</summary>
    [HttpGet("lab-options")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.READ", "檢視報告類別權限")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportLabOptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LabOptions(CancellationToken cancellationToken = default) =>
        Ok(await _service.GetLabOptionsAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>搜尋授權對象（使用者與角色）</summary>
    [HttpGet("principals")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.READ", "檢視報告類別權限")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportPrincipalOptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Principals([FromQuery] string? keyword, CancellationToken cancellationToken = default) =>
        Ok(await _service.SearchPrincipalsAsync(keyword, cancellationToken).ConfigureAwait(false));

    // ===== 類別授權 =====

    /// <summary>查詢類別授權</summary>
    [HttpGet("grants")]
    [Function("GetReportCategoryGrants", "檢視類別授權", Icon = "fa-solid fa-key", Order = 4, Description = "查詢使用者與角色的報告類別授權")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.READ", "檢視報告類別權限")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportCategoryGrantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Grants([FromQuery] string? categoryCode, [FromQuery] Guid? principalId,
        [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default) =>
        Ok(await _service.ListGrantsAsync(categoryCode, principalId, includeInactive, cancellationToken).ConfigureAwait(false));

    /// <summary>新增類別授權（敏感授權轉為待審核）</summary>
    [HttpPost("grants")]
    [Function("CreateReportCategoryGrant", "新增類別授權", Icon = "fa-solid fa-plus", Order = 5, Description = "新增報告類別授權；簽署、作廢、盲樣識別、全域或特檢授權須經審核")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.GRANT", "送出報告類別授權")]
    [ProducesResponseType(typeof(ReportGrantSubmitResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateGrant([FromBody] CreateReportCategoryGrantRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _service.SubmitGrantAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? Ok(result) : Problem400("新增類別授權失敗", result.Message);
    }

    /// <summary>撤銷類別授權（立即生效，保留紀錄）</summary>
    [HttpPost("grants/{id:guid}/revoke")]
    [Function("RevokeReportCategoryGrant", "撤銷類別授權", Icon = "fa-solid fa-ban", Order = 6, Description = "撤銷報告類別授權，立即生效")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.GRANT", "送出報告類別授權")]
    public async Task<IActionResult> RevokeGrant([FromRoute] Guid id, [FromBody] ReasonRequest request, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _service.RevokeGrantAsync(id, request?.Reason, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("撤銷類別授權失敗", message);
    }

    // ===== 簽署人資格 =====

    /// <summary>查詢簽署人資格</summary>
    [HttpGet("qualifications")]
    [Function("GetSignerQualifications", "檢視簽署人資格", Icon = "fa-solid fa-signature", Order = 7, Description = "查詢報告簽署人資格與效期")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.READ", "檢視報告類別權限")]
    [ProducesResponseType(typeof(IReadOnlyList<SignerQualificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Qualifications([FromQuery] string? categoryCode, [FromQuery] Guid? userId,
        [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default) =>
        Ok(await _service.ListQualificationsAsync(categoryCode, userId, includeInactive, cancellationToken).ConfigureAwait(false));

    /// <summary>申請簽署人資格（一律須經審核）</summary>
    [HttpPost("qualifications")]
    [Function("CreateSignerQualification", "申請簽署人資格", Icon = "fa-solid fa-plus", Order = 8, Description = "送出簽署人資格申請，經審核後生效")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.GRANT", "送出報告類別授權")]
    [ProducesResponseType(typeof(ReportGrantSubmitResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateQualification([FromBody] CreateSignerQualificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _service.SubmitQualificationAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? Ok(result) : Problem400("申請簽署人資格失敗", result.Message);
    }

    /// <summary>撤銷簽署人資格（立即生效，保留紀錄）</summary>
    [HttpPost("qualifications/{id:guid}/revoke")]
    [Function("RevokeSignerQualification", "撤銷簽署人資格", Icon = "fa-solid fa-ban", Order = 9, Description = "撤銷簽署人資格，立即生效")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.GRANT", "送出報告類別授權")]
    public async Task<IActionResult> RevokeQualification([FromRoute] Guid id, [FromBody] ReasonRequest request, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _service.RevokeQualificationAsync(id, request?.Reason, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("撤銷簽署人資格失敗", message);
    }

    // ===== 雙重審批 =====

    /// <summary>查詢授權變更申請（預設僅待審核）</summary>
    [HttpGet("change-requests")]
    [Function("GetReportGrantChangeRequests", "查詢授權申請", Icon = "fa-solid fa-clipboard-check", Order = 10, Description = "查詢待審核或歷史的報告授權申請")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.READ", "檢視報告類別權限")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportGrantChangeRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeRequests([FromQuery] RoleChangeRequestStatus? status, [FromQuery] bool all = false,
        CancellationToken cancellationToken = default)
    {
        // 不可在參數上給 nullable enum 預設值：Microsoft.AspNetCore.OpenApi 產生文件時會 InvalidCastException
        RoleChangeRequestStatus? effectiveStatus = all ? null : status ?? RoleChangeRequestStatus.Pending;
        return Ok(await _service.ListChangeRequestsAsync(effectiveStatus, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>核准申請並套用（申請人與受授權者不得核准）</summary>
    [HttpPost("change-requests/{id:guid}/approve")]
    [Function("ApproveReportGrantChange", "核准授權申請", Icon = "fa-solid fa-check", Order = 11, Description = "核准報告授權或簽署人資格申請並套用")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.APPROVE", "審核報告授權申請")]
    public async Task<IActionResult> Approve([FromRoute] Guid id, [FromBody] ReviewRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _service.ApproveAsync(id, request?.Comment, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? Ok(result) : Problem400("核准失敗", result.Message);
    }

    /// <summary>駁回申請（須填寫原因）</summary>
    [HttpPost("change-requests/{id:guid}/reject")]
    [Function("RejectReportGrantChange", "駁回授權申請", Icon = "fa-solid fa-xmark", Order = 12, Description = "駁回報告授權或簽署人資格申請")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.APPROVE", "審核報告授權申請")]
    public async Task<IActionResult> Reject([FromRoute] Guid id, [FromBody] ReviewRequest request, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _service.RejectAsync(id, request?.Comment, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("駁回失敗", message);
    }

    /// <summary>申請人撤回自己的申請</summary>
    [HttpPost("change-requests/{id:guid}/cancel")]
    [Function("CancelReportGrantChange", "撤回授權申請", Icon = "fa-solid fa-rotate-left", Order = 13, Description = "申請人撤回待審核的授權申請")]
    [RequiresPermission("SYSTEM.REPORTCATEGORY.GRANT", "送出報告類別授權")]
    public async Task<IActionResult> Cancel([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _service.CancelAsync(id, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("撤回失敗", message);
    }

    private BadRequestObjectResult Problem400(string title, string detail) =>
        BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = title, Detail = detail, Instance = HttpContext.Request.Path });
}
