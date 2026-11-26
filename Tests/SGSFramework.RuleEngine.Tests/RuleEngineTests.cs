using FluentAssertions;
using SGSFramework.Core.Rules;
using SGSFramework.RuleEngine;
using Xunit;

namespace SGSFramework.RuleEngine.Tests;

public sealed class Dept { public string Path { get; set; } = ""; }

public sealed class Member
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Salary { get; set; }
    public string CustomerType { get; set; } = "";
    public decimal Amount { get; set; }
    public int AccountAgeDays { get; set; }
    public Dept Department { get; set; } = new();
}

internal sealed class FakeProvider(params RuleSnapshot[] rules) : IRuleSnapshotProvider
{
    public IReadOnlyList<RuleSnapshot> GetActiveRules() => rules;
}

public class RuleCombinerTests
{
    [Fact]
    public void Read_ForceDeny_Overrides_Allow() =>
        RuleCombiner.CombineRead([FieldAccessLevel.Allow, FieldAccessLevel.ForceDeny]).Should().Be(FieldAccessLevel.Deny);

    [Fact]
    public void Read_MostPermissive() =>
        RuleCombiner.CombineRead([FieldAccessLevel.Mask, FieldAccessLevel.Allow, FieldAccessLevel.Deny]).Should().Be(FieldAccessLevel.Allow);

    [Fact]
    public void Read_NoMatch_IsDeny() => RuleCombiner.CombineRead([]).Should().Be(FieldAccessLevel.Deny);

    [Fact]
    public void Write_RequiresAllow_AndForceDenyWins()
    {
        RuleCombiner.CombineWrite([FieldAccessLevel.Deny]).Should().BeFalse();
        RuleCombiner.CombineWrite([FieldAccessLevel.Allow]).Should().BeTrue();
        RuleCombiner.CombineWrite([FieldAccessLevel.Allow, FieldAccessLevel.ForceDeny]).Should().BeFalse();
    }

    [Fact]
    public void Risk_BlockShortCircuits_ReviewBeatsPass()
    {
        RuleCombiner.CombineRisk([RiskDecision.Pass, RiskDecision.ManualReview]).Should().Be(RiskDecision.ManualReview);
        RuleCombiner.CombineRisk([RiskDecision.ManualReview, RiskDecision.Block]).Should().Be(RiskDecision.Block);
        RuleCombiner.CombineRisk([]).Should().Be(RiskDecision.Pass);
    }

    [Fact]
    public void Pricing_BestPrice_And_Stack()
    {
        RuleCombiner.CombinePricing([0.9m, 0.8m], PricingMergeStrategy.BestPrice).Should().Be(0.8m);
        RuleCombiner.CombinePricing([0.9m, 0.8m], PricingMergeStrategy.Stack).Should().Be(0.72m);
        RuleCombiner.CombinePricing([], PricingMergeStrategy.Stack).Should().Be(1m);
    }
}

public class ExpressionSafetyTests
{
    private readonly RuleExpressionValidator _v = new();

    [Theory]
    [InlineData("target.CustomerType == \"Company\"")]
    [InlineData("user.Level >= 3 && target.Amount > 50000")]
    [InlineData("target.Department.Path.StartsWith(user.DepartmentPath)")]
    [InlineData("user.Roles.Contains(\"DeptManager\")")]
    public void Safe_Expressions_Pass(string expr) =>
        _v.Validate(typeof(Member), RuleKind.Column, expr).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("target.GetType().Assembly != null")]
    [InlineData("System.IO.File.Exists(\"x\")")]
    [InlineData("System.Diagnostics.Process.Start(\"calc\") != null")]
    [InlineData("Convert.ToInt32(target.Id) > 0")]
    [InlineData("target.Name.GetType() != null")]
    [InlineData("1 +")]
    public void Unsafe_Or_Invalid_Expressions_Rejected(string expr) =>
        _v.Validate(typeof(Member), RuleKind.Column, expr).IsValid.Should().BeFalse();

    [Fact]
    public void Too_Long_Rejected() =>
        _v.Validate(typeof(Member), RuleKind.Column, new string('a', 600)).IsValid.Should().BeFalse();
}

