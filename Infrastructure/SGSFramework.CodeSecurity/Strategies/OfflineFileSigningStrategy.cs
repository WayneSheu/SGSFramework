using SGSFramework.CodeSecurity.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Strategies
{
    // 策略 B：離線檔案簽署 (Air-gapped 環境模擬)
    public class OfflineFileSigningStrategy : ISigningStrategy
    {
        public async Task<SigningResult> SignAsync(string artifactPath, string? timestampUrl)
        {
            // 實作讀取加密儲存體中的簽署憑證檔案
            return await Task.FromResult(SigningResult.CreateSuccess(artifactPath, "OFFLINE-KEY-999"));
        }
    }
}
