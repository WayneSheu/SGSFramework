using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.CAL.Application.Contracts;

namespace SGS.Modules.CAL.Controllers.V1;

/// <summary>CAL 控制器共用：目前使用者、CalResult → ProblemDetails 映射（含 CAL 業務錯誤碼 code）。</summary>
public abstract class CalControllerBase : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    protected string Actor =>
        User.Identity?.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "未知使用者";

    protected ActionResult<T> FromResult<T>(CalResult<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemFrom(result.Error!);

    protected ActionResult NoContentFrom(CalResult<bool> result) =>
        result.IsSuccess ? NoContent() : ProblemFrom(result.Error!);

    protected ObjectResult ProblemFrom(CalError error)
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
