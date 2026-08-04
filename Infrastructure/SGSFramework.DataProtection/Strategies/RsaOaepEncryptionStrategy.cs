using SGSFramework.DataProtection.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.DataProtection.Strategies
{
    /// <summary>
    /// RSA-OAEP 加密策略 (用於金鑰交換或小量資料)
    /// </summary>
    public class RsaOaepEncryptionStrategy : IEncryptionStrategy
    {
        public string StrategyName => "RSA-OAEP";

        public async Task<string> EncryptAsync(string plainText, string keyAlias)
        {
            return await Task.FromResult("EncryptedData_RSA");
        }

        public async Task<string> DecryptAsync(string cipherText, string keyAlias)
        {
            return await Task.FromResult("DecryptedData_RSA");
        }
    }
}
