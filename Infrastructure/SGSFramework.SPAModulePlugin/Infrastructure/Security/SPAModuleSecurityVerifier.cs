namespace SGSFramework.SPAModulePlugin.Infrastructure.Security;

using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SGSFramework.SPAModulePlugin.Domain.Abstractions;

public sealed class SPAModuleSecurityVerifier : ISPAModuleSecurityVerifier
{
    private readonly ILogger<SPAModuleSecurityVerifier> _logger;

    public SPAModuleSecurityVerifier(ILogger<SPAModuleSecurityVerifier> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool VerifyModuleIntegrity(string filePath, string expectedHash)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentException.ThrowIfNullOrEmpty(expectedHash);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("SPA 模組實體檔案不存在：{FilePath}", filePath);
            return false;
        }

        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var computedHash = sha256.ComputeHash(stream);
            var actualHashString = Convert.ToHexString(computedHash);

            bool isValid = string.Equals(actualHashString, expectedHash, StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                _logger.LogError("⚠️ SPA 模組檔案安全校驗失敗，疑似遭受篡改！檔案：{FilePath}，期望雜湊：{Expected}，實際雜湊：{Actual}",
                    filePath, expectedHash, actualHashString);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢驗 SPA 模組雜湊時發生例外狀況，檔案：{FilePath}", filePath);
            return false;
        }
    }
}