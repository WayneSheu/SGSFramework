using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.DataProtection.Abstractions
{
    /// <summary>
    /// 加密演算法策略介面
    /// </summary>
    public interface IEncryptionStrategy
    {
        /// <summary>
        /// 取得加密策略名稱
        /// </summary>
        string StrategyName { get; }
        /// <summary>
        /// 執行加密處理
        /// </summary>
        /// <param name="plainText"></param>
        /// <param name="keyAlias"></param>
        /// <returns></returns>
        Task<string> EncryptAsync(string plainText, string keyAlias);
        /// <summary>
        /// 執行解密處理
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="keyAlias"></param>
        /// <returns></returns>
        Task<string> DecryptAsync(string cipherText, string keyAlias);

    }
}
