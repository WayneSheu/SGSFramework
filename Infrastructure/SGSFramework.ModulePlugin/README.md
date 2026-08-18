**SGSFramework.ModulePlugin**

本文件為 SGSFramework.ModulePlugin 模組化外掛系統之架構設計與開發指南。
🛠️ 技術堆疊與架構規範開發框架：.NET 10.0 (採 C# 10+ 嚴格強型別與 Null 檢查規範)
架構風格：Clean Architecture 與 Modular Monolith (模組化單體) 架構
核心機制：動態組件載入：基於 AssemblyLoadContext 進行模組生命週期管理。
零信任安全驗證：自動化數位簽章驗證與沙盒隔離。
自動化中繼資料同步：動態 Controller 掃描與資料庫元資料同步。
📦 核心模組與目錄結構本系統採用外掛式模組化設計，各模組獨立編譯為主 DLL，並透過隔離與載入機制進行生命週期管理：

SGSFramework.ModulePlugin/
├── Systems/
│   └── Module/
│       └── Loaders/
│           └── ModuleLoaderExtensions.cs    # 外掛掃描、簽章驗證、ALC 載入與隔離邏輯
├── Extensions/
│   └── DynamicControllerLoaderExtensions.cs # 動態 Controller 掃描與資料庫中繼資料同步
└── plugins/                                 # 外掛模組部署目錄
    └── quarantine/                          # 安全隔離區（非法或簽章失敗檔案）

🧩 組件功能說明
SGSFramework.ModulePlugin.Systems.Module.Loaders：負責掃描外掛目錄、驗證數位簽章 (AssemblySecurityVerifier)、隔離非法檔案 (quarantine)，以及執行模組依賴註冊。
SGSFramework.ModulePlugin.Extensions：提供應用程式啟動時的動態 Controller 掃描與資料庫中繼資料同步 (DynamicControllerLoaderExtensions)。
quarantine：系統安全隔離區，當外掛模組未通過數位簽章驗證時，主 DLL 及其附屬檔案（.Application.dll、.Infrastructure.dll、.pdb）會被自動強制隔離。
🚀 快速整合與使用
服務註冊 (Program.cs)在服務初始化階段，順序載入模組化外掛與動態中繼資料：
C#
using SGSFramework.ModulePlugin.Extensions;
using SGSFramework.ModulePlugin.Systems.Module.Loaders;

var builder = WebApplication.CreateBuilder(args);

// 1. 註冊並動態掃描模組外掛（含數位簽章驗證與資料庫同步）
builder.Services.AddModularModules(builder.Configuration);

var app = builder.Build();

// 2. 啟用模組端點與組態設定
await app.UseModularModulesAsync();

// 3. 啟用動態 Controller 中繼資料同步中介軟體（具備白名單過濾）
await app.UseDynamicControllersAsync();

app.Run();

組態配置 (appsettings.json)設定外掛目錄路徑與企業受信任憑證之指紋資訊：
JSON{
  "ModularSettings": {
    "PluginsPath": "plugins",
    "TrustedThumbprint": "YOUR_COMPANY_CERT_THUMBPRINT_HERE"
  }
}

🔒 開發人員本機環境金鑰準備測試用金鑰生成：
在本機透過 Visual Studio 開發人員命令提示字元 (Developer Command Prompt) 自行產生測試用金鑰:

 Bash
 sn -k MyModuleKey.snk

 並將產出的金鑰放置於專案對應的相對路徑下.

🔒 核心防禦與安全機制防禦維度機制說明嚴格數位簽章驗證所有載入之主模組 DLL 必須通過 AssemblySecurityVerifier 憑證校驗，違規檔案將直接阻斷載入並移至 quarantine 隔離區。附屬檔案完整隔離當外掛驗證失敗時，系統會一併將同名的 .Application.dll、.Infrastructure.dll 及其對應的 .pdb 檔案移至隔離區，確保不殘留任何非法組件。
🔒 動態控制器安全白名單DynamicControllerLoaderExtensions 僅允許透過 ModuleLoaderExtensions 正規載入且驗證成功的合法模組名稱寫入 ControllerMetadata，杜絕未授權的中繼資料汙染。