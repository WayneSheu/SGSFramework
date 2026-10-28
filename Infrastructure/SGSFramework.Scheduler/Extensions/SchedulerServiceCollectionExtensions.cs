using Enterprise.HttpSdk.Extensions;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Configurations;
using SGSFramework.Scheduler.Abstractions;
using SGSFramework.Scheduler.Hosting;
using SGSFramework.Scheduler.Http;
using SGSFramework.Scheduler.Jobs;
using SGSFramework.Scheduler.Maintenance;
using SGSFramework.Scheduler.Options;
using SGSFramework.Scheduler.Services;

namespace SGSFramework.Scheduler.Extensions;

/// <summary>
/// 排程框架 (Hangfire) 註冊。
/// <para>
/// 架構：Hangfire 週期作業 → SchedulerApiClient（系統帳號 Client Credentials 權杖）→ 內部 API（[RequiresSystemScope]）。
/// 作業本身不直接操作資料，所有授權、稽核（CreatedBy = 系統帳號 ID）、撤銷檢查都沿用 API 既有管線；
/// 停用 / 凍結系統帳號即可立即中止所有排程的實際執行。
/// </para>
/// </summary>
public static class SchedulerServiceCollectionExtensions
{
    /// <summary>
    /// 註冊排程框架與內建作業（資料庫完整 / 差異備份、定期權限覆核）。
    /// Scheduler:Enabled = false 時僅註冊備份服務與作業清單（內部端點仍可由外部排程器呼叫），不啟動 Hangfire。
    /// </summary>
    public static IServiceCollection AddSGSScheduler(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddValidatedOptions<SchedulerOptions>(SchedulerOptions.SectionName)
            .Validate(o => !o.Enabled || o.ApiBaseAddress is { IsAbsoluteUri: true },
                "Scheduler:ApiBaseAddress 必須為絕對網址（排程作業呼叫內部 API 的位址）。")
            .Validate(o => !o.Enabled ||
                           (!string.IsNullOrWhiteSpace(o.SystemAccount.ClientId) && !string.IsNullOrWhiteSpace(o.SystemAccount.ClientSecret)),
                "Scheduler:SystemAccount 需設定 ClientId 與 ClientSecret（金鑰請以環境變數 Scheduler__SystemAccount__ClientSecret 注入）。");

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IDatabaseBackupService, SqlServerDatabaseBackupService>();
        services.TryAddScoped<ILogCleanupService, LogCleanupService>();

        // 作業類別一律註冊（供作業清單與 DI 啟動驗證），其相依的 SchedulerApiClient 亦須一律可解析；
        // 停用時具名 HttpClient 不掛系統帳號 Handler，但作業也不會被排入執行
        services.AddHttpClient();
        services.TryAddTransient<SchedulerApiClient>();

        // 內建週期作業（預設值，可由 Scheduler:Jobs:{Id} 覆寫 Enabled / Cron / TimeZone）
        services.AddRecurringJob<DatabaseFullBackupJob>(DatabaseFullBackupJob.JobId, "資料庫完整備份", "0 2 * * *");
        services.AddRecurringJob<DatabaseDifferentialBackupJob>(DatabaseDifferentialBackupJob.JobId, "資料庫差異備份", "0 13 * * *", enabledByDefault: false);
        services.AddRecurringJob<AccessReviewCampaignJob>(AccessReviewCampaignJob.JobId, "定期權限覆核", "0 8 1 1,4,7,10 *");
        services.AddRecurringJob<LogCleanupJob>(LogCleanupJob.JobId, "日誌清除", "30 3 * * *");

        var options = configuration.GetSection(SchedulerOptions.SectionName).Get<SchedulerOptions>() ?? new SchedulerOptions();
        if (!options.Enabled)
        {
            return services;
        }

        // 系統帳號 (M2M) HttpClient：權杖換發 / 快取 / 401 自動重新換發、CorrelationId、重試與斷路器
        services.AddSystemAccountHttpClient(
            SchedulerApiClient.HttpClientName,
            SchedulerApiClient.AccountName,
            client =>
            {
                client.BaseAddress = options.ApiBaseAddress;
                client.Timeout = Timeout.InfiniteTimeSpan; // 由 SchedulerApiClient 依作業決定逾時
            },
            account =>
            {
                configuration.GetSection($"{SchedulerOptions.SectionName}:SystemAccount").Bind(account);
                account.TokenEndpoint ??= new Uri(options.ApiBaseAddress!, "api/v1/system-accounts/token");
            });

        services.AddHangfire((sp, hangfire) =>
        {
            var scheduler = sp.GetRequiredService<IOptions<SchedulerOptions>>().Value;
            var connections = sp.GetRequiredService<IOptions<PersistentConnectionOptions>>().Value;

            if (scheduler.Storage.PrepareSchemaIfNecessary)
            {
                InstallSchema(connections, scheduler.Storage.SchemaName, sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SchedulerServiceCollectionExtensions)));
            }

            // 執行紀錄 + 最終失敗告警（每次嘗試一筆，見 JobRunRecorderFilter）
            hangfire.UseFilter(ActivatorUtilities.CreateInstance<JobRunRecorderFilter>(sp));

            hangfire
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(
                    () => new SqlConnection(connections.DefaultConnection),
                    new SqlServerStorageOptions
                    {
                        SchemaName = scheduler.Storage.SchemaName,
                        PrepareSchemaIfNecessary = false, // 執行期帳號不具 DDL 權限，結構由 InstallSchema 以部署帳號建立
                        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                        QueuePollInterval = TimeSpan.Zero,
                        UseRecommendedIsolationLevel = true,
                        DisableGlobalLocks = true
                    });
        });

        if (options.ServerEnabled)
        {
            services.AddHangfireServer(server =>
            {
                server.ServerName = $"{Environment.MachineName}:{Environment.ProcessId}";
                server.Queues = options.Server.Queues is { Length: > 0 } queues ? queues : ["default"];
                if (options.Server.WorkerCount > 0) server.WorkerCount = options.Server.WorkerCount;
            });
        }

        services.AddScoped<IScheduledJobManager, ScheduledJobManager>();
        services.AddHostedService<RecurringJobRegistrar>();
        return services;
    }

    /// <summary>
    /// 註冊自訂週期作業。作業類別以 DI Scope 建立，建議僅以 <see cref="SchedulerApiClient"/> 呼叫標註 RequiresSystemScope 的內部端點。
    /// </summary>
    /// <param name="id">作業 ID（Scheduler:Jobs 的 Key）</param>
    /// <param name="displayName">顯示名稱</param>
    /// <param name="defaultCron">預設 Cron（5 碼）</param>
    /// <param name="enabledByDefault">未設定覆寫時是否啟用</param>
    public static IServiceCollection AddRecurringJob<TJob>(
        this IServiceCollection services, string id, string displayName, string defaultCron, bool enabledByDefault = true)
        where TJob : class, IRecurringJob
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCron);

        services.TryAddScoped<TJob>();
        services.AddSingleton(new RecurringJobDescriptor(id, typeof(TJob), displayName, defaultCron, enabledByDefault));
        return services;
    }

    /// <summary>
    /// 對應 Hangfire Dashboard（Scheduler:Dashboard:Enabled = true 時）。需置於 UseAuthentication / UseAuthorization 之後。
    /// </summary>
    public static IEndpointRouteBuilder MapSGSSchedulerDashboard(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<SchedulerOptions>>().Value;
        if (!options.Enabled || !options.Dashboard.Enabled) return endpoints;

        var readOnly = options.Dashboard.ReadOnly;
        endpoints.MapHangfireDashboard(options.Dashboard.Path, new DashboardOptions
        {
            DashboardTitle = "排程管理",
            Authorization = [new SchedulerDashboardAuthorizationFilter(options.Dashboard.LocalRequestsOnly)],
            IsReadOnlyFunc = _ => readOnly,
            DisplayStorageConnectionString = false,
            AppPath = null
        });

        return endpoints;
    }

    /// <summary>
    /// 以部署帳號 (MigrationConnection，具 db_ddladmin) 建立 / 升級 Hangfire 資料表；未設定時回退 DefaultConnection。
    /// 失敗僅記錄錯誤：資料表已由 DBA 建立時，執行期仍可正常運作。
    /// </summary>
    private static void InstallSchema(PersistentConnectionOptions connections, string schemaName, ILogger logger)
    {
        var connectionString = string.IsNullOrWhiteSpace(connections.MigrationConnection)
            ? connections.DefaultConnection
            : connections.MigrationConnection;

        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            SqlServerObjectsInstaller.Install(connection, schemaName, enableHeavyMigrations: false);
            logger.LogInformation("[Scheduler] Hangfire 資料表結構已就緒（Schema：{Schema}）。", schemaName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Scheduler] 建立 Hangfire 資料表失敗（Schema：{Schema}），請確認部署帳號具 db_ddladmin 或由 DBA 執行 Scheduler 授權腳本。", schemaName);
        }
    }
}
