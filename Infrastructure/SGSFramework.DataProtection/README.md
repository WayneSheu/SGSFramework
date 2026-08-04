GSFramework.DataProtection Architecture README本文件為 SGSFramework.DataProtection 敏感資料保護與金鑰管理元件之架構規範與開發指南。SGSFramework.DataProtection 是企業級系統中專為靜態資料加密 (Data-at-Rest Encryption)、資料傳輸遮蔽 (PII Masking) 與金鑰生命週期管理設計的核心安全組件。本組件基於 ASP.NET Core Data Protection API 與 AES-256-GCM 高階加密標準封裝，提供欄位級動態加密 (Column-Level Encryption)、自動化金鑰輪替 (Key Rotation) 以及個資去識別化（Masking & Anonymization）機制，符合 ISO 27001 與個資法（GDPR / PII）資安合規標準。🛠️ 技術堆疊與設計特點開發框架：.NET 10.0 (C# 14 strict null checks)設計準則：Clean Architecture, Zero-Trust Architecture, Defense-in-Depth核心技術：AES-256-GCM / Authenticated Encryption：提供具備完整性驗證 (Authenticated Encryption with Associated Data, AEAD) 的高強度加密演算法，防止 Ciphertext 被篡改EF Core 10 欄位動態加解密：透過 ValueConverter 與自訂標籤 ([Encrypted])，實現資料庫寫入自動加密、讀取自動解密金鑰環持久化與憑證保護 (Key Ring)：支援金鑰儲存於 MSSQL 2025 / Redis，並以 X.509 企業憑證或 Key Vault 進行主金鑰 (Master Key) 加密保護個資去識別化引擎 (PII Masker)：提供宣告式 [Masked] 屬性與高效能正規表示式遮蔽引擎📦 核心架構與目錄結構本組件將加密契約、EF Core 轉換器、金鑰配置與去識別化引擎嚴格劃分：PlaintextSGSFramework.DataProtection/
├── Abstractions/               # 加解密與遮蔽契約介面
│   ├── IDataProtectionService.cs
│   ├── IPiiMaskerService.cs
│   └── Attributes/
│       ├── EncryptedAttribute.cs
│       └── MaskedAttribute.cs
├── Services/                   # 加密與遮蔽引擎實作
│   ├── DataProtectionService.cs
│   └── PiiMaskerService.cs
├── EntityFramework/            # EF Core 欄位級加解密整合
│   ├── EncryptedConverter.cs
│   └── Extensions/
│       └── ModelBuilderExtensions.cs
├── Options/                    # 強型別組態設定
│   └── DataProtectionOptions.cs
└── Extensions/                 # DI 服務註冊與管道鏈式擴充
    └── DataProtectionServiceCollectionExtensions.cs
🚀 快速整合與使用1. 服務註冊 (Program.cs)在系統初始化階段，註冊 DataProtection 服務並設定金鑰持久化路徑：C#using SGSFramework.DataProtection.Extensions;
using SGSFramework.DataProtection.Options;

var builder = WebApplication.CreateBuilder(args);

// 1. 綁定強型別 DataProtection 配置項目
builder.Services.Configure<DataProtectionOptions>(builder.Configuration.GetSection("DataProtectionSettings"));

// 2. 註冊 SGSFramework.DataProtection 核心服務（自動配置持久化金鑰環與 AES-256-GCM 引擎）
builder.Services.AddSGSDataProtection(builder.Configuration);

var app = builder.Build();
app.Run();
2. 組態配置 (appsettings.json)設定金鑰輪替週期、金鑰存儲位置與憑證指紋：JSON{
  "DataProtectionSettings": {
    "ApplicationDiscriminator": "SGSFramework.PHYS",
    "KeyRotationDays": 90,
    "KeyStorageType": "Database", // 可選: Database / Redis / FileSystem
    "CertificateThumbprint": "YOUR_X509_CERTIFICATE_THUMBPRINT_HERE",
    "DefaultMaskCharacter": "*"
  }
}
🏗️ 實作指引與程式碼範例本架構嚴格遵循「先給出介面/定義，再給出實作」與強型別 Null 檢查規範。步驟 1：定義加解密與遮蔽契約 (Interface / Abstractions)C#namespace SGSFramework.DataProtection.Abstractions;

public interface IDataProtectionService
{
    string Protect(string unprotectedText, string? purpose = null);
    string Unprotect(string protectedText, string? purpose = null);
    byte[] ProtectData(byte[] userData, string? purpose = null);
    byte[] UnprotectData(byte[] protectedData, string? purpose = null);
}

public interface IPiiMaskerService
{
    string MaskEmail(string email);
    string MaskNationalId(string nationalId);
    string MaskPhoneNumber(string phoneNumber);
    string MaskCustom(string rawValue, int keepLeft = 2, int keepRight = 2, char maskChar = '*');
}
步驟 2：實作資料保護服務 (Implementation)C#using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.DataProtection.Abstractions;
using SGSFramework.DataProtection.Options;

namespace SGSFramework.DataProtection.Services;

public sealed class DataProtectionService : IDataProtectionService
{
    private readonly IDataProtectionProvider _provider;
    private readonly IOptions<DataProtectionOptions> _options;
    private readonly ILogger<DataProtectionService> _logger;

    public DataProtectionService(
        IDataProtectionProvider provider,
        IOptions<DataProtectionOptions> options,
        ILogger<DataProtectionService> logger)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Protect(string unprotectedText, string? purpose = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(unprotectedText);

        try
        {
            var protector = GetProtector(purpose);
            return protector.Protect(unprotectedText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [DataProtection] 資料加密過程發生例外，Purpose: {Purpose}", purpose ?? "Default");
            throw new InvalidOperationException("資料加密失敗，無法確保保護安全性。", ex);
        }
    }

    public string Unprotect(string protectedText, string? purpose = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(protectedText);

        try
        {
            var protector = GetProtector(purpose);
            return protector.Unprotect(protectedText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [DataProtection] 資料解密過程發生例外，密文可能已被竄改或金鑰失效。Purpose: {Purpose}", purpose ?? "Default");
            throw new InvalidOperationException("資料解密失敗，目標密文無效或遭到竄改。", ex);
        }
    }

    public byte[] ProtectData(byte[] userData, string? purpose = null)
    {
        ArgumentNullException.ThrowIfNull(userData);
        if (userData.Length == 0) return Array.Empty<byte>();

        try
        {
            var protector = GetProtector(purpose);
            return protector.Protect(userData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [DataProtection] 二進位資料加密失敗。Purpose: {Purpose}", purpose ?? "Default");
            throw new InvalidOperationException("二進位資料加密處理異常。", ex);
        }
    }

    public byte[] UnprotectData(byte[] protectedData, string? purpose = null)
    {
        ArgumentNullException.ThrowIfNull(protectedData);
        if (protectedData.Length == 0) return Array.Empty<byte>();

        try
        {
            var protector = GetProtector(purpose);
            return protector.Unprotect(protectedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [DataProtection] 二進位資料解密失敗。Purpose: {Purpose}", purpose ?? "Default");
            throw new InvalidOperationException("二進位資料解密處理異常。", ex);
        }
    }

    private IDataProtector GetProtector(string? purpose)
    {
        string purposeString = string.IsNullOrWhiteSpace(purpose)
            ? _options.Value.ApplicationDiscriminator
            : $"{_options.Value.ApplicationDiscriminator}.{purpose}";

        return _provider.CreateProtector(purposeString);
    }
}
步驟 3：EF Core 實體動態欄位加解密使用範例 (Presentation / Domain Usage)C#using System.ComponentModel.DataAnnotations;
using SGSFramework.DataProtection.Abstractions.Attributes;

namespace SGSFramework.DataProtection.Domain.Entities;

public class Employee
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = string.Empty;

    // 標註 [Encrypted] 屬性：EF Core 寫入資料庫前自動加密，讀取時自動解密
    [Encrypted]
    public string NationalId { get; private set; } = string.Empty;

    [Encrypted]
    public string CreditCardNumber { get; private set; } = string.Empty;

    public void SetSensitiveInfo(string nationalId, string creditCard)
    {
        ArgumentException.ThrowIfNullOrEmpty(nationalId);
        ArgumentException.ThrowIfNullOrEmpty(creditCard);

        NationalId = nationalId;
        CreditCardNumber = creditCard;
    }
}
🔒 核心防禦與安全機制防禦維度機制說明AEAD 驗證式加密採用 AES-256-GCM 驗證式加密演算法，解密時自動驗證 MAC 簽章，防止 Ciphertext 被中間人修改或注入。目的隔離 (Purpose Isolation)支援 CreateProtector(purpose) 機制，不同業務領域（如身分證與信用卡）之金鑰子集隔離，避免金鑰洩漏造成全域災難。金鑰自動輪替與撤銷金鑰環具備自動生命週期（預設 90 天輪替），舊金鑰保留唯讀解密能力，支援過期金鑰自動撤銷 (Revocation)。記憶體敏感資料保護金鑰解密過程中儘速釋放敏感 Byte 陣列，防止記憶體 Dump (Memory Leak) 暴露明文金鑰。⚠️ 核心紀律規範項目具體要求嚴禁寫死 Master Key絕對禁止於 appsettings.json 或程式碼中硬編碼 (Hardcode) 明文 Master Key 或憑證私鑰。預防明文日誌洩漏日誌記錄器 (ILogger) 嚴禁列印任何保護前 (Unprotected) 或解密後之明文敏感資料，僅能記錄錯誤 Context。強型別與 Null 檢查全面啟用 C# 14 Nullable Reference Types，傳入待加解密之字串或 Byte 陣列必須進行精確 throw/check 驗證。EF Core 索引限制經 [Encrypted] 加密之 DB 欄位因每次 Ciphertext 隨機 Salt 變化，無法直接進行 SQL LIKE 或 = 精確索引搜尋。👥 團隊維護與支援主要維護者：Wayne設計規範參考：企業級 Clean Architecture 與 GDPR / PII 資料保護作業指引💡 提醒：若需為資料庫加密欄位引入可搜尋加密 (Searchable Encryption) 或確定性加密 (Deterministic Encryption)，請先於 Git 進行議題討論 (Issue Tracking)，評估安全風險與演算法合規性。