public class DynamicRuleEngineTests
{
    private static RuleSnapshot Col(string field, string cond, FieldAccessLevel read, FieldAccessLevel write = FieldAccessLevel.Deny, string role = "DeptManager") => new()
    {
        Id = 1, Kind = RuleKind.Column, TargetEntity = nameof(Member), FieldName = field, RoleName = role,
        ConditionExpr = cond, ReadAccess = read, WriteAccess = write
    };

    private static readonly RuleUserContext Manager = new() { UserId = "1", DepartmentPath = "/1/5/", Level = 5, Roles = ["DeptManager"] };

    [Fact]
    public void Column_Read_UsesCondition_AndRole()
    {
        var engine = new DynamicRuleEngine(new FakeProvider(
            Col("Salary", "target.Department.Path.StartsWith(user.DepartmentPath)", FieldAccessLevel.Allow, FieldAccessLevel.Allow)));
        var inDept = new Member { Department = new Dept { Path = "/1/5/12/" } };
        var other = new Member { Department = new Dept { Path = "/1/6/" } };

        engine.EvaluateRead(Manager, inDept, "Salary").Should().Be(FieldAccessLevel.Allow);
        engine.EvaluateWrite(Manager, inDept, "Salary").Should().BeTrue();
        engine.EvaluateRead(Manager, other, "Salary").Should().Be(FieldAccessLevel.Deny);
        engine.EvaluateRead(new RuleUserContext { Roles = [] }, inDept, "Salary").Should().Be(FieldAccessLevel.Deny);
    }

    [Fact]
    public void Column_GlobalPolicy_ForceDeny_Wins()
    {
        var engine = new DynamicRuleEngine(new FakeProvider(
            Col("Salary", "true", FieldAccessLevel.Allow),
            Col("Salary", "target.Id == 7", FieldAccessLevel.ForceDeny, role: "GlobalPolicy")));
        engine.EvaluateRead(Manager, new Member { Id = 7 }, "Salary").Should().Be(FieldAccessLevel.Deny);
        engine.EvaluateRead(Manager, new Member { Id = 8 }, "Salary").Should().Be(FieldAccessLevel.Allow);
    }

    [Fact]
    public void Row_Security_Filters_Allow_And_Excludes_ForceDeny()
    {
        RuleSnapshot Row(string cond, FieldAccessLevel a) => new()
        {
            Kind = RuleKind.Row, TargetEntity = nameof(Member), RoleName = "DeptManager", ConditionExpr = cond, ReadAccess = a
        };
        var engine = new DynamicRuleEngine(new FakeProvider(
            Row("row.Department.Path.StartsWith(user.DepartmentPath)", FieldAccessLevel.Allow),
            Row("row.Id == 3", FieldAccessLevel.ForceDeny)));
        var data = new[]
        {
            new Member { Id = 1, Department = new Dept { Path = "/1/5/" } },
            new Member { Id = 2, Department = new Dept { Path = "/1/5/12/" } },
            new Member { Id = 3, Department = new Dept { Path = "/1/5/13/" } },
            new Member { Id = 4, Department = new Dept { Path = "/1/9/" } },
        }.AsQueryable();

        engine.ApplyRowSecurity(data, Manager).Select(m => m.Id).Should().BeEquivalentTo([1, 2]);
    }

    private sealed class Person { public Guid Id { get; set; } public string Name { get; set; } = ""; }

    [Fact]
    public void Row_MemberScope_Filters_To_Members_And_HasRowRules_Is_Role_Based()
    {
        var rule = new RuleSnapshot
        {
            Kind = RuleKind.Row, TargetEntity = nameof(Person), RoleName = "MO",
            ConditionExpr = "user.MemberUserIds.Contains(row.Id)", ReadAccess = FieldAccessLevel.Allow
        };
        var engine = new DynamicRuleEngine(new FakeProvider(rule));
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var data = new[] { new Person { Id = a }, new Person { Id = b }, new Person { Id = c } }.AsQueryable();
        var mo = new RuleUserContext { Roles = ["MO"], MemberUserIds = [a, c] };

        engine.HasRowRules<Person>(mo).Should().BeTrue();
        engine.HasRowRules<Person>(new RuleUserContext { Roles = ["Other"] }).Should().BeFalse();
        engine.ApplyRowSecurity(data, mo).Select(p => p.Id).Should().BeEquivalentTo([a, c]);
    }

