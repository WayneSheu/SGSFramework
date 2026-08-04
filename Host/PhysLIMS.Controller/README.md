PhysLIMS.Api基礎設施為.NET 10 / C# 13建構的企業級基礎架構和動態插件架構核心包。該軟體包為現代微服務和模組化單體應用程式提供基礎功能，包括隔離動態組件載入上下文 (ALC)、外掛程式安全驗證、自動控制器發現和應用程式元件整合、動態 OpenApi 文件轉換和儲存庫管理。主要特點IIS 與跨平台路徑錨定：確保基於絕對物理路徑的解析AppContext.BaseDirectory，克服 IIS 執行目錄上下文漂移（inetsrv目錄問題）。隔離程序集載入（ModuleAssemblyLoadContext）：動態載入和熱卸載模組插件，避免依賴衝突或組件洩漏。安全性與程式碼簽章保護：整合了AssemblySecurityVerifier驗證 SHA-256 數位簽章或受信任憑證指紋的功能，然後再進行動態載入或執行時間執行。自動 MVC 整合：將應用程式元件、控制器和操作動態發現並註冊到 ASP.NET Core 中ApplicationPartManager。動態 OpenAPI 和 Scalar 支援：自動IOpenApiDocumentTransformer實作 ( DynamicControllerDocumentTransformer)，x-tagGroups支援 Scalar 和 Swagger UI 的分組。資料庫驅動的元資料同步：內建後台初始化程序（ModuleDatabaseInitializerHostedService）自動提取控制器、操作、選單和位元遮罩權限元資料並將其同步到 SQL 資料庫。架構背景┌─────────────────────────────────────────────────────────────────┐
│                       Presentation Layer                        │
│         (ASP.NET Core / Web API / Dynamic OpenAPI / UI)          │
└────────────────────────────────────────┬────────────────────────┘
                                         │
┌────────────────────────────────────────▼────────────────────────┐
│                   PhysLIMS.ApiInfrastructure                    │
│   ┌──────────────────────┐   ┌──────────────────────────────┐   │
│   │ ModuleLoaderEngine   │   │ SecurityVerifier             │   │
│   └──────────┬───────────┘   └──────────────┬───────────────┘   │
│              │                              │                   │
│   ┌──────────▼───────────┐   ┌──────────────▼───────────────┐   │
│   │ ModuleAssemblyLoadCtx│   │ OpenApi DocumentTransformer  │   │
│   └──────────────────────┘   └──────────────────────────────┘   │
└────────────────────────────────────────┬────────────────────────┘
                                         │
┌────────────────────────────────────────▼────────────────────────┐
│                        Domain / Core Layer                      │
│            (Entities / Interfaces / Repository Contracts)       │
└─────────────────────────────────────────────────────────────────┘
安裝與設定1. 配置設定（appsettings.json）配置動態模組資料夾和安全性憑證設定：JSON{
  "ModularSettings": {
    "PluginsPath": "plugins",
    "TrustedThumbprint": "YOUR_COMPANY_CERTIFICATE_THUMBPRINT_HERE"
  }
}
2. 依賴注入註冊（Program.cs）在應用程式啟動期間註冊模組化基礎架構服務：C#using SGSFramework.ModulePlugin.Systems.Module.Loaders;
using PhysLIMS.API.OpenAPI;

var builder = WebApplication.CreateBuilder(args);

// Register controllers and dynamic modules
builder.Services.AddControllers();
builder.Services.AddModularModules(builder.Configuration);

// Register OpenAPI Document Transformer
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<DynamicControllerDocumentTransformer>();
});

var app = builder.Build();

// Configure the modular HTTP pipeline and execute dynamic module initializers
await app.UseModularModulesAsync();

app.MapControllers();
app.Run();
模組開發標準若要建立一個與相容的可插拔模組PhysLIMS.ApiInfrastructure，請IModuleInitializer在模組專案中實作該介面：介面規範C#namespace SGSFramework.ModulePlugin.Abstractions;

public interface IModuleInitializer
{
    string ModuleName { get; }
    void RegisterDependencies(IServiceCollection services, IConfiguration configuration);
    Task OnApplicationConfigureAsync(IApplicationBuilder app);
}
模組實作範例C#using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.ModulePlugin.Abstractions;

namespace SGSFramework.Modules.SampleModule;

public class SampleModuleInitializer : IModuleInitializer
{
    public string ModuleName => "SampleModule";

    public void RegisterDependencies(IServiceCollection services, IConfiguration configuration)
    {
        // Register module-specific services
        services.AddScoped<ISampleService, SampleService>();
    }

    public Task OnApplicationConfigureAsync(IApplicationBuilder app)
    {
        // Module-specific middleware configuration
        return Task.CompletedTask;
    }
}
安全性和模組隔離數位簽章驗證所有放入目標插件目錄的程序集在載入前都會經過檢查。未簽署的程序集或無效的簽章雜湊值將被拒絕，並自動移至該plugins/quarantine目錄。動態模組卸載要在運行時安全地卸載模組上下文：C#ModuleLoaderExtensions.UnloadModule("SampleModule");
異常與故障排除錯誤代碼/症狀可能原因解決InvalidOperationException: TrustedThumbprint not configured缺少配置密鑰appsettings.json。新增ModularSettings:TrustedThumbprint包含有效指紋字串的條目。Module fails to load under IIS上下文工作目錄不符。請確保使用最新版本ModuleLoaderExtensions；路徑解析嚴格依賴AppContext.BaseDirectory。Controller Action 404 Not Found程式集缺少 Controller 命名規格或簽章失敗。確保控制器類別繼承自ControllerBase，以Controller前綴結尾，並通過數字驗證。