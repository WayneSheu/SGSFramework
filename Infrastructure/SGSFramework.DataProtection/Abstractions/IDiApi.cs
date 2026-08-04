using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.DataProtection.Abstractions
{
    public interface IDiApi
    {
        /// <summary>
        /// 執行加密處理
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="strategyName"></param>
        /// <returns></returns>
        Task<string> SecureProcessAsync(string payload, string strategyName);

        /// <summary>
        /// 執行解密處理
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="strategyName"></param>
        /// <param name="keyAlias"></param>
        /// <returns></returns>
        Task<string> SecureDecryptAsync(string cipherText, string strategyName);
    }
}
