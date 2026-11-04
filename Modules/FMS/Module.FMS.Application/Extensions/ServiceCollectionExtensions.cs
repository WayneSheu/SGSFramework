using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGS.Modules.FMS.Application.HostedServices;
using SGS.Modules.FMS.Application.Imaging;
using SGS.Modules.FMS.Application.Services;
using SGS.Modules.FMS.Application.Validation;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.FMS.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Application 層：檔案簽章檢查、ImageSharp 處理、縮圖快取與 LRU 清理、檔案管理服務。
    /// IFileManagementService 以 AddScoped 註冊，覆蓋 Core 預設的 NullFileManagementService（取最後一筆註冊）。
    /// </summary>
    public static IServiceCollection AddModuleFmsApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IFileSignatureInspector, FileSignatureInspector>();
        services.TryAddSingleton<IImageProcessor, ImageSharpImageProcessor>();
        services.TryAddSingleton<IImageCacheService, ImageCacheService>();
        services.AddHostedService<ImageCacheLruCleanupService>();
        services.AddScoped<IFileManagementService, FileManagementService>();

        return services;
    }
}
