using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Services.Verifications
{
    /// <summary>
    /// HashVerificationService 提供基於 SHA256 的檔案哈希驗證功能，用於非 Windows 環境下的插件安全性檢查。
    /// </summary>
    public class HashVerificationService : ISecurityService
    {
        private readonly Dictionary<string, string> _allowedHashes;

        public HashVerificationService(Dictionary<string, string> allowedHashes)
        {
            _allowedHashes = allowedHashes;
        }

        public async Task<bool> VerifyPluginAsync(string filePath)
        {
            var hash = await CalculateFileHashAsync(filePath);
            return _allowedHashes.TryGetValue(Path.GetFileName(filePath), out var expected) && expected == hash;
        }

        private static async Task<string> CalculateFileHashAsync(string filePath)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var bytes = await sha.ComputeHashAsync(stream);
            return Convert.ToHexString(bytes);
        }
    }
}
