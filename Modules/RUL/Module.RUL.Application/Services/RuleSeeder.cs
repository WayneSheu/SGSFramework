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

    public const string NoSelfRolesName = "不得指派自己的角色";
    public const string NoSelfPermissionsName = "不得指派自己的直接權限";
    public const string GrantCeilingName = "不得授與超過自己的權限";

    public static async Task SeedAsync(RULDbContext db, TimeProvider clock, CancellationToken ct = default)
    {
        await EnsureAsync(db, clock, new RuleDefinition
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
            Priority = 100
        }, ct).ConfigureAwait(false);

        // 授權上限：指派角色／直接權限時，只能授與自己已擁有的權限（系統管理員不受限）。
        // 欄位 GrantBeyondOwn 寫入 ForceDeny＝啟用檢查；經審批停用此規則即不檢查。
        await EnsureAsync(db, clock, new RuleDefinition
        {
            Name = GrantCeilingName,
            Description = "指派使用者角色、直接權限、實驗室專屬權限時，不得授與超過自己所擁有的權限；系統管理員不受限。停用本規則即不檢查。",
            Domain = RuleDomain.Permission,
            Kind = RuleKind.Column,
            TargetEntity = "ApplicationUser",
            FieldName = "GrantBeyondOwn",
            RoleName = "GlobalPolicy",
            ConditionExpr = "!user.IsAdmin",
            OutputPayload = "{\"errorMessage\":\"依動態規則，您不得授與超過自己所擁有的權限。\"}",
            ReadAccess = FieldAccessLevel.Allow,
            WriteAccess = FieldAccessLevel.ForceDeny,
            Priority = 900
        }, ct).ConfigureAwait(false);

        // 職責分離：任何人都不得變更自己的角色與直接權限（Column 規則，寫入 ForceDeny）。
        // 在角色權限 Maker-Checker 停用時仍可擋住自我授權；需例外時由管理者經審批停用規則。
        foreach (var (name, field, desc) in new[]
        {
            (NoSelfRolesName, "Roles", "使用者不得指派或變更自己的角色。"),
            (NoSelfPermissionsName, "DirectPermissions", "使用者不得指派或變更自己的直接權限（含實驗室專屬權限）。")
        })
        {
            await EnsureAsync(db, clock, new RuleDefinition
            {
                Name = name,
                Description = desc,
                Domain = RuleDomain.Permission,
                Kind = RuleKind.Column,
                TargetEntity = "ApplicationUser",
                FieldName = field,
                RoleName = "GlobalPolicy",
                ConditionExpr = "target.Id == user.UserGuid",
                OutputPayload = "{\"errorMessage\":\"依動態規則，您不得變更此使用者的角色或直接權限（例如不可指派給自己）。\"}",
                ReadAccess = FieldAccessLevel.Allow,
                WriteAccess = FieldAccessLevel.ForceDeny,
                Priority = 1000
            }, ct).ConfigureAwait(false);
        }
    }

    private static async Task EnsureAsync(RULDbContext db, TimeProvider clock, RuleDefinition rule, CancellationToken ct)
    {
        if (await db.Rules.AnyAsync(r => r.Name == rule.Name, ct).ConfigureAwait(false)) return;

        var now = clock.GetUtcNow();
        rule.Status = RuleStatus.Active;
        rule.Version = 1;
        rule.CreatedBy = SystemActor;
        rule.CreatedAtUtc = now;
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
