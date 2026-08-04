using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace SGSFramework.Core.Identiies.CurrentUser
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // 取得當前登入者的 ID 或名稱
        public string? UserId => GetCurrentUserId();


        public string GetCurrentUserId()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        public string GetTenantId()
        {
            return "Wayne";
            //return _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }

    }
}
