using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Abstractions
{
    /// <summary>
    /// 模組初始化器介面，定義註冊與非同步組態設定行為
    /// </summary>
    public interface IModuleInitializer
    {
        /// <summary>
        /// 模組識別名稱
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// 註冊模組內部的相依性服務
        /// </summary>
        IServiceCollection  RegisterDependencies(IServiceCollection services, IConfiguration configuration);

        /// <summary>
        /// 執行應用程式啟動時的非同步組態設定（包含資料庫遷移）
        /// </summary>
        Task OnApplicationConfigureAsync(IApplicationBuilder app);

        // 新增此方法以支援監控
        ModuleHealthReport GetHealthStatus();

    }
}
