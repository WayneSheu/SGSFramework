using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Abstractions
{
    /// <summary>
    /// 基礎設施層簽署供應器 
    /// </summary>
    public interface IInfrastructureSigner
    {
        // 執行簽署並回傳作業結果
        Task<SigningResult> ExecuteAsync(string filePath, SigningStage stage);
    }
}
