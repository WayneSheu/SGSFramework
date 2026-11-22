using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.MTG.Application.Contracts;
using SGS.Modules.MTG.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;

namespace SGS.Modules.MTG.Controllers.V1;

/// <summary>
/// 會議待辦事項：由紀要抽取或人工新增，指派負責人與期限，並郵件通知。
/// 紀要產生完成後會自動抽取一次；之後可手動重新抽取（只會取代沒被人碰過的抽取結果）。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[ControllerTitle("待辦事項", Icon = "fa-solid fa-list-check", Order = 2, Description = "會議待辦事項追蹤與通知")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ActionItemsController(ActionItemService actionItems) : MtgControllerBase
{
    /// <summary>跨會議分頁查詢待辦（可只看逾期未完成者）</summary>
    [HttpPost("api/v{version:apiVersion}/mtg/action-items/search")]
    [Function("SearchMeetingActionItems", "待辦事項", Icon = "fa-solid fa-list-check", Order = 1, Description = "跨會議的待辦事項追蹤", IsMenu = true, Path = "/sgs.modules.mtg/action-items")]
    [RequiresPermission("MTG.ACTION.READ", "檢視待辦事項")]
    [ProducesResponseType(typeof(PagedResult<ActionItemListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ActionItemListDto>>> Search([FromBody] ActionItemQueryRequest request, CancellationToken ct = default) =>
        Ok(await actionItems.SearchAsync(request ?? new ActionItemQueryRequest(), ct).ConfigureAwait(false));

    /// <summary>某場會議的待辦清單</summary>
    [HttpGet("api/v{version:apiVersion}/mtg/meetings/{meetingId:guid}/action-items")]
    [Function("ListMeetingActionItems", "會議待辦清單", Order = 2, Description = "取得某場會議的待辦事項")]
    [RequiresPermission("MTG.ACTION.READ", "檢視待辦事項")]
    [ProducesResponseType(typeof(IReadOnlyList<ActionItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ActionItemDto>>> List(Guid meetingId, CancellationToken ct = default) =>
        FromResult(await actionItems.ListAsync(meetingId, ct).ConfigureAwait(false));

    /// <summary>新增待辦</summary>
    [HttpPost("api/v{version:apiVersion}/mtg/meetings/{meetingId:guid}/action-items")]
    [Function("CreateMeetingActionItem", "新增待辦", Order = 3, Description = "人工新增待辦事項")]
    [RequiresPermission("MTG.ACTION.MANAGE", "維護待辦事項", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ActionItemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ActionItemDto>> Create(Guid meetingId, [FromBody] SaveActionItemRequest request, CancellationToken ct = default) =>
        FromResult(await actionItems.CreateAsync(Actor, meetingId, request, ct).ConfigureAwait(false));

    /// <summary>修改待辦（內容、負責人、信箱、期限、狀態）</summary>
    [HttpPut("api/v{version:apiVersion}/mtg/meetings/{meetingId:guid}/action-items/{itemId:guid}")]
    [Function("UpdateMeetingActionItem", "修改待辦", Order = 4, Description = "修改待辦事項與狀態")]
    [RequiresPermission("MTG.ACTION.MANAGE", "維護待辦事項", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ActionItemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ActionItemDto>> Update(Guid meetingId, Guid itemId, [FromBody] SaveActionItemRequest request, CancellationToken ct = default) =>
        FromResult(await actionItems.UpdateAsync(Actor, meetingId, itemId, request, ct).ConfigureAwait(false));

    /// <summary>刪除待辦</summary>
    [HttpDelete("api/v{version:apiVersion}/mtg/meetings/{meetingId:guid}/action-items/{itemId:guid}")]
    [Function("DeleteMeetingActionItem", "刪除待辦", Order = 5, Description = "刪除待辦事項")]
    [RequiresPermission("MTG.ACTION.MANAGE", "維護待辦事項", ActionCategory.Operational)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid meetingId, Guid itemId, CancellationToken ct = default) =>
        NoContentFrom(await actionItems.DeleteAsync(meetingId, itemId, ct).ConfigureAwait(false));

    /// <summary>從最新一版紀要重新抽取待辦（沒被編輯過的抽取結果會換新，人工編輯過的保留）</summary>
    [HttpPost("api/v{version:apiVersion}/mtg/meetings/{meetingId:guid}/action-items/extract")]
    [Function("ExtractMeetingActionItems", "抽取待辦", Order = 6, Description = "從紀要抽取待辦事項")]
    [RequiresPermission("MTG.ACTION.MANAGE", "維護待辦事項", ActionCategory.Operational)]
    [ProducesResponseType(typeof(ExtractActionItemsResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExtractActionItemsResult>> Extract(Guid meetingId, CancellationToken ct = default) =>
        FromResult(await actionItems.ExtractAsync(Actor, meetingId, ct).ConfigureAwait(false));

    /// <summary>郵件通知待辦：寄給有信箱的負責人，並可另寄一封完整清單給額外收件人</summary>
    [HttpPost("api/v{version:apiVersion}/mtg/meetings/{meetingId:guid}/action-items/notify")]
    [Function("NotifyMeetingActionItems", "通知待辦", Order = 7, Description = "以郵件通知待辦負責人")]
    [RequiresPermission("MTG.ACTION.NOTIFY", "寄送待辦通知", ActionCategory.Critical)]
    [ProducesResponseType(typeof(NotifyActionItemsResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotifyActionItemsResult>> Notify(Guid meetingId, [FromBody] NotifyActionItemsRequest? request, CancellationToken ct = default) =>
        FromResult(await actionItems.NotifyAsync(Actor, meetingId, request, ct).ConfigureAwait(false));
}
