using System;
using System.Net.Http;
using Enterprise.HttpSdk.Clients;
using Enterprise.HttpSdk.Handlers;
using Enterprise.HttpSdk.Interfaces;
using Enterprise.HttpSdk.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Enterprise.HttpSdk.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IHttpClientBuilder AddEnterpriseHttpClient(
            this IServiceCollection services,
            Action<HttpClientOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            var options = new HttpClientOptions();
            configureOptions(options);

            if (options.BaseAddress is null)
            {
                throw new InvalidOperationException("BaseAddress must be specified for the enterprise HttpClient.");
            }

            // 註冊基礎攔截器
            //services.AddTransient<ClientCorrelationHandler>();
            //services.AddTransient<LaboratoryMockHeaderHandler>(); // 🚀 注入環境感知型模擬標頭處理器

            // 建立強型別 HttpClient 核心 Builder
            IHttpClientBuilder httpClientBuilder = services.AddHttpClient<IApiClient, BaseApiClient>(client =>
            {
                client.BaseAddress = options.BaseAddress;
                client.Timeout = options.Timeout;
            });

            // 串接管線
            //httpClientBuilder.AddHttpMessageHandler<AuthenticationHeaderHandler>(); // 🚀 關鍵：讓所有透過 BaseApiClient 發送的請求自動具備 Token 承載能力
            //httpClientBuilder.AddHttpMessageHandler<ClientCorrelationHandler>();
            //httpClientBuilder.AddHttpMessageHandler<LaboratoryMockHeaderHandler>(); // 🚀 掛載至處理管線

            // 彈性策略防禦
            httpClientBuilder.AddStandardResilienceHandler(resilience =>
            {
                resilience.Retry.MaxRetryAttempts = options.MaxRetryAttempts;
                resilience.Retry.Delay = TimeSpan.FromSeconds(2);
                resilience.Retry.BackoffType = DelayBackoffType.Exponential;
                resilience.Retry.UseJitter = true;

                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);

                resilience.CircuitBreaker.FailureRatio = 0.5;
                resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                resilience.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            });

            return httpClientBuilder;
        }
    }
}