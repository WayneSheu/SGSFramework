using Microsoft.Extensions.Logging;
using SGSFramework.DataProtection;
using SGSFramework.DataProtection.Abstractions;
using System;
using System.Collections.Generic;
using System.Security;
using System.Text;

namespace SGSFramework.DataProtection.Gateways
{
    //public class DiApiGateway
    //{
    //    private readonly IPasswordlessAuthenticator _auth;
    //    private readonly IEncryptionService _crypto;

    //    public DiApiGateway(IPasswordlessAuthenticator auth, IEncryptionService crypto)
    //    {
    //        _auth = auth;
    //        _crypto = crypto;
    //    }

    //    //public async Task<string> ProcessSecureRequest(SecureRequest request)
    //    //{
    //    //    // 1. 驗證無密碼身分
    //    //    var isValid = await _auth.ValidateAssertionAsync(request.CredId, request.Data, request.Sig);
    //    //    if (!isValid) throw new UnauthorizedAccessException("Identity verification failed.");

    //    //    // 2. 使用 HMS 解密處理敏感資料
    //    //    return await _crypto.DecryptAsync(request.EncryptedPayload, "prod-data-key");
    //    //}
    //}

    public class DiApi : IDiApi
    {
        private readonly EncryptionContext _encryptionContext;
        private readonly ILogger<DiApi> _logger;

        public DiApi(EncryptionContext encryptionContext, ILogger<DiApi> logger)
        {
            _encryptionContext = encryptionContext;
            _logger = logger;
        }

        /// <summary>
        /// 統一入口：由 DIAPI 呼叫 EncryptionContext 執行加密
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="strategyName"></param>
        /// <returns></returns>
        /// <exception cref="SecurityException"></exception>
        public async Task<string> SecureProcessAsync(string payload, string strategyName)
        {
            _logger.LogInformation("DIAPI: Processing secure request with strategy {Strategy}", strategyName);

            try
            {
                // 統一入口：由 DIAPI 呼叫 EncryptionContext 執行加密
                return await _encryptionContext.ExecuteEncryptionAsync(payload, strategyName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DIAPI: Critical failure during secure processing.");
                throw new SecurityException("Secure processing failed at DIAPI level.", ex);
            }
        }

        public async Task<string> SecureDecryptAsync(string cipherText, string strategyName)
        {
            _logger.LogInformation("DIAPI: Processing decryption request with strategy {Strategy}", strategyName);

            try
            {
                // 建議：若解密邏輯同樣受 EncryptionContext 管理，請調用它
                // 若解密需直接透過 HmsClient，則建議在 EncryptionContext 中封裝該邏輯
                return await _encryptionContext.ExecuteDecryptionAsync(cipherText, strategyName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DIAPI: Critical failure during decryption processing.");
                throw new SecurityException("Decryption failed at DIAPI level.", ex);
            }
        }
    }
}
