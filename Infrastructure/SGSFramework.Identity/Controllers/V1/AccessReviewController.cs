using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Identity.Services;

namespace SGSFramework.Identity.Controllers.V1;

/// <summary>
/// 定期權限覆核 (Access Certification)：建立覆核活動、由覆核者逐筆確認保留或回收角色指派。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/access-reviews")]
[ControllerTitle("權限覆核", Icon = "fa-solid fa-user-check", Order = 16, Description = "定期檢視使用者的角色指派是否仍符合工作需求，並回收不需要的權限")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class AccessReviewController(IAccessReviewService accessReviewService) : ControllerBase
{
    private readonly IAccessReviewService _service = accessReviewService ?? throw new ArgumentNullException(nameof(accessReviewService));

    public sealed record CreateCampaignRequest(string Name, string? Description, DateTimeOffset DueDate, List<Guid>? RoleIds);
    public sealed record DecideRequest(List<Guid> ItemIds, AccessReviewDecision Decision, string? Comment);

    /// <summary>查詢覆核活動與進度</summary>
    [HttpGet("campaigns")]
    [Function("GetAccessReviewCampaigns", "定期權限覆核", Icon = "fa-solid fa-user-check", Order = 1,
        Description = "檢視權限覆核活動並執行覆核", IsMenu = true, Path = "/sgsframework.system/accessreview")]
    [RequiresPermission("SYSTEM.ACCESSREVIEW.READ", "檢視權限覆核")]
    public async Task<IActionResult> GetCampaigns(CancellationToken cancellationToken = default) =>
        Ok(await _service.GetCampaignsAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>建立覆核活動（快照當下的角色成員）</summary>
    [HttpPost("campaigns")]
    [Function("CreateAccessReviewCampaign", "建立權限覆核活動", Icon = "fa-solid fa-plus", Order = 2, Description = "建立定期權限覆核活動")]
    [RequiresPermission("SYSTEM.ACCESSREVIEW.MANAGE", "管理權限覆核活動")]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (ok, message, id) = await _service.CreateCampaignAsync(request.Name, request.Description, request.DueDate, request.RoleIds, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message, CampaignId = id }) : Problem400("建立覆核活動失敗", message);
    }

    /// <summary>結束覆核活動</summary>
    [HttpPost("campaigns/{id:guid}/close")]
    [Function("CloseAccessReviewCampaign", "結束權限覆核活動", Icon = "fa-solid fa-flag-checkered", Order = 3, Description = "結束權限覆核活動")]
    [RequiresPermission("SYSTEM.ACCESSREVIEW.MANAGE", "管理權限覆核活動")]
    public async Task<IActionResult> CloseCampaign([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _service.CloseCampaignAsync(id, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("結束覆核活動失敗", message);
    }

    /// <summary>查詢覆核項目；mine=true 時僅回傳目前使用者負責範圍</summary>
    [HttpGet("campaigns/{id:guid}/items")]
    [Function("GetAccessReviewItems", "檢視覆核項目", Icon = "fa-solid fa-list-check", Order = 4, Description = "檢視權限覆核活動的覆核項目")]
    [RequiresPermission("SYSTEM.ACCESSREVIEW.READ", "檢視權限覆核")]
    public async Task<IActionResult> GetItems([FromRoute] Guid id, [FromQuery] bool mine = true, CancellationToken cancellationToken = default) =>
        Ok(await _service.GetItemsAsync(id, mine, cancellationToken).ConfigureAwait(false));

    /// <summary>覆核決定：保留或回收（回收立即解除角色指派）</summary>
    [HttpPost("items/decide")]
    [Function("DecideAccessReviewItems", "執行權限覆核", Icon = "fa-solid fa-gavel", Order = 5, Description = "確認保留或回收角色指派")]
    [RequiresPermission("SYSTEM.ACCESSREVIEW.REVIEW", "執行權限覆核")]
    public async Task<IActionResult> Decide([FromBody] DecideRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (ok, message, processed) = await _service.DecideAsync(request.ItemIds ?? [], request.Decision, request.Comment, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message, Processed = processed }) : Problem400("覆核失敗", message);
    }

    private BadRequestObjectResult Problem400(string title, string detail) => BadRequest(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path
    });
}
