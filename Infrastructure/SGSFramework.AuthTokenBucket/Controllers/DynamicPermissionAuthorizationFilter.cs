using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Permissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Controllers
{

    // ==========================================
    // Controllers 層：動態授權 Filter 與屬性整合
    // 配合現有的 SGSFramework.Core.Abstractions.Attributes.RequiresPermissionAttribute
    // ==========================================
    public class DynamicPermissionAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly string _permissionCode;

        public DynamicPermissionAuthorizationFilter(string permissionCode)
        {
            _permissionCode = permissionCode;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var services = context.HttpContext.RequestServices;
            var registry = services.GetRequiredService<IPermissionRegistry>();

            long requiredBit = registry.GetOrCreateMask(_permissionCode);

            var userMaskClaim = context.HttpContext.User.FindFirst("perm_mask")?.Value;
            if (!long.TryParse(userMaskClaim, out long userMask))
            {
                context.Result = new ForbidResult();
                return;
            }

            bool hasPermission = (userMask & requiredBit) == requiredBit;
            if (!hasPermission)
            {
                context.Result = new ForbidResult();
            }

            await Task.CompletedTask;
        }
    }
}
