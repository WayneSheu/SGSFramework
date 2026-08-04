using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace SGSFramework.Core.Extensions
{
    /// <summary>
    /// Extension methods for the <see cref="IHostBuilder"/> interface. 
    /// 基礎架構設定擴充方法。
    /// </summary>
    public static class HostExtensions
    {


        /// ==========================================
        /// DI容器安全驗證設定
        /// ==========================================
        /// 強制在「開發環境」與「測試階段」開啟 Scope 驗證，防止生命週期錯亂
        /// <summary>
        /// Configures the host to use production security defaults, including scope validation and build-time validation.
        /// </summary>
        /// <param name="builder"></param> 
        public static void AddDIContainerValidation(this WebApplicationBuilder builder)
        { 
            builder.Host.UseDefaultServiceProvider((context, options) =>
            {
                // 開發環境一律強制開啟，生產環境由您決定 (或一律開啟以確保最高穩定性)
                bool isDevelopment = context.HostingEnvironment.IsDevelopment();
                options.ValidateScopes = true; // 生產環境建議持續開啟以維護穩定性
                options.ValidateOnBuild = isDevelopment || true; // 這裡可根據是否對啟動速度極度敏感來調整
            });
        }

        ///// <summary>
        ///// 透過註冊模組、執行遷移和確保控制器一致性來初始化模組化系統。
        ///// </summary>
        ///// <param name="host"></param>
        ///// <returns></returns>
        //public static async Task<IHost> InitializeModularSystemAsync(this IHost host)
        //{
        //    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ModularSystemInitialization");
        //    using (var scope = host.Services.CreateScope())
        //    {
        //        var provider = scope.ServiceProvider;
        //        var registry = provider.GetRequiredService<IModuleRegistry>();
        //        var lifecycleService = provider.GetRequiredService<ModuleLifecycleService>();
        //        var repo = provider.GetRequiredService<IDynamicControllerRepository<ControllerMetadata>>();
        //        var assemblies = provider.GetServices<ModuleAssemblyContainer>();

        //        logger.LogInformation(">>> 開始執行各功能模組初始化...");

    
        //        // 1. 模組註冊與遷移
        //        // 查詢所有已註冊的模組初始化器
        //        var modules = ModuleLoaderExtensions.GetAllInitializers();
        //        foreach (var module in modules)
        //        {
        //            try
        //            {


        //                /// 1. 註冊模組到模組註冊表
        //                registry.RegisterModule(module);

        //                //// 2. [新增] 主動檢查並執行遷移
        //                // 假設模組有註冊其 MigrationService 到 DI 中
        //                using (var moduleScope = host.Services.CreateScope())
        //                {
        //                    //// 嘗試從該模組的服務範圍內獲取遷移服務
        //                    var migrationService = moduleScope.ServiceProvider.GetService<IMigrationService>();
        //                    if (migrationService != null)
        //                    {

        //                        await migrationService.DiagnosticMigrations();


        //                        //先檢查 Pending Migrations，確認是否有落差
        //                        var pending = await migrationService.GetPendingMigrationsAsync();
        //                        var pendingList = pending.ToList();

        //                        if (pendingList.Any())
        //                        {
        //                            logger.LogInformation(">>> 發現模組 {Name} 有 {Count} 個待處理遷移...", module.ModuleName, pendingList.Count);
        //                            try
        //                            {
        //                                // 執行遷移
        //                                await migrationService.MigrateAsync();

        //                                logger.LogInformation(">>> 模組 {Name} 資料庫遷移成功。", module.ModuleName);

        //                            }
        //                            catch (MigrationException mex)
        //                            {
        //                                // 這裡可以進行更細緻的錯誤處理，例如記錄詳細的遷移失敗資訊
        //                                logger.LogError(mex, ">>> 模組 {Name} 遷移失敗: {Message}", module.ModuleName, mex.Message);
        //                                throw; // 重新拋出異常以中斷啟動流程
        //                            }

        //                        }
        //                        else
        //                        {
        //                            logger.LogInformation(">>> 模組 {Name} 遷移已是最新狀態。", module.ModuleName);
        //                        }
        //                    }

        //                }


        //                // 2. 執行模組初始化邏輯 (包含資料庫遷移)
        //                await lifecycleService.RegisterAndInitializeAsync(module, (IApplicationBuilder)host);
                        

        //                logger.LogInformation(">>> 模組 {Name} 初始化完成。", module.ModuleName);
        //            }
        //            catch (Exception ex)
        //            {
        //                // 這裡整合您處理 MigrationException 的深度遞迴邏輯
        //                LogModuleException(ex, module.ModuleName);
        //                throw; // 中斷系統啟動，確保安全性
        //            }
        //        }

        //        foreach (var container in assemblies)
        //        {
        //            // 呼叫您封裝好的 RegisterControllerToDbAsync
        //            // 注意：模組名稱需從 Assembly 的 Attribute 或預設命名空間取得
        //            string moduleName = container.Assembly.GetName().Name ?? "UnknownModule";
        //            await ModuleLoaderExtensions.RegisterModuleToDbAsync(container.Assembly, moduleName, provider);
        //            //await ModuleLoaderExtensions.RegisterControllerToDbAsync(container.Assembly, moduleName, repo);
        //        }


        //        //執行路由一致性檢查
        //        logger.LogInformation(">>> 正在進行系統路由一致性檢查...");

        //        //以確保檔案系統與資料庫的路由狀態同步
        //        await EnsureControllerConsistency(provider);

        //        logger.LogInformation(">>> 系統路由一致性已確認。");
        //    }

        //    return host;
        //}




        ///// <summary>
        ///// 確保檔案系統 (Plugins 目錄) 與 資料庫 (ControllerMetadata 表) 兩者狀態同步的關鍵防禦機制。
        ///// 執行時機建議:
        ///// 應用程式啟動時 (Program.cs)：在 AddModularModules 完成後，執行此函數進行初始化檢查。
        ///// 模組熱重載(Hot Reload) 時：若您實作了動態載入模組的功能，在每個 Load 或 Unload 操作後，應主動呼叫此函數進行局部的狀態刷新。
        ///// 
        ///// </summary>
        ///// <param name="serviceProvider"></param>
        ///// <returns></returns>
        //public static async Task EnsureControllerConsistency(IServiceProvider serviceProvider)
        //{
        //    using var scope = serviceProvider.CreateScope();
        //    var sp = scope.ServiceProvider;
        //    var unloader = sp.GetRequiredService<IModuleUnloader>();
        //    var controllerRepo = sp.GetRequiredService<IDynamicControllerRepository<ControllerMetadata>>();

        //    //將所有名稱轉為小寫並 Trim，避免大小寫不一致問題
        //    var currentModules = ModuleLoaderExtensions.GetLoadedModuleNames()
        //                                               .Select(m => m.ToLowerInvariant().Trim())
        //                                               .ToHashSet();


        //    var allControllers = await controllerRepo.GetActiveControllersAsync();

        //    // 2. 篩選出需要管理的插件模組
        //    var registeredPluginModules = allControllers
        //        .Select(x => x.ModuleName.ToLowerInvariant().Trim())
        //        .Where(name => name.StartsWith("ses.modules.")) 
        //        .Distinct();
        //    // 3. 執行Except
        //    var deadModules = registeredPluginModules.Except(currentModules).ToList();

        //    if (!deadModules.Any())
        //    {
        //        Log.Information(">>> 一致性檢查完成，未發現需要清理的殭屍模組。");
        //    }
        //    else
        //    {
        //        Log.Warning(">>> 發現殭屍模組: {Count} 個", deadModules.Count);
        //        // 執行卸載...
        //        foreach (var moduleName in deadModules)
        //        {
        //            Log.Warning(">>> [自動化修復] 偵測到殭屍插件模組: {Module}, 正在卸載...", moduleName);
        //            await unloader.UnloadModuleAsync(moduleName);
        //        }
        //    }
        //}

        //private static void LogModuleException(Exception ex, string moduleName)
        //{
        //    // 使用 Log.Fatal 確保此嚴重錯誤被記錄到所有儲存媒介
        //    Log.Fatal(">>> [Critical] 模組 {Name} 初始化發生嚴重異常: {Msg}", moduleName, ex.Message);

        //    var inner = ex.InnerException;
        //    int level = 1;
        //    while (inner != null)
        //    {
        //        // 增加日誌級別控制，並記錄精確的異常型別
        //        Log.Error("  └─ 錯誤層級 [{Level}] | 類型: {Type} | 訊息: {Msg}",
        //            level++, inner.GetType().Name, inner.Message);

        //        if (inner is AggregateException aggEx)
        //        {
        //            foreach (var innerEx in aggEx.InnerExceptions)
        //            {
        //                Log.Error("      └─ Aggregate 子錯誤: {Msg}", innerEx.Message);
        //            }
        //        }
        //        inner = inner.InnerException;
        //    }
        //}

    }
}
