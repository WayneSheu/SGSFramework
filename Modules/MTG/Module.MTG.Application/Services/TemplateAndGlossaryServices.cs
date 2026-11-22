using Microsoft.EntityFrameworkCore;
using SGS.Modules.MTG.Application.Contracts;
using SGS.Modules.MTG.Application.Rules;
using SGS.Modules.MTG.Infrastructure.Dbcontexts;
using SGS.Modules.MTG.Infrastructure.Entities;

namespace SGS.Modules.MTG.Application.Services;

/// <summary>場景 Prompt 模板維護；內建四種場景由種子資料建立，可調整提示詞但不可刪除。</summary>
public sealed class PromptTemplateService(MTGDbContext db, TimeProvider clock)
{
    public const int MaxPromptLength = 8000;

    public async Task<IReadOnlyList<PromptTemplateDto>> ListAsync(bool activeOnly, CancellationToken ct)
    {
        var query = db.PromptTemplates.AsNoTracking().AsQueryable();
        if (activeOnly) query = query.Where(t => t.IsActive);
        return await query.OrderBy(t => t.ScenarioCode).Select(t => ToDto(t)).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<MtgResult<PromptTemplateDto>> CreateAsync(string actor, string scenarioCode, SavePromptTemplateRequest request, CancellationToken ct)
    {
        var code = scenarioCode?.Trim().ToLowerInvariant();
        if (!ScenarioDefaults.IsValidCode(code)) return MtgResult<PromptTemplateDto>.Fail(MtgErrors.Validation("場景代碼僅限小寫英數字與連字號，且不得超過 50 字。"));
        var error = Validate(request);
        if (error is not null) return MtgResult<PromptTemplateDto>.Fail(error);
        if (await db.PromptTemplates.AnyAsync(t => t.ScenarioCode == code, ct).ConfigureAwait(false))
            return MtgResult<PromptTemplateDto>.Fail(MtgErrors.Conflict($"場景代碼「{code}」已存在。"));

        var entity = new PromptTemplate
        {
            Id = Guid.NewGuid(),
            ScenarioCode = code!,
            DisplayName = request.DisplayName.Trim(),
            MapPrompt = request.MapPrompt.Trim(),
            ReducePrompt = request.ReducePrompt.Trim(),
            IsActive = request.IsActive,
            Version = 1,
            CreatedBy = actor,
            CreatedAtUtc = clock.GetUtcNow()
        };
        db.PromptTemplates.Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return MtgResult<PromptTemplateDto>.Ok(ToDto(entity));
    }

    public async Task<MtgResult<PromptTemplateDto>> UpdateAsync(string actor, string scenarioCode, SavePromptTemplateRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return MtgResult<PromptTemplateDto>.Fail(error);

        var entity = await db.PromptTemplates.FirstOrDefaultAsync(t => t.ScenarioCode == scenarioCode, ct).ConfigureAwait(false);
        if (entity is null) return MtgResult<PromptTemplateDto>.Fail(MtgErrors.NotFound("場景模板"));

        entity.DisplayName = request.DisplayName.Trim();
        entity.MapPrompt = request.MapPrompt.Trim();
        entity.ReducePrompt = request.ReducePrompt.Trim();
        entity.IsActive = request.IsActive;
        entity.Version++;
        entity.UpdatedBy = actor;
        entity.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return MtgResult<PromptTemplateDto>.Ok(ToDto(entity));
    }

    private static MtgError? Validate(SavePromptTemplateRequest? r)
    {
        if (r is null || string.IsNullOrWhiteSpace(r.DisplayName) || r.DisplayName.Trim().Length > 100)
            return MtgErrors.Validation("顯示名稱為必填且不得超過 100 字。");
        if (string.IsNullOrWhiteSpace(r.MapPrompt) || r.MapPrompt.Length > MaxPromptLength)
            return MtgErrors.Validation($"Map 指令為必填且不得超過 {MaxPromptLength} 字。");
        if (string.IsNullOrWhiteSpace(r.ReducePrompt) || r.ReducePrompt.Length > MaxPromptLength)
            return MtgErrors.Validation($"Reduce 骨架為必填且不得超過 {MaxPromptLength} 字。");
        return null;
    }

    private static PromptTemplateDto ToDto(PromptTemplate t) =>
        new(t.Id, t.ScenarioCode, t.DisplayName, t.MapPrompt, t.ReducePrompt, t.IsActive, t.Version);
}

/// <summary>術語表維護。</summary>
public sealed class GlossaryService(MTGDbContext db, TimeProvider clock)
{
    public async Task<PagedResult<GlossaryEntryDto>> SearchAsync(GlossaryQueryRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = db.GlossaryEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(g => g.Term.Contains(keyword) || (g.Variants != null && g.Variants.Contains(keyword)));
        }
        if (request.IsActive is { } active) query = query.Where(g => g.IsActive == active);

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query.OrderBy(g => g.Term).Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(g => new GlossaryEntryDto(g.Id, g.Term, g.Variants, g.Category, g.IsActive))
            .ToListAsync(ct).ConfigureAwait(false);
        return new PagedResult<GlossaryEntryDto>(items, total, pageIndex, pageSize);
    }

