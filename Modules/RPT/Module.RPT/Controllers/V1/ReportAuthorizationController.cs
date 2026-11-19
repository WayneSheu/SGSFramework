using System.Net.Mime;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Entities;

namespace SGS.Modules.RPT.Controllers.V1;

/// <summary>
/// 報告授權審核：報告類別授權、簽署人資格與敏感授權的雙重審批（Maker-Checker）。
/// 權限依職責分離：檢視 / 送出授權（Maker）/ 審核授權（Checker）。API 路徑沿用 report-categories 以維持相容。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/report-categories")]
[ControllerTitle("報告授權審核", Icon = "fa-solid fa-scale-balanced", Order = 4, Description = "報告類別授權、簽署人資格與授權變更審核")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ReportAuthorizationController(IReportCategoryManagementService managementService) : ControllerBase
{
    private readonly IReportCategoryManagementService _service = managementService ?? throw new ArgumentNullException(nameof(managementService));

    public sealed record ReasonRequest(string? Reason);
    public sealed record ReviewRequest(string? Comment);

    /// <summary>搜尋授權對象（使用者 / 角色；type 未指定時兩者皆回傳）</summary>
    [HttpGet("principals")]
    [Function("SearchReportPrincipals", "搜尋授權對象", Icon = "fa-solid fa-magnifying-glass", Order = 20, Description = "搜尋可授權的使用者與角色")]
    [RequiresPermission("RPT.REPORTAUTHORIZATION.READ", "檢視報告授權")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportPrincipalOptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Principals([FromQuery] string? keyword, [FromQuery] GrantPrincipalType? type = null, CancellationToken cancellationToken = default) =>
        Ok(await _service.SearchPrincipalsAsync(keyword, type, cancellationToken).ConfigureAwait(false));

    /// <summary>使用者於各所屬實驗室的報告類別有效權限（含授權來源與簽署資格）</summary>
    [HttpGet("users/{userId:guid}/access")]
    [Function("GetUserReportAccess", "查詢使用者報告權限", Icon = "fa-solid fa-user-shield", Order = 21, Description = "查詢使用者於各所屬實驗室的報告類別有效權限、授權來源與簽署資格")]
    [RequiresPermission("RPT.REPORTAUTHORIZATION.READ", "檢視報告授權")]
    [ProducesResponseType(typeof(UserReportAccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UserAccess(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetUserReportAccessAsync(userId, cancellationToken).ConfigureAwait(false);
        return result is null ? NotFound() : Ok(result);
    }

    // ===== 類別授權 =====

    /// <summary>查詢類別授權</summary>
    [HttpGet("grants")]
    [Function("GetReportCategoryGrants", "報告授權審核", Icon = "fa-solid fa-scale-balanced", Order = 1, IsMenu = true, Path = "/sgs.modules.rpt/reportauthorization", Description = "查詢使用者與角色的報告類別授權、簽署人資格與授權審核")]
    [RequiresPermission("RPT.REPORTAUTHORIZATION.READ", "檢視報告授權")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportCategoryGrantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Grants([FromQuery] string? categoryCode, [FromQuery] Guid? principalId,
        [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default) =>
        Ok(await _service.ListGrantsAsync(categoryCode, principalId, includeInactive, cancellationToken).ConfigureAwait(false));

    /// <summary>新增類別授權（敏感授權轉為待審核）</summary>
    [HttpPost("grants")]
    [Function("CreateReportCategoryGrant", "新增類別授權", Icon = "fa-solid fa-plus", Order = 5, Description = "新增報告類別授權；簽署、作廢、盲樣識別、全域或特檢授權須經審核")]
    [RequiresPermission("RPT.REPORTAUTHORIZATION.GRANT", "送出報告類別授權")]
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
    [RequiresPermission("RPT.REPORTAUTHORIZATION.GRANT", "送出報告類別授權")]
    public async Task<IActionResult> RevokeGrant([FromRoute] Guid id, [FromBody] ReasonRequest request, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _service.RevokeGrantAsync(id, request?.Reason, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("撤銷類別授權失敗", message);
    }

    // ===== 簽署人資格 =====

    /// <summary>查詢簽署人資格</summary>
    [HttpGet("qualifications")]
    [Function("GetSignerQualifications", "檢視簽署人資格", Icon = "fa-solid fa-signature", Order = 7, Description = "查詢報告簽署人資格與效期")]
    [RequiresPermission("RPT.REPORTAUTHORIZATION.READ", "檢視報告授權")]
    [ProducesResponseType(typeof(IReadOnlyList<SignerQualificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Qualifications([FromQuery] string? categoryCode, [FromQuery] Guid? userId,
        [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default) =>
        Ok(await _service.ListQualificationsAsync(categoryCode, userId, includeInactive, cancellationToken).ConfigureAwait(false));

    /// <summary>申請簽署人資格（一律須經審核）</summary>
    [HttpPost("qualifications")]
    [Function("CreateSignerQualification", "申請簽署人資格", Icon = "fa-solid fa-plus", Order = 8, Description = "送出簽署人資格申請，經審核後生效")]
    [RequiresPermission("RPT.REPORTAUTHORIZATION.GRANT", "送出報告類別授權")]
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
    [RequiresPermission("RPT.REPORTAUTHORIZATION.GRANT", "送出報告類別授權")]
    public async Task<IActionResult> RevokeQualification([FromRoute] Guid id, [FromBody] ReasonRequest request, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _service.RevokeQualificationAsync(id, request?.Reason, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("撤銷簽署人資格失敗", message);
    }

    // ===== 雙重審批 =====

    /// <summary>查詢授權變更申請（預設僅待審核）</summary>
    [HttpGet("change-requests")]
    [Function("GetReportGrantChangeRequests", "查詢授權申請", Icon = "fa-solid fa-clipboard-check", Order = 10, Description = "查詢待審核或歷史的報告授權申請")]
    [RequiresPermission("RPT.REPORTAUTHORIZATION.READ", "檢視報告授權")]
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
    [RequiresPermission("RPT.REPORTAUTHORIZATION.APPROVE", "審核報告授權申請")]
    public async Task<IActionResult> Approve([FromRoute] Guid id, [FromBody] ReviewRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _service.ApproveAsync(id, request?.Comment, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? Ok(result) : Problem400("核准失敗", result.Message);
    }

    /// <summary>駁回申請（須填寫原因）</summary>
    [HttpPost("change-requests/{id:guid}/reject")]
    [Function("RejectReportGrantChange", "駁回授權申請", Icon = "fa-solid fa-xmark", Order = 12, Description = "駁回報告授權或簽署人資格申請")]
    [RequiresPermission("RPT.REPORTAUTHORIZATION.APPROVE", "審核報告授權申請")]
    public async Task<IActionResult> Reject([FromRoute] Guid id, [FromBody] ReviewRequest request, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _service.RejectAsync(id, request?.Comment, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("駁回失敗", message);
    }

    /// <summary>申請人撤回自己的申請</summary>
    [HttpPost("change-requests/{id:guid}/cancel")]
    [Function("CancelReportGrantChange", "撤回授權申請", Icon = "fa-solid fa-rotate-left", Order = 13, Description = "申請人撤回待審核的授權申請")]
    [RequiresPermission("RPT.REPORTAUTHORIZATION.GRANT", "送出報告類別授權")]
    public async Task<IActionResult> Cancel([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _service.CancelAsync(id, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("撤回失敗", message);
    }

    private BadRequestObjectResult Problem400(string title, string detail) =>
        BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = title, Detail = detail, Instance = HttpContext.Request.Path });
}