    [Fact]
    public void Sensitive_Members_Cannot_Be_Referenced() =>
        new RuleExpressionValidator().Validate(typeof(Microsoft.AspNetCore.Identity.IdentityUser<Guid>), RuleKind.Row, "row.PasswordHash.StartsWith(\"a\")").IsValid.Should().BeFalse();

    [Fact]
    public void Row_NoAllowRule_ReturnsNothing()
    {
        var engine = new DynamicRuleEngine(new FakeProvider());
        engine.ApplyRowSecurity(new[] { new Member() }.AsQueryable(), Manager).Should().BeEmpty();
    }

    [Fact]
    public void Form_Required_And_Visible()
    {
        var engine = new DynamicRuleEngine(new FakeProvider(new RuleSnapshot
        {
            Kind = RuleKind.Validation, TargetEntity = nameof(Member), FieldName = "TaxId", RoleName = "GlobalPolicy",
            ConditionExpr = "target.CustomerType == \"Company\"",
            OutputPayload = "{\"visible\":true,\"required\":true,\"errorMessage\":\"企業用戶必須填寫統一編號\"}"
        }));
        var states = engine.EvaluateForm(Manager, new Member { CustomerType = "Company" });
        states.Should().ContainSingle(s => s.Field == "TaxId" && s.Visible && s.Required && s.Messages.Count == 1);
        engine.EvaluateForm(Manager, new Member { CustomerType = "Person" }).Should().BeEmpty();
    }

    [Fact]
    public void Risk_NewAccountLargeAmount_ManualReview_BlockOverrides()
    {
        var review = new RuleSnapshot
        {
            Kind = RuleKind.Risk, TargetEntity = nameof(Member), RoleName = "GlobalPolicy",
            ConditionExpr = "target.AccountAgeDays < 7 && target.Amount > 50000",
            OutputPayload = "{\"decision\":\"ManualReview\",\"reason\":\"新戶大額交易風險\"}"
        };
        var block = review with { ConditionExpr = "target.Amount > 1000000", Risk = RiskDecision.Block, OutputPayload = "{\"reason\":\"超額\"}" };
        var engine = new DynamicRuleEngine(new FakeProvider(review, block));

        var v = engine.EvaluateRisk(Manager, new Member { AccountAgeDays = 1, Amount = 60000 });
        v.Decision.Should().Be(RiskDecision.ManualReview);
        v.Reasons.Should().Contain("新戶大額交易風險");
        engine.EvaluateRisk(Manager, new Member { AccountAgeDays = 1, Amount = 2000000 }).Decision.Should().Be(RiskDecision.Block);
        engine.EvaluateRisk(Manager, new Member { AccountAgeDays = 100, Amount = 10 }).Decision.Should().Be(RiskDecision.Pass);
    }

    [Fact]
    public void Pricing_Strategies()
    {
        RuleSnapshot P(long id, string cond, string action, int pri) => new()
        {
            Id = id, Kind = RuleKind.Pricing, TargetEntity = nameof(Member), RoleName = "GlobalPolicy",
            ConditionExpr = cond, ActionExpr = action, Priority = pri
        };
        var engine = new DynamicRuleEngine(new FakeProvider(
            P(1, "user.Level >= 3 && target.Amount >= 3000", "0.85m", 10),
            P(2, "target.Amount >= 10000", "0.8m", 5)));
        var m = new Member { Amount = 12000 };

        engine.EvaluatePricing(Manager, m, PricingMergeStrategy.BestPrice).Multiplier.Should().Be(0.8m);
        engine.EvaluatePricing(Manager, m, PricingMergeStrategy.Stack).Multiplier.Should().Be(0.68m);
        engine.EvaluatePricing(Manager, new Member { Amount = 100 }, PricingMergeStrategy.Stack).Multiplier.Should().Be(1m);
    }

    [Fact]
    public void Unsafe_Rule_Throws_When_Evaluated()
    {
        var engine = new DynamicRuleEngine(new FakeProvider(
            Col("Salary", "target.GetType().Assembly != null", FieldAccessLevel.Allow, role: "GlobalPolicy")));
        var act = () => engine.EvaluateRead(Manager, new Member(), "Salary");
        act.Should().Throw<Exception>();
    }
}
