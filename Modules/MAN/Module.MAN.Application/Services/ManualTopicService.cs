using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.MAN.Application.Contracts;
using SGS.Modules.MAN.Application.Rules;
using SGS.Modules.MAN.Infrastructure.Dbcontexts;
using SGS.Modules.MAN.Infrastructure.Entities;

namespace SGS.Modules.MAN.Application.Services;

/// <summary>操作手冊主題維護：查詢、建立、更新、發布／取消發布、刪除與 Tooltip 字典輸出。</summary>
public sealed class ManualTopicService(MANDbContext db, TimeProvider clock)
{
    public async Task<PagedResult<ManualTopicDto>> SearchAsync(ManualTopicQueryRequest request, CancellationToken ct)
    {
        request ??= new ManualTopicQueryRequest();
        var (pageIndex, pageSize) = ManualTopicRules.NormalizePaging(request.PageIndex, request.PageSize);

        var query = db.ManualTopics.AsNoTracking();
        if (request.Status is { } status) query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(request.ModuleName)) query = query.Where(t => t.ModuleName == request.ModuleName);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(t => t.Title.Contains(kw) || t.FeatureKey.Contains(kw) || t.Tooltip.Contains(kw));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var rows = await query.OrderBy(t => t.SortOrder).ThenBy(t => t.FeatureKey)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
        return new PagedResult<ManualTopicDto>(rows.Select(ToDto).ToList(), total, pageIndex, pageSize);
    }

    public async Task<ManResult<ManualTopicDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var topic = await db.ManualTopics.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        return topic is null ? ManError.NotFound("找不到指定的手冊主題。") : ManResult<ManualTopicDto>.Success(ToDto(topic));
    }

    public async Task<ManResult<ManualTopicDto>> CreateAsync(string actor, SaveManualTopicRequest request, CancellationToken ct)
    {
        if (ManualTopicRules.Validate(request) is { } error) return ManError.Validation(error);

        var key = request.FeatureKey.Trim();
        if (await db.ManualTopics.AnyAsync(t => t.FeatureKey == key, ct).ConfigureAwait(false))
            return ManError.Conflict("MAN.FEATURE_KEY_DUPLICATE", $"功能代碼 {key} 已存在。");

        var topic = new ManualTopic
        {
            Id = Guid.NewGuid(),
            FeatureKey = key,
            CreatedBy = actor,
            CreatedAtUtc = clock.GetUtcNow()
        };
        Apply(topic, request);
        db.ManualTopics.Add(topic);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ManResult<ManualTopicDto>.Success(ToDto(topic));
    }

    public async Task<ManResult<ManualTopicDto>> UpdateAsync(string actor, Guid id, SaveManualTopicRequest request, CancellationToken ct)
    {
        if (ManualTopicRules.Validate(request) is { } error) return ManError.Validation(error);

        var topic = await db.ManualTopics.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (topic is null) return ManError.NotFound("找不到指定的手冊主題。");
        if (!string.Equals(topic.FeatureKey, request.FeatureKey.Trim(), StringComparison.Ordinal))
            return ManError.Validation("功能代碼建立後不可變更（前端以代碼綁定提示）。");

        Apply(topic, request);

        // 已發布內容被修改：退回草稿，須重新審核發布，避免未審核內容對全站生效
        if (topic.Status == ManualTopicStatus.Published)
        {
            topic.Status = ManualTopicStatus.Draft;
            topic.PublishedAtUtc = null;
            topic.PublishedBy = null;
        }

        topic.UpdatedBy = actor;
        topic.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ManResult<ManualTopicDto>.Success(ToDto(topic));
    }

    public async Task<ManResult<ManualTopicDto>> PublishAsync(string actor, Guid id, CancellationToken ct)
    {
        var topic = await db.ManualTopics.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (topic is null) return ManError.NotFound("找不到指定的手冊主題。");
        if (ManualTopicRules.ValidateForPublish(topic.StepsMarkdown) is { } error) return ManError.Validation(error);

        var now = clock.GetUtcNow();
        topic.Status = ManualTopicStatus.Published;
        topic.PublishedAtUtc = now;
        topic.PublishedBy = actor;
        topic.UpdatedBy = actor;
        topic.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ManResult<ManualTopicDto>.Success(ToDto(topic));
    }

    public async Task<ManResult<ManualTopicDto>> UnpublishAsync(string actor, Guid id, CancellationToken ct)
    {
        var topic = await db.ManualTopics.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (topic is null) return ManError.NotFound("找不到指定的手冊主題。");

        topic.Status = ManualTopicStatus.Draft;
        topic.PublishedAtUtc = null;
        topic.PublishedBy = null;
        topic.UpdatedBy = actor;
        topic.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ManResult<ManualTopicDto>.Success(ToDto(topic));
    }

    public async Task<ManResult<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var topic = await db.ManualTopics.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (topic is null) return ManError.NotFound("找不到指定的手冊主題。");

        db.ManualTopics.Remove(topic);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ManResult<bool>.Success(true);
    }

    /// <summary>已發布主題的 Tooltip 字典（供前端 SmartTooltip 全站使用）；Version 為內容雜湊，供快取比對。</summary>
    public async Task<TooltipDictionaryDto> GetTooltipsAsync(CancellationToken ct)
    {
        var rows = await db.ManualTopics.AsNoTracking()
            .Where(t => t.Status == ManualTopicStatus.Published)
            .OrderBy(t => t.FeatureKey)
            .Select(t => new { t.FeatureKey, t.Tooltip })
            .ToListAsync(ct).ConfigureAwait(false);

        var dict = rows.ToDictionary(r => r.FeatureKey, r => r.Tooltip);
        var raw = string.Join('\n', rows.Select(r => $"{r.FeatureKey}\t{r.Tooltip}"));
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16];
        return new TooltipDictionaryDto(dict, version);
    }

    private static void Apply(ManualTopic topic, SaveManualTopicRequest request)
    {
        topic.Title = request.Title.Trim();
        topic.ModuleName = string.IsNullOrWhiteSpace(request.ModuleName) ? null : request.ModuleName.Trim();
        topic.Tooltip = request.Tooltip.Trim();
        topic.StepsMarkdown = request.StepsMarkdown ?? string.Empty;
        topic.Precautions = request.Precautions ?? string.Empty;
        topic.SortOrder = request.SortOrder;
    }

    private static ManualTopicDto ToDto(ManualTopic t) => new(
        t.Id, t.FeatureKey, t.Title, t.ModuleName, t.Tooltip, t.StepsMarkdown, t.Precautions,
        t.SortOrder, t.Status, t.Source, t.PublishedAtUtc, t.PublishedBy, t.UpdatedAtUtc);
}
