SGSFramework.Identity Architecture README本文件為 SGSFramework.Identity 身分驗證、存取授權與工作階段管理元件之架構規範與開發指南。SGSFramework.Identity 是企業級系統中專為安全驗證 (Authentication)、細粒度動態授權 (Authorization) 與多裝置工作階段 (Multi-Device Session Management) 設計的核心身分識別組件。本組件整合 JWT 自動輪替 (Token Rotation)、超越 64 位元的擴充位元圖 (Extended Bitmask) 權限運算引擎，並支援與主流 Identity Provider (如 Keycloak、IdentityServer) 的 OpenID Connect (OIDC) 聯邦驗證，資料持久化層原生支援 MSSQL 2025。🛠️ 技術堆疊與設計特點開發框架：.NET 10.0 (C# 14 strict null checks)設計準則：Clean Architecture, Zero-Trust Access Control, Modular Monolith核心技術：JWT / Refresh Token 雙軌輪替：支援 Refresh Token Reuse Detection (重用偵測) 與多裝置工作階段隔離/強制登出超長位元圖權限運算 (Extended Bitmask Engine)：突破傳統 64 位元 (ulong) 限制，採用 BigInteger / ReadOnlySpan<byte> 陣列實現超過 64 個權限點之高效能極速位元運算OIDC 與外部 Identity 整合：提供 Keycloak / IdentityServer 標準 Claims 轉換與中繼資料同步MSSQL 2025 原生整合：搭配 Temporal Tables 與 Ledger 功能進行安全存取審計日誌 (Audit Trail)📦 核心架構與目錄結構本組件將身分驗證契約、位元圖授權邏輯、Token 管理與 DI 註冊嚴格劃分：PlaintextSGSFramework.Identity/
├── Domain / Abstractions/      # 身分識別契約、權限位元模型與介面
│   ├── ITokenService.cs
│   ├── IPermissionEvaluator.cs
│   ├── ISessionManager.cs
│   └── Models/
│       ├── UserSession.cs
│       └── ExtendedBitmask.cs
├── Application/                # 授權與 Token 處理邏輯
│   ├── Services/
│   │   ├── JwtTokenService.cs
│   │   ├── BitmaskPermissionEvaluator.cs
│   │   └── SessionManager.cs
│   └── Commands/
│       └── RefreshTokenCommand.cs
├── Infrastructure / Persistence/ # MSSQL 2025 Session / Token 儲存庫
│   └── Repositories/
│       └── UserSessionRepository.cs
├── Presentation / Middleware/  # ASP.NET Core 授權攔截器與 Handler
│   ├── Handlers/
│   │   └── BitmaskPermissionHandler.cs
│   └── Middlewares/
│       └── SessionValidationMiddleware.cs
├── Options/                    # 強型別組態設定
│   └── JwtOptions.cs
└── Extensions/                 # DI 服務註冊與管道鏈式擴充
    └── IdentityServiceCollectionExtensions.cs
🚀 快速整合與使用1. 服務註冊 (Program.cs)在系統初始化階段，註冊 Identity 服務並配置 JWT 與位元圖授權管道：C#using SGSFramework.Identity.Extensions;
using SGSFramework.Identity.Options;

var builder = WebApplication.CreateBuilder(args);

// 1. 綁定強型別 Identity 與 JWT 配置
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("JwtSettings"));

// 2. 註冊 SGSFramework.Identity 核心服務 (TokenService, SessionManager, BitmaskEvaluator)
builder.Services.AddSGSIdentity(builder.Configuration);

// 3. 註冊基於 Bitmask 的動態授權策略 Policy
builder.Services.AddSGSBitmaskAuthorization();

var app = builder.Build();

// 4. 啟用身分驗證、工作階段檢查與授權中介軟體
app.UseAuthentication();
app.UseSessionValidation();
app.UseAuthorization();

app.MapControllers();
app.Run();
2. 組態配置 (appsettings.json)設定 Token 生命週期、密鑰簽章與 OIDC 整合參數：JSON{
  "JwtSettings": {
    "Issuer": "https://auth.sgs.enterprise.internal",
    "Audience": "SGS.ApiGateway",
    "SecretKey": "YOUR_SUPER_SECRET_HMAC_SHA256_KEY_MUST_BE_LONG_ENOUGH",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7,
    "AllowMultiDeviceSessions": true,
    "MaxActiveSessionsPerUser": 5
  }
}
🏗️ 實作指引與程式碼範例本架構嚴格遵循「先給出介面/定義，再給出實作」與強型別 Null 檢查規範。步驟 1：定義權限評估與 Token 契約 (Interface / Abstractions)C#namespace SGSFramework.Identity.Abstractions;

public record PermissionEvaluationResult(bool IsGranted, string Reason);

public interface IPermissionEvaluator
{
    /// <summary>
    /// 評估使用者持有的 Extended Bitmask 是否包含目標功能所需的 Bitmask 權限
    /// </summary>
    bool HasPermission(byte[] userPermissionBitmask, byte[] requiredPermissionBitmask);

    /// <summary>
    /// 解析並檢查特定 index 的位元點 (適用於 >64 個權限點)
    /// </summary>
    bool HasPermissionIndex(byte[] userPermissionBitmask, int permissionIndex);
}

