using Microsoft.Extensions.Logging;
using SGSFramework.DataProtection.Abstractions;
using System;
using System.Collections.Generic;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace SGSFramework.DataProtection.Strategies
{

    /// <summary>
    /// Windows Data Protection API (DPAPI) 加密策略
    /// </summary>
    public class DpapiEncryptionStrategy : IEncryptionStrategy
    {
        private readonly ILogger<DpapiEncryptionStrategy> _logger;
        public string StrategyName => "Windows-DPAPI";

        public DpapiEncryptionStrategy(ILogger<DpapiEncryptionStrategy> logger)
        {
            _logger = logger;
        }

        public async Task<string> EncryptAsync(string plainText, string keyAlias)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                // 使用 DataProtectionScope.LocalMachine 確保服務帳戶皆可解密
                byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine);
                return Convert.ToBase64String(encrypted);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "DPAPI 加密失敗。可能原因：金鑰存取權限不足或系統加密服務異常。");
                throw new SecurityException("無法完成資料保護作業。", ex);
            }
        }

        public async Task<string> DecryptAsync(string cipherText, string keyAlias)
        {
            try
            {
                byte[] data = Convert.FromBase64String(cipherText);
                byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "DPAPI 解密失敗。可能原因：密文損毀或非本機加密的資料。");
                throw new SecurityException("無法還原受保護的資料。", ex);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "解密輸入格式錯誤，非有效的 Base64 字串。");
                throw new ArgumentException("無效的密文格式。", ex);
            }
        }
    }
}
