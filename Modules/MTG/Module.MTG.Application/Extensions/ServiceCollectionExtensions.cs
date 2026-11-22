using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SGS.Modules.MTG.Application.Options;
using SGS.Modules.MTG.Application.Rules;
using SGS.Modules.MTG.Application.Services;
using SGSFramework.Core.Configurations;

namespace SGS.Modules.MTG.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>註冊 MTG Application 層：會議維護、模板與術語表、Map-Reduce 摘要與背景工作者。</summary>
    public static IServiceCollection AddModuleMtgApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);

        // AI 紀要：組態啟用時才強制檢查 Ollama 位址與切塊參數（預設關閉，不影響會議與逐字稿維護）
        services.AddValidatedOptions<MtgOptions>(MtgOptions.SectionName)
            .Validate(o => !o.Enabled || InternalUrlRules.IsAllowed(o.OllamaBaseUrl),
                "Meeting:OllamaBaseUrl 必須是本機或內網位址（loopback／私有網段／單一標籤主機名）。")
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.TextModel), "Meeting:TextModel 不得為空。")
            .Validate(o => !o.Enabled || (o.ChunkChars >= 500 && o.OverlapChars >= 0 && o.OverlapChars <= o.ChunkChars / 2),
                "Meeting:ChunkChars 至少 500，且 OverlapChars 不得超過其一半。")
            // 語音轉錄：啟用時才強制檢查轉錄服務位址與上限（預設關閉，不影響貼上逐字稿與產生紀要）
            .Validate(o => !o.Whisper.Enabled || InternalUrlRules.IsAllowed(o.Whisper.BaseUrl),
                "Meeting:Whisper:BaseUrl 必須是本機或內網位址（loopback／私有網段／單一標籤主機名）。")
            .Validate(o => !o.Whisper.Enabled || (!string.IsNullOrWhiteSpace(o.Whisper.Path) && o.Whisper.Path.StartsWith('/')),
                "Meeting:Whisper:Path 必須以 / 開頭。")
            .Validate(o => !o.Whisper.Enabled || o.Whisper.MaxAudioBytes > 0, "Meeting:Whisper:MaxAudioBytes 必須大於 0。");

        services.AddHttpClient(OllamaClient.HttpClientName, c => c.Timeout = Timeout.InfiniteTimeSpan); // 逾時改由每次呼叫的 CancellationToken 控制
        services.AddHttpClient(WhisperHttpEngine.HttpClientName, c => c.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton(sp => new MinutesQueue(sp.GetRequiredService<IOptions<MtgOptions>>().Value.QueueCapacity));
        services.AddSingleton(sp => new TranscriptionQueue(sp.GetRequiredService<IOptions<MtgOptions>>().Value.Whisper.QueueCapacity));
        services.AddScoped<ITranscriptionEngine, WhisperHttpEngine>();
        services.AddScoped<MeetingAudioService>();
        services.AddHostedService<TranscriptionWorker>();
        services.AddScoped<ITextGenerator, OllamaClient>();
        services.AddScoped<MapReduceSummarizer>();
        services.AddScoped<MeetingService>();
        services.AddScoped<ActionItemService>();
        services.AddScoped<PromptTemplateService>();
        services.AddScoped<GlossaryService>();
        services.AddScoped<MtgSeeder>();
        services.AddHostedService<MinutesWorker>();
        return services;
    }
}
