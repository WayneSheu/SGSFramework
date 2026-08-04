using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.DataProtection.Abstractions
{
    /// <summary>
    /// 定義資料加密服務規範
    /// </summary>
    public interface IEncryptionService
    {
        Task<string> EncryptAsync(string plainText, string keyAlias);
        Task<string> DecryptAsync(string cipherText, string keyAlias);
    }
}
