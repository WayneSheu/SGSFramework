using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Abstractions
{
    public interface ISigningStrategy
    {
        // 定義各類簽署策略的執行合約
        Task<SigningResult> SignAsync(string artifactPath, string? timestampUrl);
    }
}
