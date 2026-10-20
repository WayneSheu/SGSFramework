using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Attributes;

namespace SGSFramework.AuthTokenBucket.Controllers.V1;

/// <summary>
/// 區域實驗室功能開放範圍：設定各區域實驗室可使用的功能權限（實驗室內所有使用者的權限上限）。
/// 選單入口位於 組織管理 → 實驗室管理 的「開放功能」頁簽。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/lab-function-scopes")]
[ControllerTitle("實驗室功能範圍", Icon = "fa-solid fa-sliders", Order = 16, Description = "設定各區域實驗室開放的功能權限")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class LabFunctionScopeController(ILabFunctionScopeService labFunctionScopeService) : ControllerBase
{
    private readonly ILabFunctionScopeService _service = labFunctionScopeService ?? throw new ArgumentNullException(nameof(labFunctionScopeService));

    public sealed record SaveLabFunctionScopeRequest(bool IsRestricted, List<string>? PermissionKeys);

    /// <summary>取得區域實驗室的功能開放範圍</summary>
    [HttpGet("{tenantLabId:guid}")]
    [Function("GetLabFunctionScope", "檢視實驗室開放功能", Icon = "fa-solid fa-eye", Order = 1, Description = "檢視區域實驗室開放的功能權限")]
    [RequiresPermission("SYSTEM.LABFUNCTIONSCOPE.READ", "檢視實驗室開放功能")]
    [ProducesResponseType(typeof(LabFunctionScopeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromRoute] Guid tenantLabId, CancellationToken cancellationToken = default) =>
        Ok(await _service.GetAsync(tenantLabId, cancellationToken).ConfigureAwait(false));

    /// <summary>設定區域實驗室的功能開放範圍</summary>
    [HttpPut("{tenantLabId:guid}")]
    [Function("UpdateLabFunctionScope", "設定實驗室開放功能", Icon = "fa-solid fa-sliders", Order = 2, Description = "設定區域實驗室開放的功能權限（實驗室內所有使用者的權限上限）")]
    [RequiresPermission("SYSTEM.LABFUNCTIONSCOPE.UPDATE", "設定實驗室開放功能")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Save([FromRoute] Guid tenantLabId, [FromBody] SaveLabFunctionScopeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (ok, message) = await _service.SaveAsync(tenantLabId, request.IsRestricted, request.PermissionKeys, cancellationToken).ConfigureAwait(false);
        return ok
            ? Ok(new { Succeeded = true, Message = message })
            : BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "設定失敗", Detail = message });
    }
}
