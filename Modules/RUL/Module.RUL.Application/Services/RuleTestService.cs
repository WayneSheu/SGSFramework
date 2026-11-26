using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.RUL.Application.Contracts;
using SGS.Modules.RUL.Infrastructure.Dbcontexts;
using SGS.Modules.RUL.Infrastructure.Entities;
using SGSFramework.Core.Rules;

namespace SGS.Modules.RUL.Application.Services;

/// <summary>規則測試案例：維護與批次執行（以規則目前內容試跑，不影響線上快取）。</summary>
public sealed class RuleTestService(RULDbContext db, IRuleTester tester, TimeProvider clock)
{
    public async Task<IReadOnlyList<TestCaseDto>> ListAsync(long ruleId, CancellationToken ct)
    {
        var rows = await db.TestCases.AsNoTracking().Where(t => t.RuleId == ruleId).OrderBy(t => t.Id).ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(t => new TestCaseDto(t.Id, t.RuleId, t.Name, t.UserJson, t.TargetJson, t.ExpectedMatch)).ToList();
    }

    public async Task<RulResult<TestCaseDto>> AddAsync(string actor, long ruleId, TestCaseInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100) return RulResult<TestCaseDto>.Failure(RulError.Validation("案例名稱必填且不可超過 100 字。"));
        if (!IsJson(input.UserJson ?? "{}") || !IsJson(input.TargetJson)) return RulResult<TestCaseDto>.Failure(RulError.Validation("使用者與目標資料必須是合法 JSON。"));
        if (!await db.Rules.AnyAsync(r => r.Id == ruleId, ct).ConfigureAwait(false)) return RulResult<TestCaseDto>.Failure(RulError.NotFound("規則不存在。"));

        var e = new RuleTestCase
        {
            RuleId = ruleId, Name = input.Name.Trim(), UserJson = input.UserJson ?? "{}", TargetJson = input.TargetJson,
            ExpectedMatch = input.ExpectedMatch, CreatedBy = actor, CreatedAtUtc = clock.GetUtcNow()
        };
        db.TestCases.Add(e);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return RulResult<TestCaseDto>.Success(new TestCaseDto(e.Id, e.RuleId, e.Name, e.UserJson, e.TargetJson, e.ExpectedMatch));
    }

    public async Task<RulResult<bool>> DeleteAsync(long id, CancellationToken ct)
    {
        var e = await db.TestCases.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (e is null) return RulResult<bool>.Failure(RulError.NotFound("測試案例不存在。"));
        db.TestCases.Remove(e);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return RulResult<bool>.Success(true);
    }

    public async Task<RulResult<IReadOnlyList<TestCaseRunDto>>> RunAsync(long ruleId, CancellationToken ct)
    {
        var rule = await db.Rules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == ruleId, ct).ConfigureAwait(false);
        if (rule is null) return RulResult<IReadOnlyList<TestCaseRunDto>>.Failure(RulError.NotFound("規則不存在。"));
        var snap = RuleSnapshotProvider.ToSnapshot(rule);
        var cases = await db.TestCases.AsNoTracking().Where(t => t.RuleId == ruleId).OrderBy(t => t.Id).ToListAsync(ct).ConfigureAwait(false);
        IReadOnlyList<TestCaseRunDto> results = cases.Select(t =>
        {
            var o = tester.Test(snap, RuleAdminService.ParseUser(t.UserJson), t.TargetJson);
            return new TestCaseRunDto(t.Id, t.Name, o.Success && o.Matched == t.ExpectedMatch, o.Matched, t.ExpectedMatch, o.Error);
        }).ToList();
        return RulResult<IReadOnlyList<TestCaseRunDto>>.Success(results);
    }

    private static bool IsJson(string s)
    {
        try { using var _ = JsonDocument.Parse(s); return true; } catch (JsonException) { return false; }
    }
}
