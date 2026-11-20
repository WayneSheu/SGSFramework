using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.MAN.Application.Contracts;

namespace SGS.Modules.MAN.Controllers.V1;

/// <summary>MAN 控制器共用：目前使用者、ManResult → ProblemDetails 映射（含 MAN 業務錯誤碼 code）。</summary>
public abstract class ManControllerBase : ControllerBase
{
    protected string Actor =>
        User.Identity?.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "未知使用者";

    protected ActionResult<T> FromResult<T>(ManResult<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemFrom(result.Error!);

    protected ActionResult NoContentFrom(ManResult<bool> result) =>
        result.IsSuccess ? NoContent() : ProblemFrom(result.Error!);

    protected ObjectResult ProblemFrom(ManError error)
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
