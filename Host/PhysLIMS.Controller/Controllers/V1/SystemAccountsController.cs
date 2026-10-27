using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;
using SGSFramework.Identity.Services;

namespace SGSFramework.ApiInfrastructure.Controllers.V1;

/// <summary>
/// 系統帳號管理控制器（服務間通訊 M2M 身分：建立、操作範圍、狀態、金鑰輪替、稽核歷程）
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/system-accounts")]
[ControllerTitle("系統帳號管理", Icon = "fa-solid fa-robot", Order = 24, Description = "管理內部微服務與排程服務使用的系統帳號（Client Credentials）、操作範圍與金鑰")]
[RequiresPermission("SYSTEM.SYSTEMACCOUNTS.READ", "系統帳號管理")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public sealed class SystemAccountsController : ApiControllerBase
{
    private readonly ISystemAccountService _systemAccountService;
    private readonly SystemScopeCatalog _scopeCatalog;
    private readonly ILogger<SystemAccountsController> _logger;

    public SystemAccountsController(
        ISystemAccountService systemAccountService,
        SystemScopeCatalog scopeCatalog,
        ILogger<SystemAccountsController> logger)
    {
        ArgumentNullException.ThrowIfNull(systemAccountService);
        ArgumentNullException.ThrowIfNull(scopeCatalog);
        ArgumentNullException.ThrowIfNull(logger);

        _systemAccountService = systemAccountService;
        _scopeCatalog = scopeCatalog;
        _logger = logger;
    }

    /// <summary>
    /// 取得系統帳號清單
    /// </summary>
    [HttpGet]
    [Function("GetSystemAccounts", "系統帳號管理", Icon = "fa-solid fa-robot", Order = 1, Description = "檢視與維護服務間通訊使用的系統帳號", IsMenu = true, Path = "/sgsframework.system/systemaccounts")]
    [RequiresPermission("SYSTEM.SYSTEMACCOUNTS.READ")]
    [EndpointSummary("系統帳號清單")]
    [EndpointDescription("取得所有系統帳號（不含金鑰）。")]
    [ProducesResponseType(typeof(IReadOnlyList<SystemAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAccounts(CancellationToken cancellationToken = default)
    {
        var accounts = await _systemAccountService.GetAccountsAsync(cancellationToken);
        return Ok(accounts);
    }

    /// <summary>
    /// 取得單一系統帳號
    /// </summary>
    [HttpGet("{id}")]
    [Function("GetSystemAccount", "系統帳號明細", Icon = "fa-solid fa-id-card", Order = 2, Description = "取得單一系統帳號設定")]
    [RequiresPermission("SYSTEM.SYSTEMACCOUNTS.READ")]
    [EndpointSummary("系統帳號明細")]
    [ProducesResponseType(typeof(SystemAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccount(string id, CancellationToken cancellationToken = default)
    {
        var account = await _systemAccountService.GetAccountAsync(id, cancellationToken);
        return account is null ? NotFoundProblem(id) : Ok(account);
    }

    /// <summary>
    /// 取得可授予的操作範圍目錄
    /// </summary>
    [HttpGet("scope-catalog")]
    [Function("GetSystemScopeCatalog", "操作範圍目錄", Icon = "fa-solid fa-list-check", Order = 3, Description = "列出系統中以 RequiresSystemScope 宣告的操作範圍")]
    [RequiresPermission("SYSTEM.SYSTEMACCOUNTS.READ")]
    [EndpointSummary("操作範圍目錄")]
    [EndpointDescription("彙整目前已載入 Controller 上的 [RequiresSystemScope] 宣告與對應端點。")]
    [ProducesResponseType(typeof(IReadOnlyList<SystemScopeCatalogItemDto>), StatusCodes.Status200OK)]
    public IActionResult GetScopeCatalog() => Ok(_scopeCatalog.GetCatalog());

    /// <summary>
    /// 建立系統帳號
    /// </summary>
    [HttpPost]
    [Function("CreateSystemAccount", "建立系統帳號", Icon = "fa-solid fa-plus", Order = 4, Description = "建立系統帳號並產生金鑰（金鑰僅顯示一次）")]
    [RequiresPermission("SYSTEM.SYSTEMACCOUNTS.CREATE", "建立系統帳號")]
    [EndpointSummary("建立系統帳號")]
    [EndpointDescription("建立系統帳號並產生 client_secret；明文金鑰僅於此回應出現一次，伺服器僅保存雜湊值。")]
    [ProducesResponseType(typeof(SystemAccountSecretResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSystemAccountRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await ExecuteAsync(async () =>
        {
            var result = await _systemAccountService.CreateAsync(request, ClientIp, cancellationToken);
            Response.Headers.CacheControl = "no-store";
            return CreatedAtAction(nameof(GetAccount), new { id = result.Account.Id, version = "1" }, result);
        }, "建立系統帳號");
    }

    /// <summary>
    /// 更新系統帳號基本設定
    /// </summary>
    [HttpPut("{id}")]
    [Function("UpdateSystemAccount", "更新系統帳號", Icon = "fa-solid fa-pen", Order = 5, Description = "更新顯示名稱、說明、權杖有效時間與允許來源 IP")]
    [RequiresPermission("SYSTEM.SYSTEMACCOUNTS.UPDATE", "更新系統帳號")]
    [EndpointSummary("更新系統帳號")]
    [ProducesResponseType(typeof(SystemAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> Update(string id, [FromBody] UpdateSystemAccountRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteAsync(async () => Ok(await _systemAccountService.UpdateAsync(id, request, ClientIp, cancellationToken)), "更新系統帳號", id);
    }

    /// <summary>
    /// 更新操作範圍
    /// </summary>
    [HttpPut("{id}/scopes")]
    [Function("UpdateSystemAccountScopes", "更新操作範圍", Icon = "fa-solid fa-key", Order = 6, Description = "整批設定系統帳號的操作範圍（既有權杖將撤銷）")]
    [RequiresPermission("SYSTEM.SYSTEMACCOUNTS.UPDATE")]
    [EndpointSummary("更新操作範圍")]
    [EndpointDescription("整批取代操作範圍；異動後既有權杖立即失效，用戶端收到 401 後會以新 Scope 重新換發。")]
    [ProducesResponseType(typeof(SystemAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> UpdateScopes(string id, [FromBody] UpdateSystemAccountScopesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteAsync(async () => Ok(await _systemAccountService.UpdateScopesAsync(id, request, ClientIp, cancellationToken)), "更新操作範圍", id);
    }

    /// <summary>
    /// 變更帳號狀態（啟用 / 停用 / 凍結）
    /// </summary>
    [HttpPut("{id}/status")]
    [Function("ChangeSystemAccountStatus", "變更系統帳號狀態", Icon = "fa-solid fa-toggle-on", Order = 7, Description = "啟用、停用或凍結系統帳號")]
    [RequiresPermission("SYSTEM.SYSTEMACCOUNTS.UPDATE")]
    [EndpointSummary("變更系統帳號狀態")]
    [EndpointDescription("停用或凍結時已簽發權杖立即失效；停用、凍結與解除凍結必須填寫原因。")]
    [ProducesResponseType(typeof(SystemAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> ChangeStatus(string id, [FromBody] ChangeSystemAccountStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteAsync(async () => Ok(await _systemAccountService.ChangeStatusAsync(id, request, ClientIp, cancellationToken)), "變更系統帳號狀態", id);
    }

    /// <summary>
    /// 輪替金鑰
    /// </summary>
    [HttpPost("{id}/rotate-secret")]
    [Function("RotateSystemAccountSecret", "輪替金鑰", Icon = "fa-solid fa-rotate", Order = 8, Description = "產生新的 client_secret（僅顯示一次）")]
    [RequiresPermission("SYSTEM.SYSTEMACCOUNTS.UPDATE")]
    [EndpointSummary("輪替金鑰")]
    [EndpointDescription("產生新的 client_secret，舊金鑰立即無法換發權杖；可選擇同時撤銷既有權杖。")]
    [ProducesResponseType(typeof(SystemAccountSecretResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> RotateSecret(string id, [FromBody] RotateSystemAccountSecretRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteAsync(async () =>
        {
            var result = await _systemAccountService.RotateSecretAsync(id, request, ClientIp, cancellationToken);
            Response.Headers.CacheControl = "no-store";
            return Ok(result);
        }, "輪替金鑰", id);
    }

    /// <summary>
    /// 撤銷所有已簽發權杖
    /// </summary>
    [HttpPost("{id}/revoke-tokens")]
    [Function("RevokeSystemAccountTokens", "撤銷權杖", Icon = "fa-solid fa-ban", Order = 9, Description = "立即撤銷系統帳號所有已簽發的權杖")]
    [RequiresPermission("SYSTEM.SYSTEMACCOUNTS.UPDATE")]
    [EndpointSummary("撤銷權杖")]
    [ProducesResponseType(typeof(SystemAccountDto), StatusCodes.Status200OK)]
    public Task<IActionResult> RevokeTokens(string id, [FromBody] RevokeSystemAccountTokensRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteAsync(async () => Ok(await _systemAccountService.RevokeTokensAsync(id, request, ClientIp, cancellationToken)), "撤銷權杖", id);
    }

    /// <summary>
    /// 刪除系統帳號（僅限停用狀態）
    /// </summary>
    [HttpDelete("{id}")]
    [Function("DeleteSystemAccount", "刪除系統帳號", Icon = "fa-solid fa-trash", Order = 10, Description = "刪除已停用的系統帳號（稽核歷程保留）")]
    [RequiresPermission("SYSTEM.SYSTEMACCOUNTS.DELETE", "刪除系統帳號")]
    [EndpointSummary("刪除系統帳號")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            await _systemAccountService.DeleteAsync(id, ClientIp, cancellationToken);
            return NoContent();
        }, "刪除系統帳號", id);

    /// <summary>
    /// 取得稽核歷程
    /// </summary>
    [HttpGet("audit-logs")]
    [Function("GetSystemAccountAuditLogs", "系統帳號稽核紀錄", Icon = "fa-solid fa-clock-rotate-left", Order = 11, Description = "檢視系統帳號異動與權杖換發歷程")]
    [RequiresPermission("SYSTEM.SYSTEMACCOUNTS.READ")]
    [EndpointSummary("系統帳號稽核紀錄")]
    [EndpointDescription("取得稽核歷程（新到舊，預設 200 筆），可依帳號篩選。")]
    [ProducesResponseType(typeof(IReadOnlyList<SystemAccountAuditLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs([FromQuery] string? accountId = null, [FromQuery] int take = 200, CancellationToken cancellationToken = default)
    {
        var logs = await _systemAccountService.GetAuditLogsAsync(accountId, take, cancellationToken);
        return Ok(logs);
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// 將服務層例外轉換為 ProblemDetails：ArgumentException → 400、KeyNotFoundException → 404、InvalidOperationException → 409
    /// </summary>
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action, string operation, string? id = null)
    {
        try
        {
            return await action();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "{Operation}之請求內容無效。", operation);
            return ProblemResult(StatusCodes.Status400BadRequest, "無效的請求參數", ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFoundProblem(id);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "{Operation}被拒絕：{Message}", operation, ex.Message);
            return ProblemResult(StatusCodes.Status409Conflict, "目前狀態不允許此操作", ex.Message);
        }
    }

    private ObjectResult NotFoundProblem(string? id) =>
        ProblemResult(StatusCodes.Status404NotFound, "找不到資源", $"找不到系統帳號 {id}。");

    private ObjectResult ProblemResult(int status, string title, string detail) => new(new ProblemDetails
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path
    })
    {
        StatusCode = status
    };
}
