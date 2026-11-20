using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SGS.Modules.MAN.Application.Options;
using SGS.Modules.MAN.Application.Rules;
using SGS.Modules.MAN.Application.Services;
using SGSFramework.Core.Configurations;

namespace SGS.Modules.MAN.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>註冊 MAN Application 層：手冊主題維護與 Tooltip 輸出。</summary>
    public static IServiceCollection AddModuleManApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ManualTopicService>();

        // AI 生成：組態啟用時才強制檢查 Ollama 位址（預設關閉，不影響人工維護功能）
        services.AddValidatedOptions<ManOptions>(ManOptions.SectionName)
            .Validate(o => !o.Enabled || GenerationRules.IsAllowedOllamaUrl(o.OllamaBaseUrl),
                "Manual:OllamaBaseUrl 必須是本機或內網位址（loopback／私有網段／單一標籤主機名）。")
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.VisionModel), "Manual:VisionModel 不得為空。");

        services.AddHttpClient(OllamaClient.HttpClientName, c => c.Timeout = Timeout.InfiniteTimeSpan); // 逾時改由每次呼叫的 CancellationToken 控制
        services.AddHttpClient(OpenApiContextProvider.HttpClientName);
        services.AddSingleton<GenerationQueue>(sp => new GenerationQueue(sp.GetRequiredService<IOptions<ManOptions>>().Value.QueueCapacity));
        services.AddScoped<IOllamaClient, OllamaClient>();
        services.AddScoped<IOpenApiContextProvider, OpenApiContextProvider>();
        services.AddScoped<ManualGenerationService>();
        services.AddHostedService<ManualGenerationWorker>();
        return services;
    }
}
