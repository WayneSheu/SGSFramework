using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Attributes;

namespace SGSFramework.AuthTokenBucket.Controllers.V1;

/// <summary>
/// 稽核指派：稽核權限 = 稽核角色（全域權限）＋ 稽核指派（區域實驗室 × 起訖期間）。
/// 選單入口位於 組織管理 → 實驗室管理 的「稽核指派」頁簽。
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
