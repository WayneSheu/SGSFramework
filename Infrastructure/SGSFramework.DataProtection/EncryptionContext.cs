using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGSFramework.DataProtection.Abstractions;
using SGSFramework.DataProtection.Configurations;
using System;
using System.Collections.Generic;
using System.Security;
using System.Text;

namespace SGSFramework.DataProtection
{
    /// <summary>
    /// 加密環境上下文，負責執行策略選取
    /// EncryptionContext 註冊為 Singleton (單例)，
    /// 但它依賴的 IEncryptionStrategy 卻被註冊為 Scoped (範圍)
    /// </summary>
    public class EncryptionContext
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly EncryptionOptions _options;

        public EncryptionContext(IServiceScopeFactory scopeFactory,
            IOptions<EncryptionOptions> options)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
        }

        /// <summary>
        /// 執行加密操作，根據指定的策略名稱選擇對應的加密策略
        /// </summary>
        /// <param name="data"></param>
        /// <param name="strategyName"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="SecurityException"></exception>
        public async Task<string> ExecuteEncryptionAsync(string data, string? strategyName = null)
        {
            // 若未指定策略，則使用 DefaultStrategy
            var targetName = strategyName ?? _options.DefaultStrategy;
            // 每次執行時手動建立一個 Scope，確保策略能正確釋放
            using (var scope = _scopeFactory.CreateScope())
            {
                var strategies = scope.ServiceProvider.GetServices<IEncryptionStrategy>();
                var strategy = strategies.FirstOrDefault(s => s.StrategyName == strategyName)
                               ?? throw new NotSupportedException($"Strategy {strategyName} not found.");
                // 從配置中獲取該策略對應的金鑰別名
                var config = _options.Strategies.GetValueOrDefault(targetName);
                var keyAlias = config?.KeyAlias ?? "default-key";

                try
                {
                    return await strategy.EncryptAsync(data, keyAlias);
                }
                catch (Exception ex)
                {
                    throw new SecurityException($"Execution error for strategy {targetName}.", ex);
                }

            }
        }

        /// <summary>
        /// 執行解密操作，根據指定的策略名稱選擇對應的解密策略
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="strategyName"></param>
        /// <param name="keyAlias"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="SecurityException"></exception>
        public async Task<string> ExecuteDecryptionAsync(string cipherText, string strategyName)
        {
            // 若未指定策略，則使用 DefaultStrategy
            var targetName = strategyName ?? _options.DefaultStrategy;
            // 每次執行時手動建立一個 Scope，確保策略能正確釋放
            using (var scope = _scopeFactory.CreateScope())
            {
                var strategies = scope.ServiceProvider.GetServices<IEncryptionStrategy>();
                var strategy = strategies.FirstOrDefault(s => s.StrategyName == strategyName)
                               ?? throw new NotSupportedException($"Strategy {strategyName} not found.");
                // 從配置中獲取該策略對應的金鑰別名
                var config = _options.Strategies.GetValueOrDefault(targetName);
                var keyAlias = config?.KeyAlias ?? "default-key";
                try
                {
                    // 2. 執行策略中的解密邏輯
                    return await strategy.DecryptAsync(cipherText, keyAlias);
                }
                catch (Exception ex)
                {
                    // 3. 封裝異常以符合安全性標準
                    throw new SecurityException($"Decryption execution error for strategy {strategyName}.", ex);
                }
            }
        }
    }
}
