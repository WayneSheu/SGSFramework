using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Permissions;
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;

namespace SGSFramework.AuthTokenBucket.Filters
{
    /// <summary>
    /// Controllers 層：動態授權 Filter
    /// 支援超過 64 位元權限點之 BitPosition 驗證
    /// </summary>
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

            // 🔑 取得該權限代碼對應的 BitPosition 索引位置
            int requiredBitPosition = registry.GetOrCreateBitPosition(_permissionCode);

            // 讀取 User Claim (可支援 Base64/Hex 編碼的 BitArray 字串或逗點分隔的 BitPositions 清單)
            var userBitPositionsClaim = context.HttpContext.User.FindFirst("perm_bits")?.Value;

            if (string.IsNullOrEmpty(userBitPositionsClaim))
            {
                // 向後相容：若為舊有 64 位元遮罩 (perm_mask)
                var oldMaskClaim = context.HttpContext.User.FindFirst("perm_mask")?.Value;
                if (long.TryParse(oldMaskClaim, out long userMask))
                {
                    if (requiredBitPosition < 64)
                    {
                        long requiredMask = 1L << requiredBitPosition;
                        if ((userMask & requiredMask) == requiredMask)
                        {
                            await Task.CompletedTask;
                            return;
                        }
                    }
                }

                context.Result = new ForbidResult();
                return;
            }

            // 解析使用者持有的所有 BitPositions (例: "0,1,5,12,78")
            var grantedBits = userBitPositionsClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                  .Select(int.Parse)
                                                  .ToHashSet();

            if (!grantedBits.Contains(requiredBitPosition))
            {
                context.Result = new ForbidResult();
                return;
            }

            await Task.CompletedTask;
        }
    }
}