
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.AuditLog.Interceptors;
using SGSFramework.Persistent.Abstractions.Dbcontexts;
using SGSFramework.Persistent.Abstractions.ScriptRunners;
using SGSFramework.Persistent.Configurations.Options;
using SGSFramework.Persistent.Converters;
using SGSFramework.Persistent.Extensions;
using SGSFramework.Persistent.Interceptors;
using SGSFramework.Persistent.Repositories.Hierarchy;
using SGSFramework.Persistent.Repositories.Traditionals;
using SGSFramework.Persistent.Repositories.Vector;
using SGSFramework.Persistent.ScriptRunners;
using IdentityOptions = Microsoft.AspNetCore.Identity.IdentityOptions;

namespace SGSFramework.Persistent.Extensions
{
    public static class PersistentExtensions
    {
       

        #region Dbcontext 註冊方法 for WebApi 

        // ═════════════════════════════════════════════
        // 1. 基礎泛型：不需要 Identity 的 DbContext
        // ═════════════════════════════════════════════
        /// <summary>
        /// 註冊任意 DbContext，繼承自 BaseDbContext。
        /// 適用於WebApi 專用
        /// use: services.AddDbContextWithOptions<MyDbContext>(Configuration, "mySchema");
        /// </summary>
        public static IServiceCollection AddDbContextWithOptions<TContext>(
            this IServiceCollection services,
            IConfiguration configuration,
            string schema = "core",
            Action<DbContextOptionsBuilder>? extraOptions = null)
            where TContext : DbContext
        {
            EnsureOptionsRegistered(services, configuration);

            // 1. 註冊 DatabaseInitializer 服務
            services.AddTransient<IDatabaseInitializer, DatabaseInitializer>();

            services.AddDbContext<TContext>((sp, options) =>
            {
                var db = sp.GetRequiredService<IOptionsSnapshot<PersistentOptions>>().Value.DatabaseSettings;

                options.UseSqlServer(db.ConnectionString, sql =>
                {
                    ConfigureSqlServer(sql, db);
                });

                options.UseCustomSchema(schema);

                if (db.EnableSensitiveDataLogging)
                    options.EnableSensitiveDataLogging();

                // 核心整合：動態從當前 Scope 容器中取出 AuditInterceptor 並掛載
                var interceptor = sp.GetRequiredService<AuditInterceptor>();
                options.AddInterceptors(interceptor)
                
                .AddInterceptors(sp.GetRequiredService<EditableAttributeInterceptor>())
                //從 ServiceProvider 注入軟刪除攔截器並啟用
                .AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>());

                extraOptions?.Invoke(options);
            });

            services.AddScoped(typeof(ITraditionalRepository<,>), typeof(TraditionalRepository<,>));
            return services;
        }

        // ═════════════════════════════════════════════
        // 2. Identity 泛型：需要 Identity 的 DbContext
        // ═════════════════════════════════════════════
        /// <summary>
        /// 註冊帶有 ASP.NET Core Identity 的 DbContext。
        /// TContext 必須繼承 BaseIdentityDbContext。
        /// </summary>
        /// <typeparam name="TContext">繼承 BaseIdentityDbContext 的具體 DbContext</typeparam>
        /// <typeparam name="TUser">Identity User 實體，繼承 IdentityUser</typeparam>
        /// <typeparam name="TRole">Identity Role 實體，繼承 IdentityRole</typeparam>
        /// <typeparam name="TKey">主鍵型別，通常為 Guid 或 string</typeparam>
        /// <param name="schema">資料庫 Schema，預設 "core"</param>
        /// <param name="configureIdentity">可選：進一步調整 IdentityOptions（密碼強度、Lockout...）</param>
        /// <param name="extraOptions">可選：進一步調整 DbContextOptionsBuilder</param>
        public static IServiceCollection AddIdentityDbContextWithOptions<TContext, TUser, TRole, TKey>(
            this IServiceCollection services,
            IConfiguration configuration,
            string schema = "core",
            Action<IdentityOptions>? configureIdentity = null,
            Action<DbContextOptionsBuilder>? extraOptions = null)
            where TContext : BaseIdentityDbContext<TUser, TRole, TKey, TContext>
            where TUser : IdentityUser<TKey>
            where TRole : IdentityRole<TKey>
            where TKey : IEquatable<TKey>
        {
            EnsureOptionsRegistered(services, configuration);

            // 1. 註冊 DatabaseInitializer 服務
            services.AddTransient<IDatabaseInitializer, DatabaseInitializer>();

            // 1. 註冊 DbContext
            services.AddDbContext<TContext>((sp, options) =>
            {
                var db = sp.GetRequiredService<IOptionsSnapshot<PersistentOptions>>().Value.DatabaseSettings;

                options.UseSqlServer(db.ConnectionString, sql =>
                {
                    ConfigureSqlServer(sql, db);
                });

                options.UseCustomSchema(schema);
                // 都會從該次實例化請求的 Scope 中抽取出對應的攔截器
                var interceptor = sp.GetRequiredService<AuditInterceptor>();
                options.AddInterceptors(interceptor)
                      //
                      .AddInterceptors(sp.GetRequiredService<EditableAttributeInterceptor>())
                     //從 ServiceProvider 注入軟刪除攔截器並啟用
                    .AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>());
                //
                if (db.EnableSensitiveDataLogging)
                    options.EnableSensitiveDataLogging();
       
                extraOptions?.Invoke(options);
            });

