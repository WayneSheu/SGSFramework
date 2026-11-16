using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.QTN.Application.Contracts;
using SGS.Modules.QTN.Application.Rules;
using SGSFramework.AuthTokenBucket.Abstractions;

namespace SGS.Modules.QTN.Controllers.V1;

/// <summary>
/// QTN 控制器共用：目前使用者、欄位級權限解析（QTN.COST.READ／QTN.PRICE.READ／QTN.ESTIMATE.EDIT／QTN.QUOTE.EDIT）、
/// QtnResult → ProblemDetails 映射（含 QTN 業務錯誤碼 code）。
/// </summary>
public abstract class QtnControllerBase : ControllerBase
{
    public const string CostRead = FieldSecurityPolicy.CostPermission;
    public const string PriceRead = FieldSecurityPolicy.PricePermission;
    public const string EstimateEdit = "QTN.ESTIMATE.EDIT";
    public const string QuoteEdit = "QTN.QUOTE.EDIT";

    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    protected string Actor =>
        User.Identity?.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "未知使用者";

    protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>解析呼叫者身分與欄位級權限（後端投影依據；最高管理者視為具備全部權限）。</summary>
    protected async Task<QtnCaller> CallerAsync(CancellationToken ct)
    {
        var auth = HttpContext.RequestServices.GetRequiredService<IPermissionAuthorizationService>();
        var cost = await auth.HasPermissionAsync(User, CostRead, ct).ConfigureAwait(false);
        var price = cost || await auth.HasPermissionAsync(User, PriceRead, ct).ConfigureAwait(false);
        var estimate = await auth.HasPermissionAsync(User, EstimateEdit, ct).ConfigureAwait(false);
        var edit = await auth.HasPermissionAsync(User, QuoteEdit, ct).ConfigureAwait(false);
        return new QtnCaller(CurrentUserId, Actor, ClientIp, FieldSecurityPolicy.Resolve(cost, price), estimate, edit);
    }

    protected async Task<bool> HasPermissionAsync(string code, CancellationToken ct) =>
        await HttpContext.RequestServices.GetRequiredService<IPermissionAuthorizationService>().HasPermissionAsync(User, code, ct).ConfigureAwait(false);

    /// <summary>檢視浮水印文字：「使用者 | UTC 時間 | IP」（沿用 FMS 格式）。</summary>
    protected string BuildWatermarkText(TimeProvider clock) =>
        $"{User.Identity?.Name ?? CurrentUserId.ToString()} | {clock.GetUtcNow():yyyy-MM-dd HH:mm:ss} UTC | {ClientIp ?? "-"}";

    /// <summary>nosniff 並以 CSP sandbox 阻止瀏覽器將檔案當作網頁執行。</summary>
    protected void ApplySecurityHeaders()
    {
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers["Access-Control-Expose-Headers"] = "Content-Disposition";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self' data:; style-src 'unsafe-inline'; sandbox";
    }

    protected ActionResult<T> FromResult<T>(QtnResult<T> result, Func<T, ActionResult<T>>? onSuccess = null)
    {
        if (result.IsSuccess) return onSuccess is null ? Ok(result.Value) : onSuccess(result.Value!);
        return ProblemFrom(result.Error!);
    }

    protected ActionResult NoContentFrom(QtnResult<bool> result) =>
        result.IsSuccess ? NoContent() : ProblemFrom(result.Error!);

    protected ObjectResult ProblemFrom(QtnError error)
    {
        var problem = new ProblemDetails
        {
            Status = error.Status,
            Title = error.Title,
            Detail = error.Detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        problem.Extensions["timestamp"] = DateTime.UtcNow;
        return StatusCode(error.Status, problem);
    }
}
