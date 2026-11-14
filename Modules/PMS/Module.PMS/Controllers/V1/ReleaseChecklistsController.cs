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
/// 上線前資安與發布檢核表（FORM-SEC-01，SOP 附錄 A）：BE／FE 逐項檢核、交叉驗證（非主要作者）、
/// Tech Lead 放行（同時通過 Release Gate）、PM 確認。功能權限由 [RequiresPermission] 控管；
/// 誰能做哪個動作由專案角色（RACI）在服務層判斷，非專案內部成員一律 404。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/pms/release-checklists")]
[ControllerTitle("上線管理", Icon = "fa-solid fa-rocket", Order = 37, Description = "上線前資安與發布檢核、Release Gate 放行")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ReleaseChecklistsController(ReleaseChecklistService releases) : PmsControllerBase
{
    /// <summary>查詢上線檢核表（含阻斷／警告未完成數與簽核進度）</summary>
    [HttpPost("search")]
    [Function("SearchReleaseChecklists", "上線檢核表", Icon = "fa-solid fa-rocket", Order = 1, Description = "查詢上線前資安與發布檢核表", IsMenu = true, Path = "/sgs.modules.pms/release-checklists")]
    [RequiresPermission("PMS.RELEASE.READ", "檢視上線檢核表")]
    [ProducesResponseType(typeof(PagedResult<ReleaseListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReleaseListDto>>> Search([FromBody] ReleaseSearchRequest request, CancellationToken ct = default) =>
        Ok(await releases.SearchAsync(CurrentUserId, request ?? new ReleaseSearchRequest(), ct).ConfigureAwait(false));

    /// <summary>建立上線檢核表（由範本展開 SEC-01~11、REL-01~06）</summary>
    [HttpPost]
    [Function("CreateReleaseChecklist", "建立上線檢核表", Order = 2, Description = "建立版更上線檢核表")]
    [RequiresPermission("PMS.RELEASE.CHECK", "檢核上線項目", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ReleaseDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ReleaseDetailDto>> Create([FromBody] CreateReleaseRequest request, CancellationToken ct = default) =>
        FromResult(await releases.CreateAsync(CurrentUserId, Actor, request, ct).ConfigureAwait(false),
            dto => Created($"/api/v1/pms/release-checklists/{dto.Id}", dto));

    /// <summary>取得檢核表詳情（檢核項、簽核、阻斷未完成清單）</summary>
    [HttpGet("{id:guid}")]
    [Function("GetReleaseChecklist", "上線檢核表詳情", Order = 3, Description = "檢核項、簽核與阻斷未完成清單")]
    [RequiresPermission("PMS.RELEASE.READ", "檢視上線檢核表")]
    [ProducesResponseType(typeof(ReleaseDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReleaseDetailDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await releases.GetAsync(CurrentUserId, id, ct).ConfigureAwait(false));

    /// <summary>更新檢核項結果（通過／未通過／不適用）；放行後內容鎖定</summary>
    [HttpPut("{id:guid}/items/{code}")]
    [Function("UpdateReleaseChecklistItem", "更新檢核項", Order = 4, Description = "逐項檢核結果與備註")]
    [RequiresPermission("PMS.RELEASE.CHECK", "檢核上線項目", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ReleaseDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReleaseDetailDto>> UpdateItem(Guid id, string code, [FromBody] UpdateReleaseItemRequest request, CancellationToken ct = default) =>
        FromResult(await releases.UpdateItemAsync(CurrentUserId, Actor, id, code, request, ct).ConfigureAwait(false));

    /// <summary>放行簽核：交叉驗證（非主要作者）→ Tech Lead 放行（阻斷項全過、通過 Release Gate）→ PM 確認</summary>
    [HttpPost("{id:guid}/signoff")]
    [Function("SignReleaseChecklist", "上線放行簽核", Order = 5, Description = "交叉驗證、Tech Lead 放行與 PM 確認")]
    [RequiresPermission("PMS.RELEASE.APPROVE", "上線放行簽核", ActionCategory.Critical)]
    [ProducesResponseType(typeof(ReleaseDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReleaseDetailDto>> Sign(Guid id, [FromBody] SignReleaseRequest request, CancellationToken ct = default) =>
        FromResult(await releases.SignAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));
}