            // 2. 讀取 Identity 設定（從 appsettings 對應）
            var identitySection = configuration
                .GetSection(PersistentOptions.SectionName)
                .GetSection("IdentitySettings");

            // 3. 註冊 Identity，並綁定到上面的 DbContext
            var identityBuilder = services
                .AddIdentity<TUser, TRole>(identityOptions =>
                {
                    // 從 appsettings 讀取預設值
                    identityOptions.Password.RequireDigit =
                    identitySection.GetValue("Password:RequireDigit", true);
                    identityOptions.Password.RequiredLength =
                    identitySection.GetValue("Password:RequiredLength", 8);
                    identityOptions.Password.RequireNonAlphanumeric =
                    identitySection.GetValue("Password:RequireNonAlphanumeric", false);

                    identityOptions.Lockout.MaxFailedAccessAttempts =
                    identitySection.GetValue("Lockout:MaxFailedAccessAttempts", 5);
                    identityOptions.Lockout.DefaultLockoutTimeSpan =
                    TimeSpan.FromMinutes(
                        identitySection.GetValue("Lockout:DefaultLockoutTimeSpan", 15));

                    identityOptions.User.RequireUniqueEmail =
                    identitySection.GetValue("User:RequireUniqueEmail", true);

                    // 呼叫方可以覆蓋任何設定
                    configureIdentity?.Invoke(identityOptions);
                })
                    .AddEntityFrameworkStores<TContext>()
                    .AddDefaultTokenProviders();

            // 註冊開放泛型 (Open Generics)，使系統能動態衍生出如 ITraditionalRepository<BlazorDbContext, ActivityData>
            services.AddScoped(typeof(ITraditionalRepository<,>), typeof(TraditionalRepository<,>));


