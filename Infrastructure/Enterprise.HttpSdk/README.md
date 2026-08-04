Enterprise.HttpSdk Architecture README本文件為 Enterprise.HttpSdk 模組化通訊 SDK 組件之架構規範與開發指南。Enterprise.HttpSdk 是企業級微服務與模組化系統的核心 HTTP 通訊元件。本組件提供強型別、聲明式且具備高韌性 (Resilient) 的 HTTP 用戶端封裝，整合 Polly 彈性策略（自動重試、熔斷、限流）、分散式追蹤標頭（Correlation ID）、動態 JWT Token 注入與結構化日誌記錄，確保微服務與外部 API 溝通的穩定性與安全標準。🛠️ 技術堆疊與設計特點開發框架：.NET 10.0 (C# 14 strict null checks)設計準則：Clean Architecture, Resilient Microservice Communication核心技術：IHttpClientFactory & Typed Clients：強型別 Client 生命週期管理，避免 Socket 耗盡問題Polly v8 / Microsoft.Extensions.Http.Resilience：提供指數退避重試 (Exponential Backoff Retry)、熔斷器 (Circuit Breaker) 與超時隔離DelegatingHandler 鏈式處理：自動注入 Correlation ID、Bearer Token 與 Request/Response Serilog 追蹤System.Net.Http.Json / Refit：支援聲明式 API 介面定義與高效能 JSON 序列化📦 核心架構與目錄結構本組件採用高內聚、低耦合的架構設計，將標頭處理、韌性原則與用戶端註冊獨立劃分：PlaintextEnterprise.HttpSdk/
├── Abstractions/               # SDK 契約與通用 Client 介面定義
│   ├── IHttpSdkClient.cs
│   └── IAuthenticationTokenProvider.cs
├── Handlers/                   # 鏈式 DelegatingHandler 實作
│   ├── AuthenticationHeaderHandler.cs
│   ├── CorrelationIdHandler.cs
│   └── HttpLoggingHandler.cs
├── Resilience/                 # Polly 韌性策略管道建構
│   └── HttpResiliencePolicyBuilder.cs
├── Options/                    # 強型別組態選項 (IOptions<T>)
│   └── HttpSdkOptions.cs
└── Extensions/                 # IServiceCollection 註冊與鏈式設定擴充
    └── HttpSdkServiceCollectionExtensions.cs
🚀 快速整合與使用1. 服務註冊 (Program.cs)於系統啟動時，註冊 HTTP SDK 服務與設定對應的 API Client 韌性管道：C#using Enterprise.HttpSdk.Extensions;
using Enterprise.HttpSdk.Options;

var builder = WebApplication.CreateBuilder(args);

// 1. 綁定強型別 SDK 組態配置
builder.Services.Configure<HttpSdkOptions>(builder.Configuration.GetSection("HttpSdkSettings"));

// 2. 註冊 Enterprise HttpSdk 核心處理器 (CorrelationId, Auth, Logging)
builder.Services.AddEnterpriseHttpSdk();

// 3. 註冊強型別 Http Client 並啟用韌性管道
builder.Services.AddHttpSdkClient<IUserApiClient, UserApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["HttpSdkSettings:Services:UserServiceUrl"] 
        ?? throw new InvalidOperationException("未設定 UserServiceUrl 端點。"));
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();
app.Run();
2. 組態配置 (appsettings.json)設定目標服務端點、超時時間與 Polly 韌性參數：JSON{
  "HttpSdkSettings": {
    "EnableCorrelationId": true,
    "EnableLogging": true,
    "Resilience": {
      "MaxRetryAttempts": 3,
      "DelaySeconds": 2,
      "CircuitBreakerDurationSeconds": 30,
      "FailureRatio": 0.5
    },
    "Services": {
      "UserServiceUrl": "https://api.enterprise.internal/user-service/"
    }
  }
}
🏗️ 實作指引與程式碼範例本架構嚴格遵循「先給出介面/定義，再給出實作」與強型別 Null 檢查規範。步驟 1：定義 API 介面與資料合約 (Interface / Abstractions)C#namespace Enterprise.HttpSdk.Abstractions;

public record UserDto(Guid Id, string UserName, string Email, bool IsActive);

public interface IUserApiClient
{
    Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateUserStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
}
步驟 2：實作強型別 HTTP Client (Implementation)C#using System.Net.Http.Json;
using Enterprise.HttpSdk.Abstractions;
using Microsoft.Extensions.Logging;

namespace Enterprise.HttpSdk.Clients;

public sealed class UserApiClient : IUserApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserApiClient> _logger;

    public UserApiClient(HttpClient httpClient, ILogger<UserApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID 不能為空白 Guid。", nameof(userId));
        }

        try
        {
            _logger.LogInformation(">>> [HttpSdk] 發送 GetUserById 要求，UserId: {UserId}", userId);

            using var response = await _httpClient.GetAsync($"api/v1/users/{userId}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning(">>> [HttpSdk] 找不到指定使用者，UserId: {UserId}", userId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<UserDto>(cancellationToken: cancellationToken);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, ">>> [HttpSdk] 呼叫 UserApiClient.GetUserById 發生網路異常，UserId: {UserId}", userId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [HttpSdk] 呼叫 UserApiClient.GetUserById 發生未預期錯誤，UserId: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> UpdateUserStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID 不能為空白 Guid。", nameof(userId));
        }

        try
        {
            var payload = new { IsActive = isActive };
            using var response = await _httpClient.PutAsJsonAsync($"api/v1/users/{userId}/status", payload, cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [HttpSdk] 呼叫 UserApiClient.UpdateUserStatus 發生錯誤，UserId: {UserId}", userId);
            throw;
        }
    }
}
🔒 核心防禦與韌性機制韌性與安全維度機制說明自動指數重試 (Retry)對 5xx 伺服器錯誤與 408 Timeout 自動進行指數退避 (Exponential Backoff) 重試，降低突發網路抖動衝擊。熔斷器 (Circuit Breaker)當遠端服務錯誤率超過指定閾值 (如 50%)，自動熔斷並拒絕後續請求，給予下游服務復原時間。Correlation ID 傳遞自動從目前 HTTP Context 擷取 Trace Header，並寫入傳出 HTTP 請求頭，維持微服務間的分散式日誌追蹤。動態 JWT 注入透過 AuthenticationHeaderHandler 在發送前自動向 TokenProvider 取得最新 Bearer Token 並寫入 Header。連線池與 DNS 防護正確配置 SocketsHttpHandler.PooledConnectionLifetime，預防 DNS 變更無法生效與 Socket 耗盡問題。⚠️ 核心紀律規範項目具體要求禁止直接使用 HttpClient嚴禁在業務邏輯或服務中手動 new HttpClient()，必須統一透過 AddHttpSdkClient<TInterface, TImpl>() 註冊使用。強制強型別與 Null 檢查全面啟用 C# 14 Nullable Reference Types，傳入參數與 API 回傳值皆須進行精確 throw/check 檢查。傳遞 CancellationToken所有非同步 HTTP 呼叫方法必須將 CancellationToken 傳遞至末端，確保前端取消請求時可迅速釋放下游資源。敏感資訊遮蔽日誌記錄器 (HttpLoggingHandler) 必須自動遮蔽 Authorization 標頭與密碼、Token 等敏感 Payload。👥 團隊維護與支援主要維護者：Wayne設計規範參考：企業級 Clean Architecture 與 .NET 10 通訊標準作業指引💡 提醒：若需新增第三方 API 整合或自訂 Polly 熔斷管道，請先於 Git 進行議題討論 (Issue Tracking)，確保全域韌性策略的一致性。