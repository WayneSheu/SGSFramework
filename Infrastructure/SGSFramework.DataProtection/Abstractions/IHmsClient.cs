using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.DataProtection.Abstractions
{
    /// <summary>
    /// 定義與 HMS (Hardware Security Module) 通訊的介面
    /// </summary>
    public interface IHmsClient
    {
        /// <summary>
        /// 使用指定的金鑰別名進行加密
        /// </summary>
        Task<string> EncryptWithKeyAsync(string plainText, string keyAlias);

        /// <summary>
        /// 使用指定的金鑰別名進行解密
        /// </summary>
        Task<string> DecryptWithKeyAsync(string cipherText, string keyAlias);

        /// <summary>
        /// 取得金鑰的運作狀態或憑證資訊
        /// </summary>
        Task<KeyStatus> GetKeyStatusAsync(string keyAlias);
    }
}
