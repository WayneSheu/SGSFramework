using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.MAN.Application.Contracts;
using SGS.Modules.MAN.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;

namespace SGS.Modules.MAN.Controllers.V1;

/// <summary>
/// 內嵌提示讀取端點：任何已登入使用者皆可讀取「已發布」的 Tooltip 字典（不含操作步驟等內容），
/// 供前端 SmartTooltip 全站使用；不需額外權限，且不登錄為功能節點。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/man/tooltips")]
[ApiExplorerSettings(IgnoreApi = false)]
[Produces(MediaTypeNames.Application.Json)]
public sealed class TooltipsController(ManualTopicService topics) : ManControllerBase
{
    /// <summary>取得已發布的 Tooltip 字典與版本（ETag 供前端快取）</summary>
    [HttpGet]
    [ProducesResponseType(typeof(TooltipDictionaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<ActionResult<TooltipDictionaryDto>> Get(CancellationToken ct = default)
    {
        var dto = await topics.GetTooltipsAsync(ct).ConfigureAwait(false);
        var etag = $"\"{dto.Version}\"";
        if (Request.Headers.IfNoneMatch == etag) return StatusCode(StatusCodes.Status304NotModified);

        Response.Headers.ETag = etag;
        return Ok(dto);
    }
}
