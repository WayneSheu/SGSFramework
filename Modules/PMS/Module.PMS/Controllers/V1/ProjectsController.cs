using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Services;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Controllers.V1;

/// <summary>
/// 專案管理：專案主檔（專案代號由 PMS 核發且不可變）、RACI 成員、模組目錄、SSDLC 卡控節點與階段推進。
/// 功能權限由 [RequiresPermission] 控管；專案資料列授權（僅成員可見，非成員一律 404）由 ProjectService 套用。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/pms/projects")]
[ControllerTitle("專案維護", Icon = "fa-solid fa-diagram-project", Order = 32, Description = "專案主檔、RACI 成員、SSDLC 卡控節點、交付物與維運流程（SOP v3.0）")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ProjectsController(ProjectService projects) : PmsControllerBase
{
    /// <summary>查詢我可見的專案清單</summary>
    [HttpPost("search")]
    [Function("SearchProjects", "專案清單", Icon = "fa-solid fa-diagram-project", Order = 1, Description = "查詢我參與的專案與階段進度", IsMenu = true, Path = "/sgs.modules.pms/projects")]
    [RequiresPermission("PMS.PROJECT.READ", "檢視專案")]
    [ProducesResponseType(typeof(PagedResult<ProjectListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProjectListDto>>> Search([FromBody] ProjectSearchRequest request, CancellationToken ct = default) =>
        Ok(await projects.SearchAsync(CurrentUserId, request ?? new ProjectSearchRequest(), ct).ConfigureAwait(false));

    /// <summary>建立專案並核發專案代號</summary>
    [HttpPost]
    [Function("CreateProject", "建立專案", Order = 2, Description = "建立專案並核發不可變的專案代號")]
    [RequiresPermission("PMS.PROJECT.MANAGE", "管理專案", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectDetailDto>> Create([FromBody] CreateProjectRequest request, CancellationToken ct = default)
    {
        var result = await projects.CreateAsync(Actor, request, ct).ConfigureAwait(false);
        return FromResult(result, dto => Created($"/api/v1/pms/projects/{dto.Id}", dto));
    }

    /// <summary>取得專案詳情</summary>
    [HttpGet("{id:guid}")]
    [Function("GetProject", "專案詳情", Order = 3, Description = "專案主檔、成員、模組與卡控節點")]
    [RequiresPermission("PMS.PROJECT.READ", "檢視專案")]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectDetailDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await projects.GetAsync(CurrentUserId, id, ct).ConfigureAwait(false));

    /// <summary>更新專案（專案代號不可修改）</summary>
    [HttpPut("{id:guid}")]
    [Function("UpdateProject", "更新專案", Order = 4, Description = "更新名稱、期別、凍結線與合約 SLA 備註")]
    [RequiresPermission("PMS.PROJECT.MANAGE", "管理專案", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectDetailDto>> Update(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct = default) =>
        FromResult(await projects.UpdateAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>推進專案階段（須先通過前置卡控節點）</summary>
    [HttpPut("{id:guid}/phase")]
    [Function("AdvanceProjectPhase", "推進階段", Order = 5, Description = "依六階段順序推進，受 SSDLC 卡控節點限制")]
    [RequiresPermission("PMS.PROJECT.MANAGE", "管理專案", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectDetailDto>> AdvancePhase(Guid id, [FromBody] AdvancePhaseRequest request, CancellationToken ct = default) =>
        FromResult(await projects.AdvancePhaseAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>建議專案代號</summary>
    [HttpGet("code/suggest")]
    [Function("SuggestProjectCode", "建議專案代號", Order = 6, Description = "依專案名稱縮寫產生可用代號候選")]
    [RequiresPermission("PMS.PROJECTCODE.MANAGE", "核發專案代號", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ProjectCodeSuggestionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectCodeSuggestionDto>> SuggestCode([FromQuery] string? name, CancellationToken ct = default) =>
        Ok(await projects.SuggestCodeAsync(name, ct).ConfigureAwait(false));

    /// <summary>檢查專案代號可用性</summary>
    [HttpGet("code/check")]
    [Function("CheckProjectCode", "檢查專案代號", Order = 7, Description = "檢查代號格式、保留字與是否曾使用")]
    [RequiresPermission("PMS.PROJECTCODE.MANAGE", "核發專案代號", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ProjectCodeCheckDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectCodeCheckDto>> CheckCode([FromQuery] string? code, CancellationToken ct = default) =>
        Ok(await projects.CheckCodeAsync(code, ct).ConfigureAwait(false));

    /// <summary>新增專案成員</summary>
    [HttpPost("{id:guid}/members")]
    [Function("AddProjectMember", "新增專案成員", Order = 8, Description = "新增 BE／FE 成員（PM、Tech Lead 於建立專案時指定）")]
    [RequiresPermission("PMS.PROJECT.MANAGE", "管理專案", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ProjectMemberDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectMemberDto>> AddMember(Guid id, [FromBody] AddMemberRequest request, CancellationToken ct = default) =>
        FromResult(await projects.AddMemberAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false),
            dto => Created($"/api/v1/pms/projects/{id}", dto));

    /// <summary>移除專案成員</summary>
    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [Function("RemoveProjectMember", "移除專案成員", Order = 9, Description = "移除 BE／FE 成員（PM 與 Tech Lead 不可移除）")]
    [RequiresPermission("PMS.PROJECT.MANAGE", "管理專案", ActionCategory.Critical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken ct = default) =>
        NoContentFrom(await projects.RemoveMemberAsync(CurrentUserId, id, memberId, ct).ConfigureAwait(false));

    /// <summary>新增模組目錄（Mxx）</summary>
    [HttpPost("{id:guid}/modules")]
    [Function("AddProjectModule", "新增專案模組", Order = 10, Description = "新增模組代號 Mxx 與名稱，供文件命名與歸檔共用")]
    [RequiresPermission("PMS.PROJECT.MANAGE", "管理專案", ActionCategory.Operational)]
    [ProducesResponseType(typeof(PmsModuleDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PmsModuleDto>> AddModule(Guid id, [FromBody] AddModuleRequest request, CancellationToken ct = default) =>
        FromResult(await projects.AddModuleAsync(CurrentUserId, Actor, id, request, ct).ConfigureAwait(false),
            dto => Created($"/api/v1/pms/projects/{id}", dto));

    /// <summary>放行或駁回 SSDLC 卡控節點（僅專案 Tech Lead）</summary>
    [HttpPost("{id:guid}/gates/{gate}/decide")]
    [Function("DecideProjectGate", "卡控節點放行", Order = 11, Description = "Design／Build／Verification／Release 四道卡控節點，依序放行")]
    [RequiresPermission("PMS.PROJECT.MANAGE", "管理專案", ActionCategory.Critical)]
    [ProducesResponseType(typeof(ProjectGateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectGateDto>> DecideGate(Guid id, string gate, [FromBody] DecideGateRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<GateKind>(gate, ignoreCase: true, out var kind))
            return ProblemFrom(PmsError.Validation("gate 須為 Design、Build、Verification 或 Release。"));

        return FromResult(await projects.DecideGateAsync(CurrentUserId, Actor, id, kind, request, ct).ConfigureAwait(false));
    }
}