public interface ITokenService
{
    Task<string> GenerateAccessTokenAsync(Guid userId, string username, IEnumerable<string> roles, byte[] permissionBitmask, CancellationToken cancellationToken = default);
    Task<(string RefreshToken, Guid SessionId)> GenerateRefreshTokenAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default);
    Task<bool> RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
步驟 2：實作超長位元圖授權運算器 (Implementation)以 BigInteger 支援超過 64 個（至數千個）動態權限點的極速位元 AND 運算：C#using System.Numerics;
using Microsoft.Extensions.Logging;
using SGSFramework.Identity.Abstractions;

namespace SGSFramework.Identity.Services;

public sealed class BitmaskPermissionEvaluator : IPermissionEvaluator
{
    private readonly ILogger<BitmaskPermissionEvaluator> _logger;

    public BitmaskPermissionEvaluator(ILogger<BitmaskPermissionEvaluator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool HasPermission(byte[] userPermissionBitmask, byte[] requiredPermissionBitmask)
    {
        ArgumentNullException.ThrowIfNull(userPermissionBitmask);
        ArgumentNullException.ThrowIfNull(requiredPermissionBitmask);

        if (userPermissionBitmask.Length == 0 || requiredPermissionBitmask.Length == 0)
        {
            return false;
        }

        try
        {
            // 將 byte[] 轉為 BigInteger 進行超越 64 位元之全域 Bitwise AND 運算
            var userVector = new BigInteger(userPermissionBitmask, isUnsigned: true, isBigEndian: false);
            var requiredVector = new BigInteger(requiredPermissionBitmask, isUnsigned: true, isBigEndian: false);

            // 運算邏輯: (UserVector & RequiredVector) == RequiredVector
            bool isGranted = (userVector & requiredVector) == requiredVector;
            
            if (!isGranted)
            {
                _logger.LogWarning(">>> [Identity.Bitmask] 權限驗證不通過。使用者位元數: {UserLen}, 需求位元數: {ReqLen}", 
                    userPermissionBitmask.Length, requiredPermissionBitmask.Length);
            }

            return isGranted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [Identity.Bitmask] 執行位元圖權限比對時發生例外。");
            throw new InvalidOperationException("位元圖權限運算失敗。", ex);
        }
    }

    public bool HasPermissionIndex(byte[] userPermissionBitmask, int permissionIndex)
    {
        ArgumentNullException.ThrowIfNull(userPermissionBitmask);
        if (permissionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(permissionIndex), "權限索引不能為負數。");
        }

        try
        {
            int byteIndex = permissionIndex / 8;
            int bitOffset = permissionIndex % 8;

            if (byteIndex >= userPermissionBitmask.Length)
            {
                return false;
            }

            return (userPermissionBitmask[byteIndex] & (1 << bitOffset)) != 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [Identity.Bitmask] 檢查索引 {Index} 權限位元失敗。", permissionIndex);
            throw new InvalidOperationException($"無法驗證指定索引 ({permissionIndex}) 的權限位元。", ex);
        }
    }
}
步驟 3：控制器宣告式位元圖授權使用範例 (Presentation Usage)C#using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.Identity.Attributes;

namespace SGSFramework.Identity.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class SecurityPolicyController : ControllerBase
{
    // 宣告需求第 128 個權限點 (突破 64-bit 限制)
    [HttpGet("audit-logs")]
    [RequireBitmaskPermission(PermissionIndex = 128)]
    public IActionResult GetSystemAuditLogs()
    {
        return Ok(new { Message = "已成功讀取企業資安審計日誌。" });
    }
}
🔒 核心防禦與安全機制防禦維度機制說明超長位元圖 (Bitmask > 64)採用 BigInteger 或位元組陣列，支援超過 64 個動態權限點，消除長度限制，效能遠優於傳統字串比對。JWT Rotation & Reuse Detection每次刷洗 Refresh Token 時原 Token 即刻廢棄；若偵測到已廢棄的 Refresh Token 重複存取，自動觸發安全告警並作廢該使用者所有裝置之 Session。多裝置 Session 精準控管支援裝置綁定與多開管理（如限制最高 5 個 Active Sessions），可針對單一特定裝置或遠端一鍵強制登出 (Force Logout)。MSSQL 2025 審計記錄結合 MSSQL 2025 時態表 (Temporal Tables) 記錄 Sessions 與 Refresh Tokens 的異動歷史，防範資安事件不可追溯性。⚠️ 核心紀律規範項目具體要求嚴禁在 Claims 明文存儲大金鑰位元圖權限資料寫入 Claim 時須以 Base64 或壓縮格式傳遞，避免 HTTP Header 過大問題。強型別與 Null 檢查全面啟用 C# 14 Nullable Reference Types，Token 與 Session 輸入參數須進行嚴格 null / empty 檢核。非同步資源傳遞所有存取 MSSQL 2025 或 Redis 的 Session 驗證方法必須正確傳遞 CancellationToken。密鑰不可硬編碼SecretKey 與憑證金鑰必須透過 Key Vault 或環境變數注入，嚴禁寫入原始碼或 Repository。👥 團隊維護與支援主要維護者：Wayne設計規範參考：企業級 Clean Architecture、OAuth 2.0 / OIDC 與 dynamic Bitmask 授權標準作業指引💡 提醒：若需為 Bitmask 權限系統新增全域的角色-權限映射字典 (Role-Bitmask Dictionary) 或擴充 OIDC 聯邦驗證，請先於 Git 進行議題討論 (Issue Tracking)，以維護跨服務介面的一致性。