using SGSFramework.DataProtection.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.DataProtection.Strategies
{
    /// <summary>
    /// AES-256-GCM 加密策略 (用於高安全性需求)
    /// </summary>
    public class AesGcmEncryptionStrategy : IEncryptionStrategy
    {
        private readonly IHmsClient _hmsClient; // 注入 HMS 客戶端
        public string StrategyName => "AES-256-GCM";

        public AesGcmEncryptionStrategy(IHmsClient hmsClient)
        {
            _hmsClient = hmsClient;
        }

        public async Task<string> EncryptAsync(string plainText, string keyAlias)
        {
            // 呼叫實際的 HMS 服務而非假資料
            return await _hmsClient.EncryptWithKeyAsync(plainText, keyAlias);
        }

        public async Task<string> DecryptAsync(string cipherText, string keyAlias)
        {
            // 呼叫實際的 HMS 解密方法
            return await _hmsClient.DecryptWithKeyAsync(cipherText, keyAlias);
        }
    }
}
