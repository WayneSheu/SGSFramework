using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.RUL.Application.Contracts;

namespace SGS.Modules.RUL.Controllers.V1;

/// <summary>RUL 控制器共用：目前使用者、RulResult → ProblemDetails 映射。</summary>
public abstract class RulControllerBase : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    protected string Actor =>
        User.Identity?.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "未知使用者";

    protected ActionResult<T> FromResult<T>(RulResult<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemFrom(result.Error!);

    protected ActionResult NoContentFrom(RulResult<bool> result) =>
        result.IsSuccess ? NoContent() : ProblemFrom(result.Error!);

    protected ObjectResult ProblemFrom(RulError error)
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
