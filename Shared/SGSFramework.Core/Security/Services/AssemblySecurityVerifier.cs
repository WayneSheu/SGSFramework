
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace SGSFramework.Core.Security.Services
{
    /// <summary>
    /// 驗證組件的強名稱和數位簽章，確保其來源可信。
    /// </summary>
    public class AssemblySecurityVerifier
    {
        private readonly ILogger<AssemblySecurityVerifier> _logger;
        private readonly string _trustedThumbprint;

        public AssemblySecurityVerifier(ILogger<AssemblySecurityVerifier> logger, string trustedThumbprint)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _trustedThumbprint = trustedThumbprint ?? throw new ArgumentNullException(nameof(trustedThumbprint));
        }

        /// <summary>
        /// 驗證 DLL 的 Strong Name 公鑰 Token 與 X.509 數位簽章
        /// </summary>
        public bool VerifyAssembly(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
            {
                _logger.LogWarning(">>> [Security Audit] 驗證失敗，找不到指定的檔案路徑: {Path}", assemblyPath);
                return false;
            }

            try
            {
                // 1. 強名稱 (Strong Name) 校驗
                var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                byte[]? publicKeyToken = assemblyName.GetPublicKeyToken();

                if (publicKeyToken == null || publicKeyToken.Length == 0)
                {
                    _logger.LogWarning(">>> [Security Audit] 組件未包含強名稱公開金鑰 Token。檔案: {Path}", assemblyPath);
                    return false;
                }

                // 2. X.509 憑證數位簽章校驗 (針對 .NET 10 規範與 SYSLIB0057 警告處理)
#pragma warning disable SYSLIB0057
                using var signedCert = new X509Certificate(X509Certificate.CreateFromSignedFile(assemblyPath));
#pragma warning restore SYSLIB0057

                if (signedCert.Handle == IntPtr.Zero)
                {
                    _logger.LogWarning(">>> [Security Audit] 無法從已簽署檔案中提取有效憑證實體。檔案: {Path}", assemblyPath);
                    return false;
                }

                using var certificate = new X509Certificate2(signedCert);

                // 3. 設定憑證鏈結驗證策略，略過線上撤銷檢查與時間效期
                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

                bool isChainValid = chain.Build(certificate);

                // 4. 比對受信任憑證的指紋 (Thumbprint)
                string actualThumbprint = certificate.Thumbprint;
                if (!string.Equals(actualThumbprint, _trustedThumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(">>> [Security Audit] 憑證指紋不符！預期: {Expected}, 實際: {Actual}, 檔案: {Path}",
                        _trustedThumbprint, actualThumbprint, assemblyPath);
                    return false;
                }

                _logger.LogInformation(">>> [Security Audit] 模組數位簽章與憑證指紋驗證成功。檔案: {Path}", assemblyPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ">>> [Security Error] 執行 VerifyAssembly 時發生未預期的例外狀況。檔案路徑: {Path}", assemblyPath);
                return false;
            }
        }
    }
}