using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.MAN.Application.Contracts;
using SGS.Modules.MAN.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;

namespace SGS.Modules.MAN.Controllers.V1;

/// <summary>
/// 操作手冊主題維護：Tooltip、操作步驟與注意事項的單一來源。內容須經發布才會對全站 Tooltip 生效；
/// 修改已發布內容會退回草稿，須重新發布。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/man/topics")]
[ControllerTitle("手冊主題", Icon = "fa-solid fa-book-open", Order = 1, Description = "操作手冊主題、內嵌提示與發布管理")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ManualTopicsController(ManualTopicService topics) : ManControllerBase
{
    /// <summary>分頁查詢手冊主題</summary>
    [HttpPost("search")]
    [Function("SearchManualTopics", "手冊主題", Icon = "fa-solid fa-book-open", Order = 1, Description = "操作手冊主題維護", IsMenu = true, Path = "/sgs.modules.man/topics")]
    [RequiresPermission("MAN.TOPIC.READ", "檢視手冊主題")]
    [ProducesResponseType(typeof(PagedResult<ManualTopicDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ManualTopicDto>>> Search([FromBody] ManualTopicQueryRequest request, CancellationToken ct = default) =>
        Ok(await topics.SearchAsync(request, ct).ConfigureAwait(false));

    /// <summary>取得單一主題</summary>
    [HttpGet("{id:guid}")]
    [Function("GetManualTopic", "取得手冊主題", Order = 2, Description = "取得單一手冊主題內容")]
    [RequiresPermission("MAN.TOPIC.READ", "檢視手冊主題")]
    [ProducesResponseType(typeof(ManualTopicDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ManualTopicDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await topics.GetAsync(id, ct).ConfigureAwait(false));

    /// <summary>建立手冊主題（草稿）</summary>
    [HttpPost]
    [Function("CreateManualTopic", "建立手冊主題", Order = 3, Description = "建立手冊主題（草稿）")]
    [RequiresPermission("MAN.TOPIC.MANAGE", "維護手冊主題", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ManualTopicDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ManualTopicDto>> Create([FromBody] SaveManualTopicRequest request, CancellationToken ct = default) =>
        FromResult(await topics.CreateAsync(Actor, request, ct).ConfigureAwait(false));

    /// <summary>更新手冊主題（已發布者退回草稿）</summary>
    [HttpPut("{id:guid}")]
    [Function("UpdateManualTopic", "更新手冊主題", Order = 4, Description = "更新手冊主題；已發布內容修改後退回草稿")]
    [RequiresPermission("MAN.TOPIC.MANAGE", "維護手冊主題", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ManualTopicDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ManualTopicDto>> Update(Guid id, [FromBody] SaveManualTopicRequest request, CancellationToken ct = default) =>
        FromResult(await topics.UpdateAsync(Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>刪除手冊主題</summary>
    [HttpDelete("{id:guid}")]
    [Function("DeleteManualTopic", "刪除手冊主題", Order = 5, Description = "刪除手冊主題")]
    [RequiresPermission("MAN.TOPIC.MANAGE", "維護手冊主題", ActionCategory.Administrative)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct = default) =>
        NoContentFrom(await topics.DeleteAsync(id, ct).ConfigureAwait(false));

    /// <summary>發布主題（Tooltip 對全站生效）</summary>
    [HttpPost("{id:guid}/publish")]
    [Function("PublishManualTopic", "發布手冊主題", Order = 6, Description = "審核後發布手冊主題")]
    [RequiresPermission("MAN.TOPIC.PUBLISH", "發布手冊主題", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ManualTopicDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ManualTopicDto>> Publish(Guid id, CancellationToken ct = default) =>
        FromResult(await topics.PublishAsync(Actor, id, ct).ConfigureAwait(false));

    /// <summary>取消發布（退回草稿）</summary>
    [HttpPost("{id:guid}/unpublish")]
    [Function("UnpublishManualTopic", "取消發布手冊主題", Order = 7, Description = "取消發布並退回草稿")]
    [RequiresPermission("MAN.TOPIC.PUBLISH", "發布手冊主題", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ManualTopicDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ManualTopicDto>> Unpublish(Guid id, CancellationToken ct = default) =>
        FromResult(await topics.UnpublishAsync(Actor, id, ct).ConfigureAwait(false));
}
