using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Enterprise.HttpSdk.Interfaces;
using Enterprise.HttpSdk.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Enterprise.HttpSdk.Providers
{
    /// <summary>
    /// 系統帳號權杖服務實作（Singleton）：每個帳號設定各自快取一張權杖，以 SemaphoreSlim 避免併發時重複換發。
    /// </summary>
    public sealed class SystemAccountTokenService : ISystemAccountTokenService
    {
        /// <summary>換發權杖使用的具名 HttpClient（不掛載授權 Handler，避免遞迴）</summary>
        public const string TokenHttpClientName = "Enterprise.SystemAccountToken";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<SystemAccountClientOptions> _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SystemAccountTokenService> _logger;
        private readonly ConcurrentDictionary<string, CachedToken> _tokens = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

        public SystemAccountTokenService(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<SystemAccountClientOptions> options,
            TimeProvider timeProvider,
            ILogger<SystemAccountTokenService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> GetSystemTokenAsync(string accountName, bool forceRefresh, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(accountName);

            if (!forceRefresh && TryGetValid(accountName, out var cached))
            {
                return cached;
            }

            var gate = _locks.GetOrAdd(accountName, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // 等待期間其他執行緒可能已換發完成；強制換新時只沿用「等待期間」新換得的權杖
                if (_tokens.TryGetValue(accountName, out var existing) && IsValid(existing) &&
                    (!forceRefresh || existing.IssuedAt > _timeProvider.GetUtcNow().AddSeconds(-5)))
                {
                    return existing.AccessToken;
                }

                var token = await RequestTokenAsync(accountName, cancellationToken).ConfigureAwait(false);
                _tokens[accountName] = token;
                return token.AccessToken;
            }
            finally
            {
                gate.Release();
            }
        }

        private bool TryGetValid(string accountName, out string accessToken)
        {
            if (_tokens.TryGetValue(accountName, out var token) && IsValid(token))
            {
                accessToken = token.AccessToken;
                return true;
            }

            accessToken = string.Empty;
            return false;
        }

        private bool IsValid(CachedToken token) => token.RefreshAt > _timeProvider.GetUtcNow();

        private async Task<CachedToken> RequestTokenAsync(string accountName, CancellationToken cancellationToken)
        {
            var options = _options.Get(accountName);
            if (options.TokenEndpoint is null || string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
            {
                throw new InvalidOperationException($"系統帳號設定 [{accountName}] 缺少 TokenEndpoint、ClientId 或 ClientSecret。");
            }

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret
            };
            if (!string.IsNullOrWhiteSpace(options.Scope))
            {
                form["scope"] = options.Scope;
            }

            var client = _httpClientFactory.CreateClient(TokenHttpClientName);
            using var content = new FormUrlEncodedContent(form);
            using var response = await client.PostAsync(options.TokenEndpoint, content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError("系統帳號 [{AccountName}]（client_id：{ClientId}）換發權杖失敗，HTTP {StatusCode}：{Body}",
                    accountName, options.ClientId, (int)response.StatusCode, body);
                throw new HttpRequestException($"系統帳號 [{accountName}] 換發權杖失敗（HTTP {(int)response.StatusCode}）。", null, response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false);
            if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
            {
                throw new HttpRequestException($"系統帳號 [{accountName}] 權杖回應格式不正確。");
            }

            var now = _timeProvider.GetUtcNow();
            var lifetime = TimeSpan.FromSeconds(Math.Max(payload.ExpiresIn, 1));
            var buffer = options.RefreshBeforeExpiry < lifetime / 2 ? options.RefreshBeforeExpiry : lifetime / 2;

            _logger.LogInformation("系統帳號 [{AccountName}] 已換發權杖，有效 {Seconds} 秒，Scope：{Scope}", accountName, payload.ExpiresIn, payload.Scope);
            return new CachedToken(payload.AccessToken, now, now + lifetime - buffer);
        }

        private sealed record CachedToken(string AccessToken, DateTimeOffset IssuedAt, DateTimeOffset RefreshAt);

        private sealed class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("scope")]
            public string? Scope { get; set; }
        }
    }
}
