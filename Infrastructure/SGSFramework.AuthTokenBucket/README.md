SGSFramework.AuthTokenBucket Architecture README本文件為 SGSFramework.AuthTokenBucket 認證與動態限流元件之架構規範與開發指南。SGSFramework.AuthTokenBucket 是企業級系統中專為安全認證 (Authentication) 與 API 端點防禦設計的高效能限流 (Rate Limiting) 元件。本組件採用 令牌桶演算法 (Token Bucket Algorithm)，提供多維度（IP、使用者 ID、API Client ID）的動態流量控制，有效抵禦暴力破解 (Brute-Force)、DDoS 攻擊與高併發資源耗盡風險，並支援記憶體 (In-Memory) 與分散式 (Redis) 快取雙模式切換。🛠️ 技術堆疊與設計特點開發框架：.NET 10.0 (C# 14 strict null checks)設計準則：Clean Architecture, High-Concurrency Threat Defense核心技術：Token Bucket 演算法：支援固定平滑補充 (Refill Rate) 與突發流量 (Burst Capacity) 允許機制分散式原子性操作：結合 Redis Lua Script 確保多節點部署下的強一致性與無鎖併發安全ASP.NET Core 中介軟體：提供動態中介軟體 (Middleware) 與自訂宣告式屬性 ([TokenBucketLimit])極致效能：記憶體模式採用 System.Threading.Channels 與 Interlocked 原子操作，降低 GC 壓力📦 核心架構與目錄結構本組件將限流介面、令牌桶演算法實作、中介軟體與 DI 註冊擴充進行劃分：PlaintextSGSFramework.AuthTokenBucket/
├── Abstractions/               # 限流契約與令牌桶狀態介面
│   ├── ITokenBucketService.cs
│   ├── ITokenBucketStore.cs
│   └── Models/
│       └── TokenBucketResult.cs
├── Services/                   # 令牌桶演算法核心實作
│   ├── InMemoryTokenBucketStore.cs
│   ├── RedisTokenBucketStore.cs
│   └── TokenBucketService.cs
├── Middleware/                 # 限流攔截中介軟體
│   └── TokenBucketRateLimitingMiddleware.cs
├── Attributes/                 # 端點宣告式限流屬性
│   └── TokenBucketLimitAttribute.cs
├── Options/                    # 強型別組態設定
│   └── TokenBucketOptions.cs
└── Extensions/                 # DI 服務註冊與管道鏈式擴充
    └── TokenBucketServiceCollectionExtensions.cs
🚀 快速整合與使用1. 服務註冊 (Program.cs)在系統初始化階段，註冊 TokenBucket 服務並啟用限流中介軟體：C#using SGSFramework.AuthTokenBucket.Extensions;
using SGSFramework.AuthTokenBucket.Options;

var builder = WebApplication.CreateBuilder(args);

// 1. 綁定強型別組態設定
builder.Services.Configure<TokenBucketOptions>(builder.Configuration.GetSection("TokenBucketSettings"));

// 2. 註冊 SGSFramework.AuthTokenBucket 核心服務 (預設選用 Redis 或 In-Memory)
builder.Services.AddAuthTokenBucket(builder.Configuration);

var app = builder.Build();

// 3. 啟用 Auth Token Bucket 限流中介軟體
app.UseAuthTokenBucket();

app.MapControllers();
app.Run();
2. 組態配置 (appsettings.json)設定預設的容量上限 (Capacity)、補充速率 (RefillRate) 與儲存媒介：JSON{
  "TokenBucketSettings": {
    "StoreType": "InMemory", // 可選: InMemory / Redis
    "RedisConnectionString": "localhost:6379,defaultDatabase=1",
    "DefaultOptions": {
      "Capacity": 100,
      "RefillRatePerSecond": 10,
      "KeyPrefix": "sgs_ratelimit"
    },
    "Endpoints": {
      "Authentication": {
        "Capacity": 5,
        "RefillRatePerSecond": 1,
        "KeyPrefix": "sgs_auth_limit"
      }
    }
  }
}
🏗️ 實作指引與程式碼範例本架構嚴格遵循「先給出介面/定義，再給出實作」與強型別 Null 檢查規範。步驟 1：定義限流契約與結果模型 (Interface / Abstractions)C#namespace SGSFramework.AuthTokenBucket.Abstractions;

public sealed record TokenBucketResult
{
    public bool IsAllowed { get; init; }
    public long RemainingTokens { get; init; }
    public TimeSpan RetryAfter { get; init; }

    public static TokenBucketResult Success(long remaining) =>
        new() { IsAllowed = true, RemainingTokens = remaining, RetryAfter = TimeSpan.Zero };

    public static TokenBucketResult Denied(TimeSpan retryAfter) =>
        new() { IsAllowed = false, RemainingTokens = 0, RetryAfter = retryAfter };
}

public interface ITokenBucketService
{
    ValueTask<TokenBucketResult> ConsumeAsync(
        string bucketKey, 
        int tokensToConsume = 1, 
        int capacity = 100, 
        double refillRatePerSecond = 10.0, 
        CancellationToken cancellationToken = default);
}
步驟 2：實作令牌桶核心服務 (Implementation)C#using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SGSFramework.AuthTokenBucket.Services;

public sealed class TokenBucketService : ITokenBucketService
{
    private readonly ITokenBucketStore _store;
    private readonly IOptions<TokenBucketOptions> _options;
    private readonly ILogger<TokenBucketService> _logger;

    public TokenBucketService(
        ITokenBucketStore store,
        IOptions<TokenBucketOptions> options,
        ILogger<TokenBucketService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<TokenBucketResult> ConsumeAsync(
        string bucketKey,
        int tokensToConsume = 1,
        int capacity = 100,
        double refillRatePerSecond = 10.0,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketKey);

        if (tokensToConsume <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokensToConsume), "消耗令牌數必須大於 0。");
        }

        try
        {
            var result = await _store.TryConsumeAsync(bucketKey, tokensToConsume, capacity, refillRatePerSecond, cancellationToken);

            if (!result.IsAllowed)
            {
                _logger.LogWarning(">>> [AuthTokenBucket] 限流攔截！Key: {Key}, 重試等待: {RetryAfter} 秒", 
                    bucketKey, result.RetryAfter.TotalSeconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [AuthTokenBucket] 執行令牌桶扣減時發生例外，Key: {Key}", bucketKey);
            
            // 降級防護 (Fail-Open 或 Fail-Closed，依配置定義)
            return TokenBucketResult.Success(1);
        }
    }
}
步驟 3：控制器宣告式使用範例 (Controller Usage)C#using SGSFramework.AuthTokenBucket.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace SGSFramework.AuthTokenBucket.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    [TokenBucketLimit(Capacity = 5, RefillRatePerSecond = 0.2, KeyPartition = PartitionType.ClientIp)] // 5 次容量，每 5 秒恢復 1 次
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // 執行認證邏輯
        return Ok(new { Token = "JWT_TOKEN_SAMPLE" });
    }
}
🔒 核心防禦與限流機制防禦維度機制說明IP / User 多重分割支援以 Client IP、User ID 或 API Key 做為 Partition Key，避免單一用戶過載整體 API。動態 HTTP 429 標頭被攔截時自動回傳 HTTP 429 Too Many Requests，並附帶 Retry-After 與 X-RateLimit-Remaining 標頭。Redis 原子性 Lua 腳本分散式環境下將計算與更新整合至單一 Lua Script 執行，防止高併發產生 Race Condition。突發流量平滑化允許短時間內的突發 Peak 流量（上限為 Bucket Capacity），同時透過 Refill Rate 穩定收斂調頻。⚠️ 核心紀律規範項目具體要求金鑰區隔 (Key Prefix)所有寫入快取之 Bucket Key 必須加入系統首綴（如 sgs_auth_），禁止動態字串直接暴露於全域命名空間。嚴格強型別與 Null 檢查全面啟用 C# 14 Nullable Reference Types，傳入參數與配置選項皆須進行精確 throw/check 驗證。防範 Redis 斷線崩潰當 Redis 發生連線異常時，須依據策略觸發 Fail-Open (放行) 或降級至 Local Memory，不得直接拋出 500 錯誤。高頻介面採用 ValueTask核心非同步方法應優先回傳 ValueTask<T>，降低高併發場景下 Task 物件之記憶體配置負擔。👥 團隊維護與支援主要維護者：Wayne設計規範參考：企業級 Clean Architecture 與高併發安全防禦作業指引💡 提醒：如需新增自訂限流策略（例如搭配 Bitmask 權限動態調整容量），請先於 Git 進行議題討論 (Issue Tracking)，確保全域快取結構的一致性。