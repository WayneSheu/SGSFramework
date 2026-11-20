using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.MAN.Application.Options;
using SGS.Modules.MAN.Application.Rules;
using SGS.Modules.MAN.Infrastructure.Dbcontexts;
using SGS.Modules.MAN.Infrastructure.Entities;

namespace SGS.Modules.MAN.Application.Services;

/// <summary>
/// 背景工作者：依序（一次一個，避免同時載入多個模型耗盡記憶體）處理生成佇列。
/// 流程：讀取規格上下文 → 呼叫本機視覺模型 → 解析 → 以「草稿」寫入手冊主題（AI 內容須人工審核後才可發布）。
/// </summary>
public sealed class ManualGenerationWorker(
    GenerationQueue queue,
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<ManualGenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverInterruptedJobsAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            GenerationWorkItem item;
            try
            {
                item = await queue.DequeueAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await ProcessAsync(item, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 單一作業失敗不得讓整個工作者結束
                logger.LogError(ex, "[MAN] 生成作業 {JobId} 發生未預期例外。", item.JobId);
            }
        }
    }

    /// <summary>服務重啟時，上次未完成的作業已無截圖可處理（截圖不落地），標記為失敗讓使用者重新送出。</summary>
    private async Task RecoverInterruptedJobsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MANDbContext>();
            var stale = await db.GenerationJobs
                .Where(j => j.Status == GenerationJobStatus.Pending || j.Status == GenerationJobStatus.Running)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var job in stale)
            {
                job.Status = GenerationJobStatus.Failed;
                job.Message = "服務重新啟動，作業中斷，請重新送出。";
                job.FinishedAtUtc = clock.GetUtcNow();
            }

            if (stale.Count > 0) await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 資料表尚未建立（遷移尚未完成）等情況不應阻擋服務啟動
            logger.LogWarning(ex, "[MAN] 無法復原中斷的生成作業。");
        }
    }

    private async Task ProcessAsync(GenerationWorkItem item, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<MANDbContext>();

        var job = await db.GenerationJobs.FirstOrDefaultAsync(j => j.Id == item.JobId, ct).ConfigureAwait(false);
        if (job is null) return;

        var sw = Stopwatch.StartNew();
        job.Status = GenerationJobStatus.Running;
        job.StartedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        try
        {
            var apiContext = await sp.GetRequiredService<IOpenApiContextProvider>()
                .GetContextAsync(item.Request.ApiKeyword, ct).ConfigureAwait(false);

            var prompt = ManualPromptBuilder.BuildUserPrompt(item.Request.Title, item.Request.FeatureKey, item.Request.ModuleName, apiContext);
            var ollama = sp.GetRequiredService<IOllamaClient>();
            var maxAttempts = Math.Clamp(sp.GetRequiredService<IOptionsMonitor<ManOptions>>().CurrentValue.MaxAttempts, 1, 5);

            // 小型模型偶爾會輸出空內容或壞掉的 JSON（thinking 版模型尤其如此）：自動重試；逾時與連線錯誤不重試
            GeneratedManual? manual = null;
            string? parseError = null;
            for (var attempt = 1; attempt <= maxAttempts && manual is null; attempt++)
            {
                var raw = await ollama.ChatWithImageAsync(ManualPromptBuilder.SystemPrompt, prompt, item.Image, ct).ConfigureAwait(false);
                manual = ManualOutputParser.TryParse(raw, out parseError);
                if (manual is null) logger.LogWarning("[MAN] 生成作業 {JobId} 第 {Attempt}/{Max} 次輸出無法使用：{Reason}", item.JobId, attempt, maxAttempts, parseError);
            }

            if (manual is null) throw new InvalidOperationException($"{parseError ?? "無法解析模型輸出。"}（已重試 {maxAttempts} 次，請換一張更清楚的截圖或稍後再試）");

            job.TopicId = await UpsertDraftAsync(db, item, manual, ct).ConfigureAwait(false);
            job.Status = GenerationJobStatus.Succeeded;
            job.Message = apiContext.Length > 0 ? "已生成草稿（已參考後端規格）。" : "已生成草稿（僅依截圖）。";

            // 耗時與完成時間必須與「成功」狀態同一次寫入，否則輪詢會看到已完成但沒有耗時的作業
            job.DurationMs = sw.ElapsedMilliseconds;
            job.FinishedAtUtc = clock.GetUtcNow();

            // 主題與作業狀態同一交易寫入；失敗時由下方 catch 清除追蹤並改記為失敗
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[MAN] 生成作業 {JobId} 失敗。", item.JobId);

            // 丟棄尚未成功寫入的主題變更，重新載入作業後記為失敗
            db.ChangeTracker.Clear();
            job = await db.GenerationJobs.FirstAsync(j => j.Id == item.JobId, CancellationToken.None).ConfigureAwait(false);
            job.StartedAtUtc ??= clock.GetUtcNow();
            job.TopicId = null;
            job.Status = GenerationJobStatus.Failed;
            // 僅顯示我們自己產生的訊息；未預期例外不外洩內部細節
            job.Message = ex is InvalidOperationException or TimeoutException
                ? GenerationRules.SafeMessage(ex.Message)
                : "生成失敗，請稍後再試或查看系統日誌。";
        }
        finally
        {
            sw.Stop();
            job.DurationMs = sw.ElapsedMilliseconds;
            job.FinishedAtUtc = clock.GetUtcNow();
            job.UpdatedBy = item.Actor;
            job.UpdatedAtUtc = job.FinishedAtUtc;
        }

        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<Guid> UpsertDraftAsync(MANDbContext db, GenerationWorkItem item, GeneratedManual manual, CancellationToken ct)
    {
        var key = item.Request.FeatureKey;
        var now = clock.GetUtcNow();
        var topic = await db.ManualTopics.FirstOrDefaultAsync(t => t.FeatureKey == key, ct).ConfigureAwait(false);

        if (topic is not null && !item.Request.Overwrite)
            throw new InvalidOperationException($"功能代碼 {key} 已存在，未勾選覆寫。");

        if (topic is null)
        {
            topic = new ManualTopic { Id = Guid.NewGuid(), FeatureKey = key, CreatedBy = item.Actor, CreatedAtUtc = now };
            db.ManualTopics.Add(topic);
        }
        else
        {
            topic.UpdatedBy = item.Actor;
            topic.UpdatedAtUtc = now;
        }

        topic.Title = item.Request.Title;
        topic.ModuleName = string.IsNullOrWhiteSpace(item.Request.ModuleName) ? topic.ModuleName : item.Request.ModuleName.Trim();
        topic.Tooltip = manual.Tooltip;
        topic.StepsMarkdown = manual.StepsMarkdown;
        topic.Precautions = manual.Precautions;
        topic.Source = ManualContentSource.AiGenerated;

        // AI 內容一律回到草稿，須人工審核後才可發布（覆寫已發布主題時同樣退回草稿）
        topic.Status = ManualTopicStatus.Draft;
        topic.PublishedAtUtc = null;
        topic.PublishedBy = null;
        return topic.Id;
    }
}
