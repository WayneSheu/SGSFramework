using System.Net;
using Enterprise.HttpSdk.Handlers;
using Enterprise.HttpSdk.Interfaces;
using Enterprise.HttpSdk.Options;
using Enterprise.HttpSdk.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Enterprise.HttpSdk.Extensions
{
    /// <summary>
    /// 服務間通訊（M2M）HttpClient 註冊：系統帳號權杖注入 + X-Correlation-ID + 指數退讓重試 + 斷路器。
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddSystemAccountHttpClient("OrderServiceClient", "Hangfire",
    ///     client => client.BaseAddress = new Uri("http://order-service.default.svc.cluster.local"),
    ///     account => builder.Configuration.GetSection("SystemAccount:Hangfire").Bind(account));
    /// </code>
    /// </example>
    public static class SystemAccountHttpClientExtensions
    {
        /// <param name="clientName">具名 HttpClient 名稱（IHttpClientFactory.CreateClient 使用）</param>
        /// <param name="accountName">系統帳號設定名稱；多個 HttpClient 可共用同一帳號與其權杖快取</param>
        /// <param name="configureClient">設定 BaseAddress（請使用 K8s 內部網域）等</param>
        /// <param name="configureAccount">設定權杖端點與 client_id / client_secret（金鑰請由環境變數注入）</param>
        public static IHttpClientBuilder AddSystemAccountHttpClient(
            this IServiceCollection services,
            string clientName,
            string accountName,
            Action<HttpClient> configureClient,
            Action<SystemAccountClientOptions> configureAccount)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
            ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
            ArgumentNullException.ThrowIfNull(configureClient);
            ArgumentNullException.ThrowIfNull(configureAccount);

            services.AddOptions<SystemAccountClientOptions>(accountName).Configure(configureAccount);
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<ISystemAccountTokenService, SystemAccountTokenService>();
            services.TryAddTransient<ClientCorrelationHandler>();

            // 換發權杖專用 HttpClient：不掛授權 Handler；僅對網路錯誤 / 5xx 短暫重試
            services.AddHttpClient(SystemAccountTokenService.TokenHttpClientName)
                .AddStandardResilienceHandler(resilience =>
                {
                    resilience.Retry.MaxRetryAttempts = 2;
                    resilience.Retry.BackoffType = DelayBackoffType.Exponential;
                    resilience.Retry.UseJitter = true;
                });

            var clientBuilder = services.AddHttpClient(clientName, configureClient)
                .AddHttpMessageHandler<ClientCorrelationHandler>()
                .AddHttpMessageHandler(sp => new SystemTokenAuthorizationHandler(
                    sp.GetRequiredService<ISystemAccountTokenService>(), accountName));

            clientBuilder.AddResilienceHandler($"{clientName}-InternalMicroservicePolicy", builder =>
            {
                // A. 指數退讓重試：5xx、408、429 或網路錯誤時等待約 2s、4s、8s 後重試（含 Jitter）；4xx 屬呼叫端錯誤不重試
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .HandleResult(response => (int)response.StatusCode >= 500 ||
                                                  response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests),
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromSeconds(2)
                });

                // B. 斷路器：30 秒內失敗率達 50%（至少 8 次請求）即熔斷 15 秒，保護內網頻寬與下游服務
                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    FailureRatio = 0.5,
                    MinimumThroughput = 8,
                    BreakDuration = TimeSpan.FromSeconds(15)
                });
            });

            return clientBuilder;
        }
    }
}
