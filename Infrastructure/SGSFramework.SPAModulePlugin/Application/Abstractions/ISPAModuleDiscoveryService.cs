using SGSFramework.SPAModulePlugin.Domain.Enums;
using SGSFramework.SPAModulePlugin.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SPAModulePlugin.Application.Abstractions
{
    public interface ISPAModuleDiscoveryService
    {
        /// <summary>
        /// 依據使用者權限與指定 SPA 框架取得可用的外掛模組清單
        /// </summary>
        Task<IReadOnlyList<SPAModuleManifest>> GetAuthorizedModulesAsync(Guid userId, SPAFrameworkType frameworkType, CancellationToken cancellationToken = default);
    }
}