            return services;
        }


        #endregion

        #region 3.DbContext 註冊方法 for Blazor (Pooled Factory)
        /// <summary>
        /// for Blazor 專用的註冊方法，使用 Pooled Factory 以適應 Blazor 的 DI 生命週期。
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="schema"></param>
        /// <param name="extraOptions"></param>
        /// <returns></returns>
        /// <exception cref="OptionsValidationException"></exception>
        // ══════════════════════════════════════════════════════════════
        // 2. Blazor 專用：Pooled Factory 整合 (完美避開生命週期衝突)
        // ══════════════════════════════════════════════════════════════
        public static IServiceCollection AddDbContextForBlazor<TContext>(
            this IServiceCollection services,
            IConfiguration configuration,
            string schema = "core",
            Action<DbContextOptionsBuilder>? extraOptions = null)
            where TContext : DbContext
        {
            EnsureOptionsRegistered(services, configuration);

            //註冊 DatabaseInitializer 服務
            services.AddTransient<IDatabaseInitializer, DatabaseInitializer>();

            services.AddPooledDbContextFactory<TContext>((sp, options) =>
            {
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                var settings = sp.GetRequiredService<IOptionsMonitor<PersistentOptions>>().CurrentValue.DatabaseSettings;

                if (string.IsNullOrWhiteSpace(settings.ConnectionString))
                {
                    throw new ArgumentNullException(nameof(settings.ConnectionString), "資料庫連線字串不可為空。");
                }

                options.UseSqlServer(settings.ConnectionString, sql =>
                {
                    sql.UseHierarchyId();
                    sql.EnableRetryOnFailure();
                });

                options.UseCustomSchema(schema);

                if (env.EnvironmentName == "Development" && settings.EnableSensitiveDataLogging)
                {
                    options.EnableSensitiveDataLogging(true);
                    options.LogTo(Console.WriteLine, LogLevel.Information);
                    options.EnableDetailedErrors();
                }

                // 核心整合：不論 Factory 本身多長壽，每次透過工廠生成 DbContext 時，
                // 都會從該次實例化請求的 Scope 中抽取出對應的攔截器
                var interceptor = sp.GetRequiredService<AuditInterceptor>();
                options.AddInterceptors(interceptor)
                .AddInterceptors(sp.GetRequiredService<EditableAttributeInterceptor>())
                //從 ServiceProvider 注入軟刪除攔截器並啟用
                .AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>());

                extraOptions?.Invoke(options);
            });

            // 為了相容性，依然提供 Scoped 註冊
            services.AddScoped(sp =>
                sp.GetRequiredService<IDbContextFactory<TContext>>().CreateDbContext());

            services.AddScoped(typeof(ITraditionalRepository<,>), typeof(TraditionalRepository<,>));
            return services;
        }


        // ══════════════════════════════════════════════════════════════
        // 2. Identity 泛型：適用於需要 使用者權限管理 的模組
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 註冊帶有 ASP.NET Core Identity 的 DbContext (支援 Blazor Pooled Factory)。
        /// </summary>
        public static IServiceCollection AddIdentityDbContextForBlazor<TContext, TUser, TRole, TKey>(
            this IServiceCollection services,
            IConfiguration configuration,
            string schema = "core",
            Action<IdentityOptions>? configureIdentity = null,
            Action<DbContextOptionsBuilder>? extraOptions = null)
            where TContext : BaseIdentityDbContext<TUser, TRole, TKey, TContext>
            where TUser : IdentityUser<TKey>
            where TRole : IdentityRole<TKey>
            where TKey : IEquatable<TKey>
        {
            // 1. 確保 Options 已註冊
            EnsureOptionsRegistered(services, configuration);
            // 1. 註冊 DatabaseInitializer 服務
            services.AddTransient<IDatabaseInitializer, DatabaseInitializer>();

            // 2. 註冊 Pooled DbContext Factory
            services.AddPooledDbContextFactory<TContext>((sp, options) =>
            {
                // 1. 註冊 DatabaseInitializer 服務
                services.AddTransient<IDatabaseInitializer, DatabaseInitializer>();

                var env = sp.GetRequiredService<IWebHostEnvironment>();
                // 使用 Monitor 以支援單例工廠中的配置讀取
                var settings = sp.GetRequiredService<IOptionsMonitor<PersistentOptions>>().CurrentValue.DatabaseSettings;

                // 手動驗證：防止啟動時因連線字串空值而崩潰
                if (string.IsNullOrWhiteSpace(settings.ConnectionString))
                {
                    throw new OptionsValidationException(
                        nameof(PersistentOptions),
                        typeof(PersistentOptions),
                        new[] { "IdentityDbContext 的 ConnectionString 缺失。" });
                }

                options.UseSqlServer(settings.ConnectionString, sql =>
                {
                    // 呼叫私有共用配置方法 (處理 HierarchyId, Retry, Timeout)
                    ConfigureSqlServer(sql, settings);
                });
 


                // 套用自定義 Schema (例如 Identity 專用表)
                options.UseCustomSchema(schema);
                // 都會從該次實例化請求的 Scope 中抽取出對應的攔截器
                var interceptor = sp.GetRequiredService<AuditInterceptor>();
                options.AddInterceptors(interceptor)
                .AddInterceptors(sp.GetRequiredService<EditableAttributeInterceptor>())
                //從 ServiceProvider 注入軟刪除攔截器並啟用
                .AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>());
                // 環境感知：開發環境配置
                if (env.IsDevelopment())
                {
                    options.EnableDetailedErrors();
                    if (settings.EnableSensitiveDataLogging)
                        options.EnableSensitiveDataLogging();
                }

                extraOptions?.Invoke(options);
            });

            // 3. 註冊 Scoped DbContext (相容傳統注入與 SSR)
            services.AddScoped(sp =>
                sp.GetRequiredService<IDbContextFactory<TContext>>().CreateDbContext());

            // 4. 配置 Identity 引擎
            // 從 appsettings 讀取 Identity 設定區段 (假設位於 PersistentOptions 下)
            var identitySection = configuration.GetSection($"{PersistentOptions.SectionName}:IdentitySettings");

            services.AddIdentity<TUser, TRole>(options =>
            {
                // 自動綁定常見安全設定
                options.Password.RequireDigit = identitySection.GetValue("Password:RequireDigit", true);
                options.Password.RequiredLength = identitySection.GetValue("Password:RequiredLength", 8);
                options.User.RequireUniqueEmail = identitySection.GetValue("User:RequireUniqueEmail", true);

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(
                    identitySection.GetValue("Lockout:DefaultLockoutTimeSpanMinutes", 15));
                options.Lockout.MaxFailedAccessAttempts =
                    identitySection.GetValue("Lockout:MaxFailedAccessAttempts", 5);

                // 允許外部 Action 進行微調
                configureIdentity?.Invoke(options);
            })
            .AddEntityFrameworkStores<TContext>() // 關鍵：綁定到上述的 TContext
            .AddDefaultTokenProviders();


            // 註冊智慧欄位路由服務
            // 1. 將 IQueryRoutingService 註冊為單例 (Singleton)
            //    建構函式現在只依賴全域的 IServiceProvider，可完美成功解構
            services.AddSingleton<IQueryRoutingService, QueryRoutingService>();
            // 開放泛型註冊：現在 GenericVectorRepository 的建構函式只依賴 TDbContext，可以完美解析！
            services.AddScoped(typeof(ITraditionalRepository<,>), typeof(TraditionalRepository<,>));
            // 這裡同時註冊了 IHierarchicalRepository 的實作，確保層級結構化倉儲也能在 Blazor 中使用
            services.AddScoped(typeof(IHierarchicalRepository<,>), typeof(HierarchicalRepository<,>));

            return services;
        }

        #endregion


        // ─────────────────────────────────────────────
        // 私有：確保 PersistentOptions 已被註冊（冪等）
        // ─────────────────────────────────────────────
        private static void EnsureOptionsRegistered(
            IServiceCollection services,
            IConfiguration configuration)
        {
            //try
            //{
                // 檢查 DI 容器是否已經註冊過該 Options，避免重複執行導致效能損耗
                if (services.Any(sd => sd.ServiceType == typeof(IConfigureOptions<PersistentOptions>)))
                {
                    return;
                }

                // 2. 註冊自動索引慣例 (Convention) - 整個系統只需執行一次
                // 這樣不論是呼叫 AddDbContextForBlazor 或 AddDbContextWithOptions 都會生效
                services.AddSingleton<AutoIndexConvention>();

                // 1. 綁定 JSON 區段
                // 2. 啟用 Data Annotation 驗證 (如 [Required], [Range])
                // 3. 啟用啟動時驗證 (ValidateOnStart)
                services.AddOptions<PersistentOptions>()
                    .Bind(configuration.GetSection(PersistentOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                // 註冊 Interceptor 至 DI 容器 (Scoped 生命週期)
                services.AddScoped<SoftDeleteInterceptor>();
                // 註冊Entity 不可變更
                services.AddScoped<EditableAttributeInterceptor>();

                // 同步註冊為一般 Configure而非手動 Bind，確保 IOptions<T> 也能正常運作
                services.Configure<PersistentOptions>(configuration.GetSection(PersistentOptions.SectionName));
            //}
            //catch (OptionsValidationException ex)
            //{
            //    // 專門攔截配置錯誤，將詳細的欄位錯誤印出來
            //    Log.Fatal("配置驗證失敗！欄位：{Failures}", string.Join(", ", ex.Failures));
            //    throw ;
            //}

        }

        // ─────────────────────────────────────────────
        // 私有：共用的 SqlServer 選項配置，避免重複
        // ─────────────────────────────────────────────
        private static void ConfigureSqlServer(
            SqlServerDbContextOptionsBuilder sql,
            DatabaseOptions db)
        {
            if (db.UseHierarchyId)
                sql.UseHierarchyId();

            sql.EnableRetryOnFailure(
                maxRetryCount: db.MaxRetryCount,// 最大重試次數
                maxRetryDelay: TimeSpan.FromSeconds(db.MaxRetryDelaySeconds),// 重試之間的最大延遲時間
                errorNumbersToAdd: null);// 需要添加的錯誤編號，null 表示不添加任何特定錯誤編號;可選：自訂額外需要重試的 SQL 錯誤代碼
                                        //預設處理的暫時性錯誤
                                        //此策略內部維護了一個特定錯誤代碼清單，當 SQL Server 拋出包含下列錯誤碼的 SqlException 時，才會觸發自動重試：
                                        //1205：死結（Deadlock）
                                        //40613：資料庫暫時無法使用（Database temporary unavailable）
                                        //40501：服務忙碌中（Service is busy）
                                        //49918 / 49919：執行批次處理或要求過多被拒（Throttling）
                                        //10928 / 10929：資源限制（如超過連接數、記憶體限制）
                                        //注意： 諸如語法錯誤（重試幾次都不會成功）或權限不足（如 Login Failed）等非暫時性錯誤，此策略會立刻拋出異常，絕不重試。

            sql.CommandTimeout(db.CommandTimeout);
        }


    }
}