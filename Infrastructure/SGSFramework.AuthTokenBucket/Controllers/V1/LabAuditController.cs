using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Attributes;

namespace SGSFramework.AuthTokenBucket.Controllers.V1;

/// <summary>
/// 稽核指派：稽核權限 = 稽核角色（全域權限）＋ 稽核指派（區域實驗室 × 起訖期間）。
/// 入口：獨立選單頁「稽核指派」（跨實驗室），以及 組織管理 → 實驗室管理 的「稽核指派」頁簽（單一實驗室）。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/lab-audits")]
[ControllerTitle("稽核指派", Icon = "fa-solid fa-user-secret", Order = 17, Description = "指派稽核員於期間內稽核區域實驗室")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class LabAuditController(ILabAuditService labAuditService) : ControllerBase
{
    private readonly ILabAuditService _service = labAuditService ?? throw new ArgumentNullException(nameof(labAuditService));

    public sealed record RevokeRequest(string? Reason);

    /// <summary>
    /// 稽核指派（選單入口）：獨立頁面可跨實驗室檢視與維護稽核指派。
    /// 此端點僅提供選單節點的歸屬與頁面路徑，不回傳業務資料。
    /// </summary>
    [HttpGet("entry")]
    [Function("LabAuditEntry", "稽核指派", Icon = "fa-solid fa-user-secret", Order = 0,
        Description = "跨實驗室檢視、新增與撤銷稽核指派", IsMenu = true, Path = "/sgsframework.org/labaudit")]
    [RequiresPermission("ORG.LABAUDIT.READ", "檢視稽核指派")]
    [EndpointSummary("稽核指派")]
    [EndpointDescription("選單入口，實際作業請使用 api/v1/lab-audits。")]
    public IActionResult Entry() => Ok(new { Path = "/sgsframework.org/labaudit" });

    /// <summary>查詢稽核指派（可依實驗室或使用者篩選）</summary>
    [HttpGet]
    [Function("GetLabAudits", "檢視稽核指派", Icon = "fa-solid fa-eye", Order = 1, Description = "查詢區域實驗室的稽核指派紀錄")]
    [RequiresPermission("ORG.LABAUDIT.READ", "檢視稽核指派")]
    [ProducesResponseType(typeof(IReadOnlyList<LabAuditAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? tenantLabId, [FromQuery] Guid? userId, CancellationToken cancellationToken = default) =>
        Ok(await _service.ListAsync(tenantLabId, userId, cancellationToken).ConfigureAwait(false));

    /// <summary>可指派稽核的使用者（持有稽核角色）</summary>
    [HttpGet("auditors")]
    [Function("GetAuditorCandidates", "稽核員清單", Icon = "fa-solid fa-users", Order = 2, Description = "列出持有稽核角色、可被指派稽核的使用者")]
    [RequiresPermission("ORG.LABAUDIT.READ", "檢視稽核指派")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditorCandidateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Auditors(CancellationToken cancellationToken = default) =>
        Ok(await _service.GetAuditorCandidatesAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>新增稽核指派</summary>
    [HttpPost]
    [Function("CreateLabAudit", "新增稽核指派", Icon = "fa-solid fa-plus", Order = 3, Description = "指派稽核員於起訖期間內稽核區域實驗室")]
    [RequiresPermission("ORG.LABAUDIT.MANAGE", "管理稽核指派")]
    [ProducesResponseType(typeof(LabAuditAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateLabAuditRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (ok, message, assignment) = await _service.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message, Assignment = assignment }) : Problem400("新增稽核指派失敗", message);
    }

    /// <summary>
    /// 批次新增稽核指派（多稽核員 × 多實驗室，各自起訖期間）。全有全無；dryRun=true 僅驗證並回傳逐筆錯誤與警告。
    /// 驗證不通過時回 400，內容仍為逐筆結果，供前端標示。
    /// </summary>
    [HttpPost("batch")]
    [Function("BatchCreateLabAudit", "批次新增稽核指派", Icon = "fa-solid fa-layer-group", Order = 7, Description = "一次指派多位稽核員於多間實驗室，各自設定起訖期間")]
    [RequiresPermission("ORG.LABAUDIT.MANAGE", "管理稽核指派")]
    [ProducesResponseType(typeof(BatchCreateLabAuditResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BatchCreateLabAuditResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BatchCreate([FromBody] BatchCreateLabAuditRequest request, [FromQuery] bool dryRun = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _service.BatchCreateAsync(request, dryRun, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    /// <summary>修改稽核指派（稽核員、實驗室、期間、原因；已撤銷或已到期者不可修改）</summary>
    [HttpPut("{id:guid}")]
    [Function("UpdateLabAudit", "修改稽核指派", Icon = "fa-solid fa-pen-to-square", Order = 5, Description = "修改稽核指派的稽核員、實驗室、期間與原因，並保留異動摘要")]
    [RequiresPermission("ORG.LABAUDIT.MANAGE", "管理稽核指派")]
    [ProducesResponseType(typeof(LabAuditAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateLabAuditRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (ok, message, assignment) = await _service.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message, Assignment = assignment }) : Problem400("修改稽核指派失敗", message);
    }

    /// <summary>稽核指派完整異動歷程（建立、修改、撤銷，由舊到新）</summary>
    [HttpGet("{id:guid}/history")]
    [Function("GetLabAuditHistory", "檢視稽核指派異動歷程", Icon = "fa-solid fa-clock-rotate-left", Order = 6, Description = "查詢單一稽核指派的完整異動歷程")]
    [RequiresPermission("ORG.LABAUDIT.READ", "檢視稽核指派")]
    [ProducesResponseType(typeof(IReadOnlyList<LabAuditHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var history = await _service.GetHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        return history is null ? NotFound() : Ok(history);
    }

    /// <summary>撤銷稽核指派（立即失效，保留紀錄）</summary>
    [HttpPost("{id:guid}/revoke")]
    [Function("RevokeLabAudit", "撤銷稽核指派", Icon = "fa-solid fa-ban", Order = 4, Description = "撤銷稽核指派，稽核權限立即失效")]
    [RequiresPermission("ORG.LABAUDIT.MANAGE", "管理稽核指派")]
    public async Task<IActionResult> Revoke([FromRoute] Guid id, [FromBody] RevokeRequest request, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _service.RevokeAsync(id, request?.Reason, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("撤銷稽核指派失敗", message);
    }

    private BadRequestObjectResult Problem400(string title, string detail) =>
        BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = title, Detail = detail });
}
