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
/// 專案風險管理表（SOP 3.1）：PM 風險追蹤（Tech Lead 協助），風險負責人更新自己負責風險的狀態與因應措施。
/// 功能權限由 [RequiresPermission] 控管；誰能做哪個動作由專案角色（RACI）在服務層判斷。僅內部成員可見，非成員一律 404。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/pms/risks")]
[ControllerTitle("風險管理", Icon = "fa-solid fa-triangle-exclamation", Order = 41, Description = "專案風險管理表")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class RisksController(RiskService risks) : PmsControllerBase
{
    /// <summary>查詢風險（未結案與高等級優先；不含已作廢）</summary>
    [HttpPost("search")]
    [Function("SearchRisks", "風險表", Icon = "fa-solid fa-triangle-exclamation", Order = 1, Description = "查詢專案風險管理表", IsMenu = true, Path = "/sgs.modules.pms/risks")]
    [RequiresPermission("PMS.RISK.READ", "檢視專案風險")]
    [ProducesResponseType(typeof(PagedResult<RiskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RiskDto>>> Search([FromBody] RiskSearchRequest request, CancellationToken ct = default) =>
        Ok(await risks.SearchAsync(CurrentUserId, request ?? new RiskSearchRequest(), ct).ConfigureAwait(false));

    /// <summary>建立風險（僅專案 PM／Tech Lead）</summary>
    [HttpPost]
    [Function("CreateRisk", "新增風險", Order = 2, Description = "PM／Tech Lead 新增風險")]
    [RequiresPermission("PMS.RISK.MANAGE", "維護專案風險", ActionCategory.Operational)]
    [ProducesResponseType(typeof(RiskDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<RiskDto>> Create([FromBody] CreateRiskRequest request, CancellationToken ct = default) =>
        FromResult(await risks.CreateAsync(CurrentUserId, Actor, request, ct).ConfigureAwait(false),
            dto => Created($"/api/v1/pms/risks/{dto.Id}", dto));

    /// <summary>取得風險</summary>
    [HttpGet("{id:guid}")]
    [Function("GetRisk", "風險詳情", Order = 3, Description = "單筆風險")]
    [RequiresPermission("PMS.RISK.READ", "檢視專案風險")]
    [ProducesResponseType(typeof(RiskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await risks.GetAsync(CurrentUserId, id, ct).ConfigureAwait(false));

    /// <summary>更新風險（PM／Tech Lead 全部欄位；風險負責人僅狀態與因應措施）</summary>
    [HttpPut("{id:guid}")]
    [Function("UpdateRisk", "更新風險", Order = 4, Description = "更新風險等級、狀態、因應措施與負責人")]
    [RequiresPermission("PMS.RISK.MANAGE", "維護專案風險", ActionCategory.Operational)]
    [ProducesResponseType(typeof(RiskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskDto>> Update(Guid id, [FromBody] UpdateRiskRequest request, CancellationToken ct = default) =>
        FromResult(await risks.UpdateAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>作廢風險（軟刪除，僅專案 PM／Tech Lead）</summary>
    [HttpDelete("{id:guid}")]
    [Function("VoidRisk", "作廢風險", Order = 5, Description = "作廢風險（保留稽核）")]
    [RequiresPermission("PMS.RISK.MANAGE", "維護專案風險", ActionCategory.Critical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Void(Guid id, CancellationToken ct = default) =>
        NoContentFrom(await risks.VoidAsync(CurrentUserId, Actor, id, ct).ConfigureAwait(false));
}
