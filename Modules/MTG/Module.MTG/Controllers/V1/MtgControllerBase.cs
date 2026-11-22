using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.MTG.Application.Contracts;

namespace SGS.Modules.MTG.Controllers.V1;

/// <summary>MTG 控制器共用：目前使用者、MtgResult → ProblemDetails 映射（含 MTG 業務錯誤碼 code）。</summary>
public abstract class MtgControllerBase : ControllerBase
{
    protected string Actor =>
        User.Identity?.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "未知使用者";

    protected ActionResult<T> FromResult<T>(MtgResult<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemFrom(result.Error!);

    protected ActionResult NoContentFrom(MtgResult<bool> result) =>
        result.IsSuccess ? NoContent() : ProblemFrom(result.Error!);

    protected ObjectResult ProblemFrom(MtgError error)
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
