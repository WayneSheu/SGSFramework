// Path: src/SGSFramework/Infrastructure/SGSFramework.SPAModulePlugin/Application/Abstractions/ISPAModuleDiscoveryService.cs
namespace SGSFramework.SPAModulePlugin.Application.Abstractions;

using SGSFramework.SPAModulePlugin.Domain.Enums;
using SGSFramework.SPAModulePlugin.Domain.ValueObjects;

/// <summary>
/// SPA 模組探索與授權清單解析服務介面
/// </summary>
public interface ISPAModuleDiscoveryService
{
    /// <summary>
    /// 依據使用者身分與前端框架類型，取得經過權限篩選的 SPA 模組 Manifest 清單
    /// </summary>
    /// <param name="userId">使用者識別碼</param>
    /// <param name="frameworkType">前端框架類型 (Vue3 / BlazorWasm)</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>授權 SPA 模組 Manifest 列表</returns>
    Task<IReadOnlyList<SPAModuleManifest>> GetAuthorizedModulesAsync(
        Guid userId,
        SPAFrameworkType frameworkType,
        CancellationToken cancellationToken = default);
}