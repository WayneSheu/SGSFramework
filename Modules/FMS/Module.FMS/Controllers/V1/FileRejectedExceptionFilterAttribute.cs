using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.FMS.Controllers.V1;

/// <summary>將 <see cref="FileRejectedException"/> 轉為對應狀態碼的 ProblemDetails（可預期錯誤，不進入全域 500 例外處理）</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class FileRejectedExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Exception is not FileRejectedException ex) return;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = ex.StatusCode,
            Title = "檔案請求被拒絕",
            Detail = ex.Message,
            Instance = context.HttpContext.Request.Path
        })
        { StatusCode = ex.StatusCode };
        context.ExceptionHandled = true;
    }
}
