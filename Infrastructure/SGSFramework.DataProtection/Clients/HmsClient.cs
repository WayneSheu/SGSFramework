using Enterprise.HttpSdk.Clients;
using Microsoft.Extensions.Logging;
using SGSFramework.DataProtection.Abstractions;

namespace SGSFramework.DataProtection.Clients
{

    ///// <summary>
    ///// 硬體安全模組/金鑰管理服務
    ///// 生產級 HMS 客戶端實作，採用策略注入與配置管理
    ///// </summary>
    public class HmsClient : BaseApiClient, IHmsClient
    {
        public HmsClient(HttpClient httpClient, ILogger<HmsClient> logger)
            : base(httpClient, logger) { }

        public async Task<string> EncryptWithKeyAsync(string plainText, string keyAlias)
        {
            var payload = new { plainText, keyAlias };
            var result = await PostAsync<object, string>("/v1/encrypt", payload);
            return result ?? throw new InvalidOperationException("Encryption returned null.");
        }

        public async Task<string> DecryptWithKeyAsync(string cipherText, string keyAlias)
        {
            var payload = new { cipherText, keyAlias };
            var result = await PostAsync<object, string>("/v1/decrypt", payload);
            return result ?? throw new InvalidOperationException("Decryption returned null.");
        }

        public async Task<KeyStatus> GetKeyStatusAsync(string keyAlias)
        {
            var result = await GetAsync<KeyStatus>($"/v1/status/{keyAlias}");
            return result;
        }
    }
}
