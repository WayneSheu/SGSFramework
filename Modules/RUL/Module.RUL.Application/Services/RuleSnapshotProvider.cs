using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.RUL.Application.Rules;
using SGS.Modules.RUL.Infrastructure.Dbcontexts;
using SGS.Modules.RUL.Infrastructure.Entities;
using SGSFramework.Core.Rules;

namespace SGS.Modules.RUL.Application.Services;

/// <summary>
/// Core <see cref="IRuleSnapshotProvider"/> 實作（Singleton）：從 rul.Rules 讀取「已核准、啟用、在有效期間」的規則並快取。
/// 核准生效時立即失效；並設短 TTL（60 秒）作為多節點部署的最終一致保底。
/// </summary>
public sealed class RuleSnapshotProvider(IServiceScopeFactory scopes, IMemoryCache cache, TimeProvider clock) : IRuleSnapshotProvider
{
    private const string Key = "rul:active-rules";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public IReadOnlyList<RuleSnapshot> GetActiveRules() =>
        cache.GetOrCreate(Key, e =>
        {
            e.AbsoluteExpirationRelativeToNow = Ttl;
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RULDbContext>();
            var now = clock.GetUtcNow();
            return (IReadOnlyList<RuleSnapshot>)db.Rules.AsNoTracking()
                .Where(r => r.Status == RuleStatus.Active)
                .ToList()
                .Where(r => RuleInputRules.IsEffective(r.EffectiveFromUtc, r.EffectiveToUtc, now))
                .Select(ToSnapshot)
                .ToList();
        })!;

    public void Invalidate() => cache.Remove(Key);

    public static RuleSnapshot ToSnapshot(RuleDefinition r) => new()
    {
        Id = r.Id,
        Version = r.Version,
        Domain = r.Domain,
        Kind = r.Kind,
        TargetEntity = r.TargetEntity,
        FieldName = r.FieldName,
        RoleName = r.RoleName,
        ConditionExpr = r.ConditionExpr,
        ActionExpr = r.ActionExpr,
        OutputPayload = r.OutputPayload,
        ReadAccess = r.ReadAccess,
        WriteAccess = r.WriteAccess,
        Risk = r.Risk,
        Priority = r.Priority
    };
}
