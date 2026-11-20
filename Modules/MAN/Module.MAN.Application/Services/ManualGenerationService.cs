using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGS.Modules.MAN.Application.Contracts;
using SGS.Modules.MAN.Application.Options;
using SGS.Modules.MAN.Application.Rules;
using SGS.Modules.MAN.Infrastructure.Dbcontexts;
using SGS.Modules.MAN.Infrastructure.Entities;

namespace SGS.Modules.MAN.Application.Services;

/// <summary>AI 生成作業的受理與查詢：驗證請求、建立作業紀錄並放入佇列（實際生成由背景工作者執行）。</summary>
public sealed class ManualGenerationService(
    MANDbContext db,
    GenerationQueue queue,
    IOllamaClient ollama,
    IOptionsMonitor<ManOptions> options,
    TimeProvider clock)
{
    public async Task<GenerationStatusDto> GetStatusAsync(CancellationToken ct)
    {
        var opt = options.CurrentValue;
        if (!opt.Enabled)
            return new GenerationStatusDto(false, false, opt.VisionModel, false, queue.Length, "AI 生成尚未啟用（Manual:Enabled=false）。");

        var health = await ollama.GetHealthAsync(ct).ConfigureAwait(false);
        var installed = health.Models.Any(m => IsSameModel(m, opt.VisionModel));
        var message = !health.Reachable ? health.Error
            : installed ? null
            : $"Ollama 尚未安裝視覺模型 {opt.VisionModel}，請先執行 ollama pull。";
        return new GenerationStatusDto(true, health.Reachable, opt.VisionModel, installed, queue.Length, message);
    }

    public async Task<ManResult<GenerationJobDto>> EnqueueAsync(string actor, GenerateTopicRequest request, byte[] image, CancellationToken ct)
    {
        var opt = options.CurrentValue;
        if (!opt.Enabled) return ManError.Conflict("MAN.GENERATION_DISABLED", "AI 生成尚未啟用，請聯絡系統管理員設定 Manual:Enabled。");
        if (!GenerationRules.IsAllowedOllamaUrl(opt.OllamaBaseUrl))
            return ManError.Conflict("MAN.OLLAMA_URL_NOT_ALLOWED", "Ollama 位址必須是本機或內網位址，已拒絕送出截圖。");

        if (GenerationRules.ValidateRequest(request.FeatureKey, request.Title, request.ApiKeyword) is { } requestError)
            return ManError.Validation(requestError);
        if (GenerationRules.ValidateImage(image, opt.MaxImageBytes) is { } imageError)
            return ManError.Validation(imageError);

        var key = request.FeatureKey.Trim();
        if (!request.Overwrite && await db.ManualTopics.AnyAsync(t => t.FeatureKey == key, ct).ConfigureAwait(false))
            return ManError.Conflict("MAN.FEATURE_KEY_DUPLICATE", $"功能代碼 {key} 已存在；若要以 AI 草稿覆寫，請勾選「覆寫既有主題」。");

        if (await db.GenerationJobs.AnyAsync(j => j.FeatureKey == key && (j.Status == GenerationJobStatus.Pending || j.Status == GenerationJobStatus.Running), ct)
                .ConfigureAwait(false))
            return ManError.Conflict("MAN.JOB_IN_PROGRESS", $"功能代碼 {key} 已有生成作業進行中。");

        var job = new GenerationJob
        {
            Id = Guid.NewGuid(),
            FeatureKey = key,
            Title = request.Title.Trim(),
            Model = opt.VisionModel,
            Overwrite = request.Overwrite,
            Status = GenerationJobStatus.Pending,
            CreatedBy = actor,
            CreatedAtUtc = clock.GetUtcNow()
        };
        db.GenerationJobs.Add(job);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var item = new GenerationWorkItem(job.Id, request with { FeatureKey = key, Title = job.Title }, image, actor);
        if (!queue.TryEnqueue(item))
        {
            job.Status = GenerationJobStatus.Failed;
            job.Message = "生成佇列已滿，請稍後再試。";
            job.FinishedAtUtc = clock.GetUtcNow();
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return ManError.Conflict("MAN.QUEUE_FULL", "生成佇列已滿，請稍後再試。");
        }

        return ManResult<GenerationJobDto>.Success(ToDto(job));
    }

    public async Task<PagedResult<GenerationJobDto>> SearchAsync(GenerationJobQueryRequest request, CancellationToken ct)
    {
        request ??= new GenerationJobQueryRequest();
        var (pageIndex, pageSize) = ManualTopicRules.NormalizePaging(request.PageIndex, request.PageSize);

        var query = db.GenerationJobs.AsNoTracking();
        if (request.Status is { } status) query = query.Where(j => j.Status == status);

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var rows = await query.OrderByDescending(j => j.CreatedAtUtc)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct).ConfigureAwait(false);
        return new PagedResult<GenerationJobDto>(rows.Select(ToDto).ToList(), total, pageIndex, pageSize);
    }

    public async Task<ManResult<GenerationJobDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var job = await db.GenerationJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct).ConfigureAwait(false);
        return job is null ? ManError.NotFound("找不到指定的生成作業。") : ManResult<GenerationJobDto>.Success(ToDto(job));
    }

    /// <summary>Ollama 模型名稱可省略 :latest；比對時忽略此差異。</summary>
    private static bool IsSameModel(string installed, string configured) =>
        string.Equals(Normalize(installed), Normalize(configured), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string name) => name.EndsWith(":latest", StringComparison.OrdinalIgnoreCase) ? name[..^7] : name;

    internal static GenerationJobDto ToDto(GenerationJob j) => new(
        j.Id, j.FeatureKey, j.Title, j.Status, j.Model, j.Overwrite, j.TopicId, j.Message, j.DurationMs, j.CreatedBy, j.CreatedAtUtc, j.FinishedAtUtc);
}
