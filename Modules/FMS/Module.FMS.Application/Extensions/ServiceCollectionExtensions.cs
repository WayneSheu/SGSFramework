using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SGS.Modules.FMS.Application.Auditing;
using SGS.Modules.FMS.Application.Imaging;
using SGS.Modules.FMS.Application.Security;
using SGS.Modules.FMS.Application.Services;
using SGS.Modules.FMS.Application.Validation;
using SGS.Modules.FMS.Application.Watermark;
using SGS.Modules.FMS.Infrastructure.Options;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.FMS.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Application 層：檔案簽章檢查、Magick.NET 影像處理、記憶體縮圖快取、信封加密（Data Protection 包裝資料金鑰）、
    /// 動態浮水印、非同步存取稽核與檔案管理服務。
    /// IFileManagementService 以 AddScoped 註冊，覆蓋 Core 預設的 NullFileManagementService（取最後一筆註冊）。
    /// </summary>
    public static IServiceCollection AddModuleFmsApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 儲存必加密：平台須支援 AES-GCM（Windows / 近代 Linux OpenSSL 皆支援），啟用時不支援即拒絕啟動
        if (configuration.GetValue<bool>($"{FileManagementOptions.SectionName}:{nameof(FileManagementOptions.Enabled)}") && !AesGcm.IsSupported)
            throw new PlatformNotSupportedException("[FMS] 執行平台不支援 AES-GCM，無法啟用檔案加密儲存。");

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.TryAddSingleton<IFileSignatureInspector, FileSignatureInspector>();
        services.TryAddSingleton<IImageProcessor, MagickImageProcessor>();
        services.TryAddSingleton<IImageCacheService, ImageCacheService>();
        services.TryAddSingleton<IWatermarkRenderer, WatermarkRenderer>();
        services.TryAddSingleton<IFileKeyProtector, FileKeyProtector>();
        services.TryAddSingleton(sp => new UploadBufferLimiter(
            sp.GetRequiredService<IOptions<FileManagementOptions>>().Value.Upload.MaxConcurrentBufferedUploads));

        // 存取稽核：有界佇列 + 背景批次寫入（Ledger APPEND_ONLY）
        services.TryAddSingleton<FileAccessLogQueue>();
        services.TryAddScoped<IFileAccessAuditor, FileAccessAuditor>();
        services.AddHostedService<FileAccessLogWriter>();
        services.TryAddScoped<IFileAccessLogQueryService, FileAccessLogQueryService>();
        services.TryAddScoped<IFileMaintenanceService, FileMaintenanceService>();

        services.AddScoped<FileManagementService>();
        services.AddScoped<IFileManagementService>(sp => sp.GetRequiredService<FileManagementService>());

        return services;
    }
}
