using SGSFramework.CodeSecurity.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Strategies
{
    // 策略 A：離線/本地機台簽署 (Windows 11 TPM)
    public class LocalTpmSigningStrategy : ISigningStrategy
    {
        public async Task<SigningResult> SignAsync(string artifactPath, string? timestampUrl)
        {
            // 實作呼叫 Windows SignTool 並綁定 TPM 的邏輯
            return await Task.FromResult(SigningResult.CreateSuccess(artifactPath, "TPM-KEY-001"));
        }
    }
}
