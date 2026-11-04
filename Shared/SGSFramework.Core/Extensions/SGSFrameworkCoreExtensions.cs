using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Core.FileManagement;
using SGSFramework.Core.FileStorages;
using SGSFramework.Core.Generators;
using SGSFramework.Core.Identiies.CurrentUser;
using SGSFramework.Core.Identiies.Tenants;
using SGSFramework.Core.Identities.Services;
using SGSFramework.Core.Services;
using ZiggyCreatures.Caching.Fusion;

namespace SGSFramework.Core.Extensions
{
    public static class SGSFrameworkCoreExtensions
    {
        // 注入 SGSFramework.Core 的服務 
        public static void AddSGSFrameworkCore(this WebApplicationBuilder builder)
        {

            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            builder.Services.AddSingleton<IGuidGenerator, SystemGuidGenerator>();

            // MemoryCache
            builder.Services.AddMemoryCache();
            // 註冊 FusionCache 並設定全域預設選項
            // 🟢 FusionCache 2.x 建議的標準 Builder 模式寫法
            builder.Services.AddFusionCache()
                .TryWithAutoSetup()
                .WithDefaultEntryOptions(new FusionCacheEntryOptions
                {
                    Duration = TimeSpan.FromMinutes(10),
                    IsFailSafeEnabled = true,
                    FailSafeMaxDuration = TimeSpan.FromHours(1)
                });


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

            // 檔案管理：SGS.Modules.FMS 外掛載入時以 AddScoped 覆蓋；未安裝時回應 503（模組未安裝）
            builder.Services.TryAddScoped<IFileManagementService, NullFileManagementService>();

            // 日誌儲存：SGS.Modules.LOG 外掛載入時以 AddSingleton 覆蓋；未安裝時落檔緩衝（Log_Buffer），恢復後回補
            builder.Services.TryAddSingleton<SGSFramework.Core.Logging.ISystemLogStore, SGSFramework.Core.Logging.NullSystemLogStore>();
            builder.Services.TryAddSingleton<SGSFramework.Core.Logging.ISecurityLogStore, SGSFramework.Core.Logging.NullSecurityLogStore>();


        }
    }
}