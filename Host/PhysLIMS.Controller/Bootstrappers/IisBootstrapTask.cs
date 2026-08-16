using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGSFramework.ApiInfrastructure.Services;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace SGSFramework.ApiInfrastructure.Bootstrappers
{
    /// <summary>
    /// 系統初始化階段 (Step 1) 的 IIS 自動化配置工作
    /// </summary>
    public static class IisBootstrapTask
    {
        public static void Execute(IConfiguration configuration, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(logger);

            // 檢查當前程序是否具備 Windows 管理員權限
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                logger.LogWarning(">>> [安全防護] 當前執行程序未具備 Windows 系統管理員權限，無法修改 IIS 設定，已自動略過 IIS 環境變數配置步驟。");
                return;
            }

            try
            {
                logger.LogInformation("Step 1.1: 開始執行 IIS 應用程式集區環境變數檢查與配置...");

                var configurator = new IisEnvironmentConfigurator(
                    LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<IisEnvironmentConfigurator>()
                );

                string poolName = configuration["IIS:AppPoolName"] ?? "PhysLIMS.API";

                configurator.ConfigureAppPoolEnvironmentVariable(poolName, "ASPNETCORE_ENVIRONMENT", "Production");
                configurator.ConfigureAppPoolEnvironmentVariable(poolName, "PhysLIMS_LedgerEnabled", "true");

                logger.LogInformation("Step 1.1: IIS 應用程式集區環境變數配置完成。");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Step 1.1 執行失敗: 無法完成 IIS 環境配置。");
                throw;
            }
        }
    }
}
