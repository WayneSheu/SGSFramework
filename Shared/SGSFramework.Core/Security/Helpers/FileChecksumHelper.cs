using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SGSFramework.Core.Security.Helpers
{
    public static class FileChecksumHelper
    {
        /// <summary>
        /// 計算指定檔案的 SHA256 雜湊值（回傳小寫十六進位字串）
        /// </summary>
        public static string ComputeSha256Checksum(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("找不到指定的檔案。", filePath);
            }

            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();

            byte[] hashBytes = sha256.ComputeHash(stream);

            // 轉換為十六進位字串
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
