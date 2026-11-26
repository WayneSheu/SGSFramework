using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.RUL.Application.Contracts;
using SGS.Modules.RUL.Application.Rules;
using SGS.Modules.RUL.Application.Services;
using SGS.Modules.RUL.Infrastructure.Dbcontexts;
using SGS.Modules.RUL.Infrastructure.Entities;
using SGSFramework.Core.Rules;
using SGSFramework.RuleEngine;
using Xunit;

namespace SGS.Modules.RUL.Tests;

public class RuleInputRulesTests
{
    private static RuleInput Valid() => new()
    {
        Name = "企業用戶統編必填", Domain = RuleDomain.DynamicForm, Kind = RuleKind.Validation, TargetEntity = "SampleRuleTarget",
        FieldName = "TaxId", ConditionExpr = "target.CustomerType == \"Company\"", OutputPayload = "{\"required\":true}"
    };

    [Fact] public void Valid_Input_Passes() => RuleInputRules.Validate(Valid()).Should().BeNull();

    [Fact] public void Domain_Kind_Mismatch_Fails() =>
        RuleInputRules.Validate(Valid() with { Domain = RuleDomain.Pricing }).Should().Contain("不支援");

    [Fact] public void Column_Requires_Field() =>
        RuleInputRules.Validate(Valid() with { Domain = RuleDomain.Permission, Kind = RuleKind.Column, FieldName = null }).Should().Contain("欄位");

    [Fact] public void Row_Read_Must_Be_Allow_Or_ForceDeny() =>
        RuleInputRules.Validate(Valid() with { Domain = RuleDomain.Permission, Kind = RuleKind.Row, ReadAccess = FieldAccessLevel.Mask }).Should().NotBeNull();

    [Fact] public void Pricing_Requires_Action() =>
        RuleInputRules.Validate(Valid() with { Domain = RuleDomain.Pricing, Kind = RuleKind.Pricing, ActionExpr = null }).Should().Contain("折扣");

    [Fact] public void Invalid_Payload_Json_Fails() =>
        RuleInputRules.Validate(Valid() with { OutputPayload = "{oops" }).Should().Contain("JSON");

    [Fact] public void Effective_Window_Order() =>
        RuleInputRules.Validate(Valid() with { EffectiveFromUtc = DateTimeOffset.UtcNow, EffectiveToUtc = DateTimeOffset.UtcNow.AddDays(-1) }).Should().NotBeNull();

    [Fact] public void IsEffective_Window()
    {
        var now = DateTimeOffset.UtcNow;
        RuleInputRules.IsEffective(null, null, now).Should().BeTrue();
        RuleInputRules.IsEffective(now.AddDays(1), null, now).Should().BeFalse();
        RuleInputRules.IsEffective(null, now.AddDays(-1), now).Should().BeFalse();
    }

    [Theory]
    [InlineData("alice", "Alice", true)]
    [InlineData("alice", "bob", false)]
    public void Self_Approval(string maker, string checker, bool expected) =>
        RuleApprovalRules.IsSelfApproval(maker, checker).Should().Be(expected);
}

public class RuleSeederTests
{
    [Fact]
    public async Task Seed_Is_Idempotent_And_Condition_Is_Valid_For_ApplicationUser()
    {
        var opts = new DbContextOptionsBuilder<RULDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        await using var db = new RULDbContext(opts);
        await RuleSeeder.SeedAsync(db, TimeProvider.System);
        await RuleSeeder.SeedAsync(db, TimeProvider.System);

        var rule = await db.Rules.SingleAsync();
        rule.RoleName.Should().Be("全區實驗室管理員");
        (await db.Versions.CountAsync()).Should().Be(1);

        var catalog = new RuleTargetCatalog().Register(typeof(SGSFramework.Core.Abstractions.Entities.Identities.ApplicationUser));
        new RuleExpressionValidator(catalog).Validate("ApplicationUser", rule.Kind, rule.ConditionExpr).IsValid.Should().BeTrue();
    }
}

public class RuleAdminServiceTests
{
    private static (RuleAdminService Svc, RULDbContext Db, RuleSnapshotProvider Snap) Create()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddMemoryCache();
        services.AddDbContext<RULDbContext>(o => o.UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddDynamicRuleEngine();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RuleSnapshotProvider>();
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<RULDbContext>();
        var snap = new RuleSnapshotProvider(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<IMemoryCache>(), TimeProvider.System);
        var svc = new RuleAdminService(db, sp.GetRequiredService<IRuleExpressionValidator>(), sp.GetRequiredService<IRuleTargetCatalog>(),
            sp.GetRequiredService<IRuleTester>(), snap, TimeProvider.System);
        return (svc, db, snap);
    }

    private static RuleInput Pricing() => new()
    {
        Name = "VIP 85 折", Domain = RuleDomain.Pricing, Kind = RuleKind.Pricing, TargetEntity = "SampleRuleTarget",
        ConditionExpr = "user.Level >= 3 && target.TotalAmount >= 3000", ActionExpr = "0.85m", Priority = 10
    };

    [Fact]
    public async Task Maker_Cannot_Approve_Own_Change()
    {
        var (svc, _, _) = Create();
        var c = (await svc.SubmitChangeAsync("alice", new SubmitChangeRequest { ChangeType = RuleChangeType.Create, Proposed = Pricing(), Reason = "新增" }, default)).Value!;
        var r = await svc.ApproveAsync("Alice", c.Id, new ReviewRequest(null), default);
        r.IsSuccess.Should().BeFalse();
        r.Error!.Code.Should().Be("RUL.SELF_APPROVAL");
    }

