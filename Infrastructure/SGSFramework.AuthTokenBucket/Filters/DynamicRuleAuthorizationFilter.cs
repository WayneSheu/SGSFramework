using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.RuleEngine.Abstractions;
using SGSFramework.AuthTokenBucket.RuleEngine.Rules.Laboratory;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Filters
{
    /// <summary>
    /// 基於動態規則引擎，檢查使用者是否擁有所需權限的篩選器。
    /// </summary>
    public sealed class DynamicRuleAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly ILogger<DynamicRuleAuthorizationFilter> _logger;
        private readonly LaboratoryRuleEngine _ruleEngine;
        private readonly string[] _allowedCategories;

        public DynamicRuleAuthorizationFilter(
            ILogger<DynamicRuleAuthorizationFilter> logger,
            LaboratoryRuleEngine ruleEngine,
            string[] allowedCategories)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
            _allowedCategories = allowedCategories ?? Array.Empty<string>();
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var user = context.HttpContext.User;
            if (user.Identity is not { IsAuthenticated: true })
            {
                context.Result = new UnauthorizedObjectResult(new { message = "未授權的存取請求。" });
                return;
            }

            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var userId))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "無法解析使用者識別碼。" });
                return;
            }

            Guid targetLabId = Guid.Empty;
            if (context.HttpContext.Request.Headers.TryGetValue("X-Target-LabId", out var headerVal) &&
                Guid.TryParse(headerVal.ToString(), out var parsedLabId))
            {
                targetLabId = parsedLabId;
            }

            if (targetLabId == Guid.Empty)
            {
                context.Result = new BadRequestObjectResult(new { message = "缺少必要的實驗室上下文範圍 (TargetLabId)。" });
                return;
            }

            var authContext = new LabAuthContext
            {
                User = user,
                UserId = userId,
                TargetLabId = targetLabId,
                AllowedCategories = _allowedCategories
            };

            var (isPassed, errorMessage) = await _ruleEngine.ExecuteAsync(authContext, context.HttpContext.RequestAborted);
            if (!isPassed)
            {
                context.Result = new ObjectResult(new { message = errorMessage })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
