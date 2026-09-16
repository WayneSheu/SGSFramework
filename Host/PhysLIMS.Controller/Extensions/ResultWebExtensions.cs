
namespace SGSFramework.ApiInfrastructure.Extensions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Linq;

/// <summary>
/// 提供將 Domain Result 與 Error 轉譯為 RFC 7807 相容之 ProblemDetails API 回應。
/// </summary>
public static class ResultWebExtensions
{
    public static IActionResult ToActionResult(this Result result, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);

        return result.IsSuccess
            ? controller.NoContent()
            : MapErrorToProblemDetails(result.Error, controller);
    }

    public static IActionResult ToActionResult<TValue>(this Result<TValue> result, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);

        return result.IsSuccess
            ? (result.Value is null ? controller.NoContent() : controller.Ok(result.Value))
            : MapErrorToProblemDetails(result.Error, controller);
    }

    public static IActionResult MapErrorToProblemDetails(Error error, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(controller);

        int statusCode = error.Type switch
        {
            ErrorType.Failure => StatusCodes.Status400BadRequest,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            ErrorType.DependencyFailure => StatusCodes.Status502BadGateway,
            ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = error.Code,
            Detail = error.Message,
            Instance = controller.HttpContext.Request.Path
        };

        // 結構化轉譯 ValidationError 內部項目至 Extensions
        if (error is ValidationError validationError && validationError.Errors.Count > 0)
        {
            problemDetails.Extensions["errors"] = validationError.Errors.Select(e => new
            {
                field = e.Code,
                message = e.Message
            });
        }

        return controller.StatusCode(statusCode, problemDetails);
    }
}