using SGSFramework.DataProtection.Abstractions;
using System;
using System.Collections.Generic;
using System.Security;
using System.Text;

namespace SGSFramework.DataProtection.Services
{
    /// <summary>
    /// 實作基於硬體安全模組(HMS)的加密服務
    /// </summary>
    public class HmsEncryptionService : IEncryptionService
    {
        private readonly IHmsClient _hmsClient;

        public HmsEncryptionService(IHmsClient hmsClient) => _hmsClient = hmsClient ?? throw new ArgumentNullException(nameof(hmsClient));

        public async Task<string> EncryptAsync(string plainText, string keyAlias)
        {
            try
            {
                // 呼叫外部硬體安全模組進行運算，避免密鑰落地
                return await _hmsClient.EncryptWithKeyAsync(plainText, keyAlias);
            }
            catch (Exception ex)
            {
                // 記錄安全性異常
                throw new SecurityException("HMS encryption operation failed.", ex);
            }
        }

        public async Task<string> DecryptAsync(string cipherText, string keyAlias)
        {
            try
            {
                return await _hmsClient.DecryptWithKeyAsync(cipherText, keyAlias);
            }
            catch (Exception ex)
            {
                throw new SecurityException("HMS decryption operation failed.", ex);
            }
        }
    }
}
