using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;

namespace SGS.Modules.PMS.Controllers.V1;

/// <summary>
/// 專案甘特圖（SOP 啟動規劃：WBS、甘特圖）：任務（階層、起迄、負責人、進度）、前置相依、基準線與偏差，並疊加既有資料的里程碑。
/// 功能權限由 [RequiresPermission] 控管；PM／Tech Lead 維護全部，被指派者只能更新自己任務的進度（服務層判斷）；
/// 僅專案內部成員可見，客戶與非成員一律 404。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/pms/projects/{projectId:guid}/gantt")]
[ControllerTitle("專案甘特圖", Icon = "fa-solid fa-chart-gantt", Order = 42, Description = "WBS 任務排程、相依、基準線與里程碑")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class GanttController(GanttService gantt, GanttExportService exports) : PmsControllerBase
{
    /// <summary>取得甘特圖（任務、里程碑、基準線與偏差）；baselineId 省略＝最新基準線，Guid.Empty＝不對照</summary>
    [HttpGet]
    [Function("GetProjectGantt", "專案甘特圖", Icon = "fa-solid fa-chart-gantt", Order = 1, Description = "專案 WBS 甘特圖", IsMenu = true, Path = "/sgs.modules.pms/gantt")]
    [RequiresPermission("PMS.GANTT.READ", "檢視專案甘特圖")]
    [ProducesResponseType(typeof(GanttDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GanttDto>> Get(Guid projectId, [FromQuery] Guid? baselineId, CancellationToken ct = default) =>
        FromResult(await gantt.GetAsync(CurrentUserId, projectId, baselineId, ct).ConfigureAwait(false));

    /// <summary>新增任務（僅 PM／Tech Lead）</summary>
    [HttpPost("tasks")]
    [Function("CreateGanttTask", "新增甘特圖任務", Order = 2, Description = "新增 WBS 任務或里程碑")]
    [RequiresPermission("PMS.GANTT.MANAGE", "維護專案甘特圖", ActionCategory.Operational)]
    [ProducesResponseType(typeof(GanttMutationResult), StatusCodes.Status201Created)]
    public async Task<ActionResult<GanttMutationResult>> CreateTask(Guid projectId, [FromBody] CreateGanttTaskRequest request, CancellationToken ct = default) =>
        FromResult(await gantt.CreateTaskAsync(CurrentUserId, Actor, projectId, request, ct).ConfigureAwait(false),
            dto => Created($"/api/v1/pms/projects/{projectId}/gantt", dto));

    /// <summary>更新任務（僅 PM／Tech Lead）；日期變動可連動順延後繼任務</summary>
    [HttpPut("tasks/{taskId:guid}")]
    [Function("UpdateGanttTask", "更新甘特圖任務", Order = 3, Description = "更新任務名稱、日期、負責人與進度")]
    [RequiresPermission("PMS.GANTT.MANAGE", "維護專案甘特圖", ActionCategory.Operational)]
    [ProducesResponseType(typeof(GanttMutationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GanttMutationResult>> UpdateTask(Guid projectId, Guid taskId, [FromBody] UpdateGanttTaskRequest request, CancellationToken ct = default) =>
        FromResult(await gantt.UpdateTaskAsync(CurrentUserId, Actor, projectId, taskId, request, ct).ConfigureAwait(false));

    /// <summary>圖上拖曳排程：移動或縮放（僅 PM／Tech Lead）</summary>
    [HttpPut("tasks/{taskId:guid}/schedule")]
    [Function("ScheduleGanttTask", "調整甘特圖任務排程", Order = 4, Description = "拖曳移動或縮放任務，可連動順延後繼任務")]
    [RequiresPermission("PMS.GANTT.MANAGE", "維護專案甘特圖", ActionCategory.Operational)]
    [ProducesResponseType(typeof(GanttMutationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GanttMutationResult>> ScheduleTask(Guid projectId, Guid taskId, [FromBody] ScheduleGanttTaskRequest request, CancellationToken ct = default) =>
        FromResult(await gantt.ScheduleTaskAsync(CurrentUserId, Actor, projectId, taskId, request, ct).ConfigureAwait(false));

    /// <summary>更新任務進度（PM／Tech Lead 或被指派者）</summary>
    [HttpPut("tasks/{taskId:guid}/progress")]
    [Function("UpdateGanttTaskProgress", "更新甘特圖任務進度", Order = 5, Description = "更新任務完成百分比")]
    [RequiresPermission("PMS.GANTT.MANAGE", "維護專案甘特圖", ActionCategory.Operational)]
    [ProducesResponseType(typeof(GanttMutationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GanttMutationResult>> UpdateProgress(Guid projectId, Guid taskId, [FromBody] UpdateTaskProgressRequest request, CancellationToken ct = default) =>
        FromResult(await gantt.UpdateProgressAsync(CurrentUserId, Actor, projectId, taskId, request, ct).ConfigureAwait(false));

    /// <summary>調整階層與順序（僅 PM／Tech Lead）</summary>
    [HttpPut("tasks/{taskId:guid}/position")]
    [Function("MoveGanttTask", "調整甘特圖任務階層與順序", Order = 6, Description = "搬移任務到其他上層或調整同層順序")]
    [RequiresPermission("PMS.GANTT.MANAGE", "維護專案甘特圖", ActionCategory.Operational)]
    [ProducesResponseType(typeof(GanttMutationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GanttMutationResult>> MoveTask(Guid projectId, Guid taskId, [FromBody] MoveGanttTaskRequest request, CancellationToken ct = default) =>
        FromResult(await gantt.MoveTaskAsync(CurrentUserId, Actor, projectId, taskId, request, ct).ConfigureAwait(false));

    /// <summary>刪除任務（軟刪除，連同子任務與相關相依；僅 PM／Tech Lead）</summary>
    [HttpDelete("tasks/{taskId:guid}")]
    [Function("DeleteGanttTask", "刪除甘特圖任務", Order = 7, Description = "刪除任務與其子任務")]
    [RequiresPermission("PMS.GANTT.MANAGE", "維護專案甘特圖", ActionCategory.Critical)]
    [ProducesResponseType(typeof(GanttMutationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GanttMutationResult>> DeleteTask(Guid projectId, Guid taskId, CancellationToken ct = default) =>
        FromResult(await gantt.DeleteTaskAsync(CurrentUserId, Actor, projectId, taskId, ct).ConfigureAwait(false));

    /// <summary>新增前置相依（完成後才開始；僅 PM／Tech Lead）</summary>
    [HttpPost("dependencies")]
    [Function("AddGanttDependency", "新增甘特圖相依", Order = 8, Description = "設定任務前置相依")]
    [RequiresPermission("PMS.GANTT.MANAGE", "維護專案甘特圖", ActionCategory.Operational)]
    [ProducesResponseType(typeof(GanttMutationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GanttMutationResult>> AddDependency(Guid projectId, [FromBody] AddGanttDependencyRequest request, CancellationToken ct = default) =>
        FromResult(await gantt.AddDependencyAsync(CurrentUserId, Actor, projectId, request, ct).ConfigureAwait(false));

    /// <summary>移除前置相依（僅 PM／Tech Lead）</summary>
    [HttpDelete("dependencies/{dependencyId:guid}")]
    [Function("RemoveGanttDependency", "移除甘特圖相依", Order = 9, Description = "移除任務前置相依")]
    [RequiresPermission("PMS.GANTT.MANAGE", "維護專案甘特圖", ActionCategory.Operational)]
    [ProducesResponseType(typeof(GanttMutationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GanttMutationResult>> RemoveDependency(Guid projectId, Guid dependencyId, CancellationToken ct = default) =>
        FromResult(await gantt.RemoveDependencyAsync(CurrentUserId, projectId, dependencyId, ct).ConfigureAwait(false));

    /// <summary>建立基準線（對目前所有任務拍下起迄快照；僅 PM／Tech Lead）</summary>
    [HttpPost("baselines")]
    [Function("CreateGanttBaseline", "建立甘特圖基準線", Order = 10, Description = "拍下目前排程作為基準線")]
    [RequiresPermission("PMS.GANTT.MANAGE", "維護專案甘特圖", ActionCategory.Operational)]
    [ProducesResponseType(typeof(GanttBaselineDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<GanttBaselineDto>> CreateBaseline(Guid projectId, [FromBody] CreateBaselineRequest request, CancellationToken ct = default) =>
        FromResult(await gantt.CreateBaselineAsync(CurrentUserId, Actor, projectId, request, ct).ConfigureAwait(false),
            dto => Created($"/api/v1/pms/projects/{projectId}/gantt", dto));

    /// <summary>刪除基準線（僅 PM／Tech Lead）</summary>
    [HttpDelete("baselines/{baselineId:guid}")]
    [Function("DeleteGanttBaseline", "刪除甘特圖基準線", Order = 11, Description = "刪除基準線")]
    [RequiresPermission("PMS.GANTT.MANAGE", "維護專案甘特圖", ActionCategory.Critical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteBaseline(Guid projectId, Guid baselineId, CancellationToken ct = default) =>
        NoContentFrom(await gantt.DeleteBaselineAsync(CurrentUserId, projectId, baselineId, ct).ConfigureAwait(false));

    /// <summary>套用預設階段計畫（六個 SSDLC 階段，僅在專案尚無任務時；僅 PM／Tech Lead）</summary>
    [HttpPost("seed-phases")]
    [Function("SeedGanttPhases", "套用預設階段計畫", Order = 12, Description = "以六個 SSDLC 階段建立初始計畫")]
    [RequiresPermission("PMS.GANTT.MANAGE", "維護專案甘特圖", ActionCategory.Operational)]
    [ProducesResponseType(typeof(GanttMutationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GanttMutationResult>> SeedPhases(Guid projectId, [FromBody] SeedGanttPhasesRequest request, CancellationToken ct = default) =>
        FromResult(await gantt.SeedPhasesAsync(CurrentUserId, Actor, projectId, request, ct).ConfigureAwait(false));

    // 匯出端點刻意不加 [Function]：與選單入口共用 PMS.GANTT.READ，若登記為功能，選單同步會把選單節點改綁到這個「非選單」功能而隱藏。
    /// <summary>匯出 WBS（Excel）；POST 以 Body 傳遞對照基準線與負責人顯示名稱</summary>
    [HttpPost("export/wbs")]
    [RequiresPermission("PMS.GANTT.READ", "檢視專案甘特圖")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportWbs(Guid projectId, [FromBody] GanttExportRequest request, CancellationToken ct = default) =>
        ToFile(await exports.ExportWbsAsync(CurrentUserId, Actor, projectId, request, ct).ConfigureAwait(false));

    /// <summary>匯出甘特圖（PDF，A4 橫式）</summary>
    [HttpPost("export/pdf")]
    [RequiresPermission("PMS.GANTT.READ", "檢視專案甘特圖")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPdf(Guid projectId, [FromBody] GanttExportRequest request, CancellationToken ct = default) =>
        ToFile(await exports.ExportChartPdfAsync(CurrentUserId, Actor, projectId, request, ct).ConfigureAwait(false));

    private IActionResult ToFile(PmsResult<GanttExportFile> result)
    {
        if (result.Error is not null) return FromResult(result).Result!;

        var file = result.Value!;
        Response.Headers.CacheControl = "private, no-store";
        ApplySecurityHeaders();
        return File(file.Content, file.ContentType, file.FileName);
    }
}