    /// <summary>啟用中的術語（供桌面端快取，在本機即時做同音字校正）。</summary>
    public async Task<IReadOnlyList<GlossaryTermDto>> GetActiveTermsAsync(CancellationToken ct)
    {
        var rows = await db.GlossaryEntries.AsNoTracking().Where(g => g.IsActive).OrderBy(g => g.Term)
            .Select(g => new { g.Term, g.Variants }).ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(r => new GlossaryTermDto(r.Term, GlossaryCorrector.ParseVariants(r.Variants))).ToList();
    }

    public async Task<MtgResult<GlossaryEntryDto>> CreateAsync(string actor, SaveGlossaryEntryRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return MtgResult<GlossaryEntryDto>.Fail(error);

        var term = request.Term.Trim();
        if (await db.GlossaryEntries.AnyAsync(g => g.Term == term, ct).ConfigureAwait(false))
            return MtgResult<GlossaryEntryDto>.Fail(MtgErrors.Conflict($"術語「{term}」已存在。"));

        var entity = new GlossaryEntry
        {
            Id = Guid.NewGuid(),
            Term = term,
            Variants = NormalizeVariants(request.Variants),
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            IsActive = request.IsActive,
            CreatedBy = actor,
            CreatedAtUtc = clock.GetUtcNow()
        };
        db.GlossaryEntries.Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return MtgResult<GlossaryEntryDto>.Ok(ToDto(entity));
    }

    public async Task<MtgResult<GlossaryEntryDto>> UpdateAsync(string actor, Guid id, SaveGlossaryEntryRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return MtgResult<GlossaryEntryDto>.Fail(error);

        var entity = await db.GlossaryEntries.FirstOrDefaultAsync(g => g.Id == id, ct).ConfigureAwait(false);
        if (entity is null) return MtgResult<GlossaryEntryDto>.Fail(MtgErrors.NotFound("術語"));

        var term = request.Term.Trim();
        if (await db.GlossaryEntries.AnyAsync(g => g.Term == term && g.Id != id, ct).ConfigureAwait(false))
            return MtgResult<GlossaryEntryDto>.Fail(MtgErrors.Conflict($"術語「{term}」已存在。"));

        entity.Term = term;
        entity.Variants = NormalizeVariants(request.Variants);
        entity.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedBy = actor;
        entity.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return MtgResult<GlossaryEntryDto>.Ok(ToDto(entity));
    }

    public async Task<MtgResult<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.GlossaryEntries.FirstOrDefaultAsync(g => g.Id == id, ct).ConfigureAwait(false);
        if (entity is null) return MtgResult<bool>.Fail(MtgErrors.NotFound("術語"));
        db.GlossaryEntries.Remove(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return MtgResult<bool>.Ok(true);
    }

    private static MtgError? Validate(SaveGlossaryEntryRequest? r)
    {
        if (r is null || string.IsNullOrWhiteSpace(r.Term) || r.Term.Trim().Length > 100)
            return MtgErrors.Validation("標準詞為必填且不得超過 100 字。");
        if (r.Variants is { Length: > 1000 }) return MtgErrors.Validation("誤聽詞不得超過 1000 字。");
        if (r.Category is { Length: > 50 }) return MtgErrors.Validation("類別不得超過 50 字。");
        return null;
    }

    /// <summary>整理誤聽詞：去除空白與重複，並排除與標準詞相同者。</summary>
    private static string? NormalizeVariants(string? variants)
    {
        var list = GlossaryCorrector.ParseVariants(variants).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return list.Count == 0 ? null : string.Join(GlossaryCorrector.VariantSeparator, list);
    }

    private static GlossaryEntryDto ToDto(GlossaryEntry g) => new(g.Id, g.Term, g.Variants, g.Category, g.IsActive);
}

/// <summary>種子資料：啟動時補齊四種內建場景模板（已存在者不覆蓋，避免蓋掉使用者調整）。</summary>
public sealed class MtgSeeder(MTGDbContext db, TimeProvider clock)
{
    public async Task SeedAsync(CancellationToken ct)
    {
        var existing = await db.PromptTemplates.Select(t => t.ScenarioCode).ToListAsync(ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        foreach (var def in ScenarioDefaults.All.Where(d => !existing.Contains(d.Code)))
        {
            db.PromptTemplates.Add(new PromptTemplate
            {
                Id = Guid.NewGuid(),
                ScenarioCode = def.Code,
                DisplayName = def.DisplayName,
                MapPrompt = def.MapPrompt,
                ReducePrompt = def.ReducePrompt,
                IsActive = true,
                Version = 1,
                CreatedBy = "system",
                CreatedAtUtc = now
            });
        }

        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
