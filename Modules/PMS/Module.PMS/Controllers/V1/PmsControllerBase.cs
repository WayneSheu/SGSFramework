using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.PMS.Application.Contracts;

namespace SGS.Modules.PMS.Controllers.V1;

/// <summary>PMS 控制器共用：目前使用者識別、PmsResult → ProblemDetails 映射（含 PMS 業務錯誤碼 code）。</summary>
public abstract class PmsControllerBase : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    protected string Actor =>
        User.Identity?.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "未知使用者";

    /// <summary>檢視浮水印文字：「使用者 | UTC 時間 | IP」（沿用 FMS 格式）。</summary>
    protected string BuildWatermarkText(TimeProvider clock)
    {
        var who = User.Identity?.Name ?? CurrentUserId.ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-";
        return $"{who} | {clock.GetUtcNow():yyyy-MM-dd HH:mm:ss} UTC | {ip}";
    }

    /// <summary>nosniff 並以 CSP sandbox 阻止瀏覽器將檔案當作網頁執行。</summary>
    protected void ApplySecurityHeaders()
    {
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self' data:; style-src 'unsafe-inline'; sandbox";
    }

    protected ActionResult<T> FromResult<T>(PmsResult<T> result, Func<T, ActionResult<T>>? onSuccess = null)
    {
        if (result.IsSuccess) return onSuccess is null ? Ok(result.Value) : onSuccess(result.Value!);
        return ProblemFrom(result.Error!);
    }

    protected ActionResult NoContentFrom(PmsResult<bool> result) =>
        result.IsSuccess ? NoContent() : ProblemFrom(result.Error!);

    protected ObjectResult ProblemFrom(PmsError error)
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
