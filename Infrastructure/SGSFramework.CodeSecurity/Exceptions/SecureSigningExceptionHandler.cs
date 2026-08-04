using Microsoft.Extensions.Logging;
using SGSFramework.CodeSecurity.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Exceptions
{
    public class SecureSigningExceptionHandler
    {
        private readonly ILogger<SecureSigningExceptionHandler> _logger;

        public SecureSigningExceptionHandler(ILogger<SecureSigningExceptionHandler> logger)
            => _logger = logger;

        public SigningResult HandleSigningError(Exception ex, string artifactPath)
        {
            // 1. 隱藏技術細節：僅記錄到受限的內部日誌系統
            _logger.LogError("Signing critical failure: {Path}. Reason: {Msg}", artifactPath, ex.Message);

            // 2. 使用安全狀態封裝：對外僅回傳模糊的失敗標記
            // 嚴禁將 Exception.ToString() 或過多的技術細節作為錯誤訊息回傳
            return SigningResult.CreateFailure(artifactPath, "Signing operation could not be completed securely.");
        }
    }
}
