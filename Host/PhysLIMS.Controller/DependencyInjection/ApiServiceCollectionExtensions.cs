
namespace SGSFramework.ApiInfrastructure.DependencyInjection;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.Core.Errors;
using System;
using System.Linq;

/// <summary>
/// 展現層與控制行為之依賴注入擴充類別。
/// </summary>
public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// 配置 Web API 全域模型驗證 (ModelState) 自動轉換為企業級 ProblemDetails 格式
    /// </summary>
    public static IServiceCollection AddCustomApiBehavior(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = actionContext =>
            {
                var errors = actionContext.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .SelectMany(e => e.Value!.Errors.Select(er => Error.Validation(e.Key, er.ErrorMessage)))
                    .ToList();

                var validationError = ValidationError.FromErrors(errors);

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = validationError.Code,
                    Detail = validationError.Message,
                    Instance = actionContext.HttpContext.Request.Path
                };

                problemDetails.Extensions["errors"] = errors.Select(e => new
                {
                    field = e.Code,
                    message = e.Message
                });

                return new BadRequestObjectResult(problemDetails);
            };
        });

        return services;
    }
}