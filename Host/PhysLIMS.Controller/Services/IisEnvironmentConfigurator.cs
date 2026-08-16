using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using Configuration = Microsoft.Web.Administration.Configuration;
using ConfigurationElement = Microsoft.Web.Administration.ConfigurationElement;
using ConfigurationElementCollection = Microsoft.Web.Administration.ConfigurationElementCollection;
using ConfigurationSection = Microsoft.Web.Administration.ConfigurationSection;

namespace SGSFramework.ApiInfrastructure.Services
{
    /// <summary>
    /// IIS 應用程式集區環境變數自動化設定服務（需具備 Windows 管理員權限）
    /// </summary>
    /// <summary>
    /// IIS 應用程式集區環境變數與生產級效能參數自動化設定服務（需具備 Windows 管理員權限）
    /// </summary>
    public class IisEnvironmentConfigurator
    {
        private readonly ILogger<IisEnvironmentConfigurator> _logger;

        public IisEnvironmentConfigurator(ILogger<IisEnvironmentConfigurator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 配置指定 IIS Application Pool 的生產級參數與環境變數（若集區不存在則自動建立）
        /// </summary>
        public void ConfigureAppPoolEnvironmentVariable(string poolName, string envName, string envValue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
            ArgumentException.ThrowIfNullOrWhiteSpace(envName);
            ArgumentException.ThrowIfNullOrWhiteSpace(envValue);

            try
            {
                using var serverManager = new ServerManager();

                // 1. 取得或自動建立應用程式池
                var pool = serverManager.ApplicationPools[poolName];
                if (pool == null)
                {
                    _logger.LogInformation("找不到指定的 IIS Application Pool: {PoolName}，正在自動建立...", poolName);
                    pool = serverManager.ApplicationPools.Add(poolName);
                    serverManager.CommitChanges();

                    // 重新抓取剛建立的 Pool 執行個體
                    pool = serverManager.ApplicationPools[poolName]
                           ?? throw new InvalidOperationException($"無法自動建立 IIS Application Pool: {poolName}");
                }

                // 2. 套用生產級效能與抗延遲優化設定 (對應 PowerShell 生產標準)[cite: 3]
                _logger.LogInformation("正在套用 IIS 應用程式池 [{PoolName}] 生產級效能與穩定度參數...", poolName);

                // 設定啟動模式為 AlwaysRunning (防止閒置時進程被釋放)[cite: 3]
                pool.StartMode = StartMode.AlwaysRunning;

                // 關閉閒置逾時 (預設 20 分鐘會關閉進程，生產環境設為 0)[cite: 3]
                pool.ProcessModel.IdleTimeout = TimeSpan.Zero;

                // 關閉預設的隨機回收，改為指定每日離峰時間定時回收[cite: 3]
                pool.Recycling.PeriodicRestart.Time = TimeSpan.Zero;
                pool.Recycling.PeriodicRestart.Schedule.Clear();
                pool.Recycling.PeriodicRestart.Schedule.Add(new TimeSpan(4, 0, 0)); // 每日凌晨 4 點回收[cite: 3]

                // 3. 設定環境變數
                Configuration config = serverManager.GetApplicationHostConfiguration();
                ConfigurationSection applicationPoolsSection = config.GetSection("system.applicationHost/applicationPools");
                ConfigurationElementCollection applicationPoolsCollection = applicationPoolsSection.GetCollection();

                ConfigurationElement? addElement = FindElement(applicationPoolsCollection, "add", "name", poolName);
                if (addElement != null)
                {
                    ConfigurationElementCollection environmentVariablesCollection = addElement.GetCollection("environmentVariables");

                    ConfigurationElement? existingVar = FindElement(environmentVariablesCollection, "add", "name", envName);
                    if (existingVar != null)
                    {
                        existingVar["value"] = envValue;
                        _logger.LogInformation("更新 IIS Application Pool [{PoolName}] 環境變數: {EnvName} = {EnvValue}", poolName, envName, envValue);
                    }
                    else
                    {
                        ConfigurationElement addElement1 = environmentVariablesCollection.CreateElement("add");
                        addElement1["name"] = envName;
                        addElement1["value"] = envValue;
                        environmentVariablesCollection.Add(addElement1);
                        _logger.LogInformation("新增 IIS Application Pool [{PoolName}] 環境變數: {EnvName} = {EnvValue}", poolName, envName, envValue);
                    }
                }

                serverManager.CommitChanges();
                _logger.LogInformation("IIS 應用程式池 [{PoolName}] 生產級參數與環境變數配置完成。", poolName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "設定 IIS Application Pool 生產級參數失敗: {Message}", ex.Message);
                throw new InvalidOperationException($"無法設定 IIS 環境變數與效能參數: {ex.Message}", ex);
            }
        }

        private static ConfigurationElement? FindElement(ConfigurationElementCollection collection, string elementTagName, params string[] keyValues)
        {
            foreach (ConfigurationElement element in collection)
            {
                if (String.Equals(element.ElementTagName, elementTagName, StringComparison.OrdinalIgnoreCase))
                {
                    bool matches = true;
                    for (int i = 0; i < keyValues.Length; i += 2)
                    {
                        object o = element.GetAttributeValue(keyValues[i]);
                        string? value = o?.ToString();
                        if (!String.Equals(value, keyValues[i + 1], StringComparison.OrdinalIgnoreCase))
                        {
                            matches = false;
                            break;
                        }
                    }
                    if (matches)
                    {
                        return element;
                    }
                }
            }
            return null;
        }
    }
}
