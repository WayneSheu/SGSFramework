using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SGSFramework.Core.Helpers
{
    public static class HashHelper
    {
        /// <summary>
        /// 內部核心方法：計算 Refresh Token 的 SHA-256 單向安全雜湊值
        /// </summary>
        /// <param name="input">明文隨機 Token 字串</param>
        /// <returns>固定 64 碼之十六進位大寫字串</returns>
        public static string ComputeHash(string input)
        {
            // 🚀 採用強型別 Null 檢查，符合 .NET 10 嚴謹規範
            ArgumentException.ThrowIfNullOrWhiteSpace(input);

            // 🚀 使用 .NET 10 高效且不重複配置記憶體的內置加密方法
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));

            // 🚀 Convert.ToHexString 預設即輸出大寫字串，極速且符合資料庫索引高效率比對
            return Convert.ToHexString(bytes);
        }
    }
}
