using SGSFramework.SPAModulePlugin.Domain.Enums;
using SGSFramework.SPAModulePlugin.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SPAModulePlugin.Domain.Repositories
{
    public interface ISPAModuleRepository
    {
        /// <summary>
        /// 掃描實體目錄並取得所有通過安全驗證的 SPA 模組 Manifest
        /// </summary>
        Task<IReadOnlyList<SPAModuleManifest>> GetValidModulesAsync(SPAFrameworkType frameworkType, CancellationToken cancellationToken = default);
    }
}