    [Fact]
    public async Task Approve_Creates_Rule_Version_And_Updates_Snapshot()
    {
        var (svc, db, snap) = Create();
        snap.GetActiveRules().Should().BeEmpty();
        var c = (await svc.SubmitChangeAsync("alice", new SubmitChangeRequest { ChangeType = RuleChangeType.Create, Proposed = Pricing(), Reason = "新增" }, default)).Value!;
        var ok = await svc.ApproveAsync("bob", c.Id, new ReviewRequest("ok"), default);
        ok.IsSuccess.Should().BeTrue();

        (await db.Rules.CountAsync()).Should().Be(1);
        (await db.Versions.CountAsync()).Should().Be(1);
        snap.GetActiveRules().Should().ContainSingle(r => r.ActionExpr == "0.85m");

        // 已處理的申請不可再核准
        (await svc.ApproveAsync("carol", c.Id, new ReviewRequest(null), default)).Error!.Code.Should().Be("RUL.NOT_PENDING");
    }

    [Fact]
    public async Task Disable_Removes_From_Active_And_Rollback_Restores_Content()
    {
        var (svc, db, snap) = Create();
        var c = (await svc.SubmitChangeAsync("alice", new SubmitChangeRequest { ChangeType = RuleChangeType.Create, Proposed = Pricing(), Reason = "新增" }, default)).Value!;
        var ruleId = (await svc.ApproveAsync("bob", c.Id, new ReviewRequest(null), default)).Value!.RuleId!.Value;

        var d = (await svc.SubmitChangeAsync("alice", new SubmitChangeRequest { RuleId = ruleId, ChangeType = RuleChangeType.Disable, Reason = "停用" }, default)).Value!;
        await svc.ApproveAsync("bob", d.Id, new ReviewRequest(null), default);
        snap.GetActiveRules().Should().BeEmpty();

        // 更新為 0.9 後回滾到 v1
        var u = (await svc.SubmitChangeAsync("alice", new SubmitChangeRequest { RuleId = ruleId, ChangeType = RuleChangeType.Update, Proposed = Pricing() with { ActionExpr = "0.9m" }, Reason = "調整" }, default)).Value!;
        await svc.ApproveAsync("bob", u.Id, new ReviewRequest(null), default);
        var rb = (await svc.RollbackAsync("alice", new RollbackRequest(ruleId, 1, "誤調"), default)).Value!;
        await svc.ApproveAsync("bob", rb.Id, new ReviewRequest(null), default);

        var rule = await db.Rules.SingleAsync();
        rule.ActionExpr.Should().Be("0.85m");
        rule.Version.Should().Be(4);
    }

    [Fact]
    public async Task Submit_Rejects_Unsafe_Expression_And_Unknown_Target()
    {
        var (svc, _, _) = Create();
        var bad = Pricing() with { ConditionExpr = "target.GetType().Assembly != null" };
        (await svc.SubmitChangeAsync("alice", new SubmitChangeRequest { ChangeType = RuleChangeType.Create, Proposed = bad, Reason = "x" }, default)).IsSuccess.Should().BeFalse();

        var unknown = Pricing() with { TargetEntity = "Nope" };
        (await svc.SubmitChangeAsync("alice", new SubmitChangeRequest { ChangeType = RuleChangeType.Create, Proposed = unknown, Reason = "x" }, default)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Second_Pending_Change_On_Same_Rule_Conflicts()
    {
        var (svc, _, _) = Create();
        var c = (await svc.SubmitChangeAsync("alice", new SubmitChangeRequest { ChangeType = RuleChangeType.Create, Proposed = Pricing(), Reason = "新增" }, default)).Value!;
        var id = (await svc.ApproveAsync("bob", c.Id, new ReviewRequest(null), default)).Value!.RuleId!.Value;
        await svc.SubmitChangeAsync("alice", new SubmitChangeRequest { RuleId = id, ChangeType = RuleChangeType.Disable, Reason = "a" }, default);
        var second = await svc.SubmitChangeAsync("alice", new SubmitChangeRequest { RuleId = id, ChangeType = RuleChangeType.Disable, Reason = "b" }, default);
        second.Error!.Code.Should().Be("RUL.PENDING_EXISTS");
    }

    [Fact]
    public async Task Reject_Requires_Comment_And_Checker()
    {
        var (svc, _, _) = Create();
        var c = (await svc.SubmitChangeAsync("alice", new SubmitChangeRequest { ChangeType = RuleChangeType.Create, Proposed = Pricing(), Reason = "新增" }, default)).Value!;
        (await svc.RejectAsync("bob", c.Id, new ReviewRequest(" "), default)).IsSuccess.Should().BeFalse();
        (await svc.RejectAsync("alice", c.Id, new ReviewRequest("no"), default)).Error!.Code.Should().Be("RUL.SELF_APPROVAL");
        (await svc.RejectAsync("bob", c.Id, new ReviewRequest("不符"), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void DryRun_Evaluates_Condition()
    {
        var (svc, _, _) = Create();
        var r = svc.DryRun(new DryRunRequest { Rule = Pricing(), UserJson = "{\"level\":5}", TargetJson = "{\"totalAmount\":5000}" });
        r.Value!.Matched.Should().BeTrue();
        r.Value.Detail.Should().Contain("0.85");
        svc.DryRun(new DryRunRequest { Rule = Pricing(), UserJson = "{\"level\":1}", TargetJson = "{\"totalAmount\":5000}" }).Value!.Matched.Should().BeFalse();
    }
}
