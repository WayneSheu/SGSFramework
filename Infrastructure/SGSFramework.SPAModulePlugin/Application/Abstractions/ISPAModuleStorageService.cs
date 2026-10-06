// Path: src/SGSFramework/Infrastructure/SGSFramework.SPAModulePlugin/Application/Abstractions/ISPAModuleStorageService.cs
namespace SGSFramework.SPAModulePlugin.Application.Abstractions;

using Microsoft.AspNetCore.Http;
using SGSFramework.SPAModulePlugin.Application.DTOs;
using SGSFramework.SPAModulePlugin.Domain.Enums;

/// <summary>
/// SPA 模組實體檔案與目錄維護服務介面
/// </summary>
public interface ISPAModuleStorageService
{
    /// <summary>
    /// 解壓縮並部署 SPA 模組套件至指定的 wwwroot 子目錄
    /// </summary>
    Task<SPAModuleUploadResponseDto> DeployModulePackageAsync(
        SPAFrameworkType frameworkType,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 實體刪除特定 SPA 模組目錄
    /// </summary>
    Task<bool> DeleteModuleDirectoryAsync(
        string moduleName,
        SPAFrameworkType frameworkType,
        CancellationToken cancellationToken = default);
}