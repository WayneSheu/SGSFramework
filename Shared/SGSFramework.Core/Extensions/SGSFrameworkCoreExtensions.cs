using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.FileStorages;
using SGSFramework.Core.Identiies.CurrentUser;
using SGSFramework.Core.Identiies.Tenants;
using SGSFramework.Core.Services;

namespace SGSFramework.Core.Extensions
{


    public static class SGSFrameworkCoreExtensions
    {
        // 注入 SGSFramework.Core 的服務 
        public static void AddSGSFrameworkCore(this WebApplicationBuilder builder)
        {

            // MemoryCache
            builder.Services.AddMemoryCache(); // 務必加入此行
   

            //註冊 HttpContextAccessor 以便在服務中存取 HttpContext (例如：取得使用者資訊、請求相關資料等)
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            builder.Services.AddScoped<ITenantService, NullTenantService>();

            #region IO
            // 設定 Kestrel 伺服器限制 (針對檔案大小)
            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 2147483648; // 2GB
            });

            // 2. 設定 Form 傳輸限制 (如果你是用 Form 上傳)
            //解除 HTTP Form / Multipart 檔案上傳限制 (處理大檔案匯入)
            builder.Services.Configure<FormOptions>(x =>
            {
                x.ValueLengthLimit = int.MaxValue;
                x.MultipartBodyLengthLimit = int.MaxValue; // 支援大檔案上傳
                x.MultipartBoundaryLengthLimit = int.MaxValue;
                x.MultipartHeadersCountLimit = int.MaxValue;
                x.MultipartHeadersLengthLimit = int.MaxValue;
            });
            // 註冊檔案儲存服務 (LocalFileStorageHelper)
            builder.Services.AddScoped(typeof(IFileStorageHelper<>), typeof(LocalFileStorageHelper<>));
            #endregion

            // 使用 TryAddScoped 註冊 Fallback 的 Null 服務
            // 若 SGS.Modules.ORG 在後續階段有註冊真實實作，將會自動覆蓋此預設項目
            builder.Services.TryAddScoped<IOrganizationIntegrationService, NullOrganizationIntegrationService>();
           
          

        }
    }
}