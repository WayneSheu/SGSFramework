using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.RUL.Infrastructure.Dbcontexts;
using SGS.Modules.RUL.Infrastructure.Entities;
using SGSFramework.Core.Rules;

namespace SGS.Modules.RUL.Application.Services;

/// <summary>
/// 預設規則種子（以名稱判斷、只在不存在時建立）：由系統自動核准（Version 1，ApprovedBy = system），
/// 之後的修改仍須經 Maker-Checker。管理者停用或修改後，啟動時不會被覆蓋。
/// </summary>
public static class RuleSeeder
{
    public const string SystemActor = "system";

    public const string MoMembersOnlyName = "全區實驗室管理員僅能維護自己的成員";

    public static async Task SeedAsync(RULDbContext db, TimeProvider clock, CancellationToken ct = default)
    {
        if (await db.Rules.AnyAsync(r => r.Name == MoMembersOnlyName, ct).ConfigureAwait(false)) return;

        var now = clock.GetUtcNow();
        var rule = new RuleDefinition
        {
            Name = MoMembersOnlyName,
            Description = "使用者管理：全區實驗室管理員（MO）只能看到並維護與自己共同歸屬實驗室的成員（含自己）；其他角色與系統管理員不受影響。",
            Domain = RuleDomain.Permission,
            Kind = RuleKind.Row,
            TargetEntity = "ApplicationUser",
            RoleName = "全區實驗室管理員",
            ConditionExpr = "user.MemberUserIds.Contains(row.Id)",
            ReadAccess = FieldAccessLevel.Allow,
            WriteAccess = FieldAccessLevel.Allow,
            Priority = 100,
            Status = RuleStatus.Active,
            Version = 1,
            CreatedBy = SystemActor,
            CreatedAtUtc = now
        };
        db.Rules.Add(rule);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        db.Versions.Add(new RuleVersion
        {
            RuleId = rule.Id,
            Version = 1,
            SnapshotJson = JsonSerializer.Serialize(RuleAdminService.ToInput(rule), RuleAdminService.Json),
            ApprovedBy = SystemActor,
            CreatedBy = SystemActor,
            CreatedAtUtc = now
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
