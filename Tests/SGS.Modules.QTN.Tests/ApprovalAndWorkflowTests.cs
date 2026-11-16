using System.Text.Json;
using FluentAssertions;
using SGS.Modules.QTN.Application.Rules;
using SGS.Modules.QTN.Application.Services;
using SGS.Modules.QTN.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.QTN.Tests;

public class MarginLightPolicyTests
{
    [Theory]
    [InlineData(999_999, 0.40, MarginLight.Green)]
    [InlineData(1_000_000, 0.45, MarginLight.Yellow)]   // 金額 100 萬起即黃燈
    [InlineData(500_000, 0.399, MarginLight.Yellow)]    // 毛利 30%~39%
    [InlineData(5_000_000, 0.45, MarginLight.Yellow)]   // 500 萬（含）仍為黃燈
    [InlineData(5_000_001, 0.45, MarginLight.Red)]      // 超過 500 萬
    [InlineData(200_000, 0.299, MarginLight.Red)]       // 毛利低於 30%
    public void Light_TakesHighestTriggeredLevel(decimal price, double margin, MarginLight expected) =>
        MarginLightPolicy.Light(price, (decimal)margin).Should().Be(expected);

    [Fact]
    public void Route_Green_TechLeadThenSalesManager() =>
        MarginLightPolicy.Evaluate(800_000m, 0.45m, false, false).Route.Should().Equal(ApprovalRole.TechLead, ApprovalRole.SalesManager);

    [Fact]
    public void Route_Yellow_AddsPmoDirector() =>
        MarginLightPolicy.Evaluate(2_000_000m, 0.45m, false, false).Route.Should().Equal(ApprovalRole.TechLead, ApprovalRole.SalesManager, ApprovalRole.PmoDirector);

    [Fact]
    public void Route_Red_AddsCfoAndCeo() =>
        MarginLightPolicy.Evaluate(6_000_000m, 0.45m, false, false).Route.Should()
            .Equal(ApprovalRole.TechLead, ApprovalRole.SalesManager, ApprovalRole.PmoDirector, ApprovalRole.Cfo, ApprovalRole.Ceo);

    [Fact]
    public void IpBuyout_ForcesLegalCountersign() =>
        MarginLightPolicy.Evaluate(800_000m, 0.45m, ipBuyout: true, false).Route.Should().EndWith(ApprovalRole.Legal);

    [Theory]
    [InlineData(MarginLight.Green, 1.0)]
    [InlineData(MarginLight.Yellow, 0.8)]
    [InlineData(MarginLight.Red, 0.5)]
    public void BonusFactor_ByLight(MarginLight light, double factor) => MarginLightPolicy.BonusFactor(light).Should().Be((decimal)factor);

    [Fact]
    public void MarginBelowTwentyPercent_LockedUnlessSpecialApproval()
    {
        MarginLightPolicy.Evaluate(500_000m, 0.19m, false, specialApproval: false).Blocked.Should().BeTrue();
        MarginLightPolicy.Evaluate(500_000m, 0.19m, false, specialApproval: true).Blocked.Should().BeFalse();
        MarginLightPolicy.Evaluate(500_000m, 0.20m, false, specialApproval: false).Blocked.Should().BeFalse();
    }

    [Fact]
    public void YellowOrAbove_RequiresDiscountExplanation()
    {
        MarginLightPolicy.Evaluate(800_000m, 0.45m, false, false).RequiresDiscountExplanation.Should().BeFalse();
        MarginLightPolicy.Evaluate(800_000m, 0.35m, false, false).RequiresDiscountExplanation.Should().BeTrue();
    }
}

public class ApprovalSodTests
{
    private static readonly Guid Submitter = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();

    [Fact]
    public void Approver_HoldingRole_CanDecide() => ApprovalSod.CanDecide(Approver, Submitter, 0, holdsRole: true).IsOk.Should().BeTrue();

    [Fact]
    public void Submitter_CannotApproveOwnQuote() =>
        ApprovalSod.CanDecide(Submitter, Submitter, 0, true).Outcome.Should().Be(RuleOutcome.SeparationOfDuties);

    [Fact]
    public void SamePerson_CannotDecideTwoStepsInOneCycle() =>
        ApprovalSod.CanDecide(Approver, Submitter, 1, true).Outcome.Should().Be(RuleOutcome.SeparationOfDuties);

    [Fact]
    public void NonHolder_IsRejected() => ApprovalSod.CanDecide(Approver, Submitter, 0, false).Outcome.Should().Be(RuleOutcome.RoleNotAllowed);
}

public class QuoteWorkflowTests
{
    [Theory]
    [InlineData(QuoteStatus.Draft, false, true)]
    [InlineData(QuoteStatus.Draft, true, false)]
    [InlineData(QuoteStatus.PendingApproval, true, false)]
    [InlineData(QuoteStatus.Approved, true, false)]
    [InlineData(QuoteStatus.Rejected, true, false)]
    public void OnlyUnlockedDraftIsEditable(QuoteStatus status, bool locked, bool editable) =>
        QuoteWorkflow.IsEditable(status, locked).Should().Be(editable);

    [Fact]
    public void Submit_RequiresTechReview()
    {
        QuoteWorkflow.CanSubmit(QuoteStatus.Draft, techReviewed: false).Outcome.Should().Be(RuleOutcome.ValidationFailed);
        QuoteWorkflow.CanSubmit(QuoteStatus.Draft, techReviewed: true).IsOk.Should().BeTrue();
        QuoteWorkflow.CanSubmit(QuoteStatus.Approved, true).Outcome.Should().Be(RuleOutcome.InvalidTransition);
    }

    [Theory]
    [InlineData(QuoteStatus.Draft, false)]
    [InlineData(QuoteStatus.PendingApproval, false)]
    [InlineData(QuoteStatus.Rejected, true)]
    [InlineData(QuoteStatus.Approved, true)]
    [InlineData(QuoteStatus.Issued, true)]
    [InlineData(QuoteStatus.Expired, true)]
    [InlineData(QuoteStatus.Won, false)]
    [InlineData(QuoteStatus.Superseded, false)]
    public void Revise_AllowedSources(QuoteStatus status, bool allowed) => QuoteWorkflow.CanRevise(status).Should().Be(allowed);

    [Fact]
    public void Revise_SupersedesOnlyApprovedOrIssued()
    {
        QuoteWorkflow.SupersedesSource(QuoteStatus.Approved).Should().BeTrue();
        QuoteWorkflow.SupersedesSource(QuoteStatus.Issued).Should().BeTrue();
        QuoteWorkflow.SupersedesSource(QuoteStatus.Rejected).Should().BeFalse();
    }

    [Fact]
    public void NextVersion_IncrementsMinorAndTagsChangeRequest()
    {
        QuoteWorkflow.NextVersion([], false).Should().Be((1, 0, "v1.0"));
        QuoteWorkflow.NextVersion([(1, 0)], false).Label.Should().Be("v1.1");
        QuoteWorkflow.NextVersion([(1, 0), (1, 1)], true).Label.Should().Be("v1.2-CR");
    }

    [Fact]
    public void Won_RejectedAfterValidity()
    {
        var issued = new DateOnly(2026, 11, 1);
        var validUntil = QuoteWorkflow.ValidUntil(issued, 30);
        validUntil.Should().Be(new DateOnly(2026, 12, 1));
        QuoteWorkflow.CanMarkWon(QuoteStatus.Issued, validUntil, validUntil).IsOk.Should().BeTrue();
        QuoteWorkflow.CanMarkWon(QuoteStatus.Issued, validUntil, validUntil.AddDays(1)).IsOk.Should().BeFalse();
        QuoteWorkflow.IsExpired(QuoteStatus.Issued, validUntil, validUntil.AddDays(1)).Should().BeTrue();
    }
}

public class QuoteNumberingTests
{
    [Fact]
    public void QuoteNo_FollowsTemplateFormat() =>
        QuoteNumbering.NextQuoteNo(PricingModel.FixedPrice, new DateOnly(2026, 11, 15), []).Should().Be("QT-FP-20261115-01");

    [Fact]
    public void QuoteNo_IncrementsPerModelAndDay() =>
        QuoteNumbering.NextQuoteNo(PricingModel.Retainer, new DateOnly(2026, 11, 15), ["QT-RT-20261115-01", "QT-RT-20261115-03", "QT-FP-20261115-07"])
            .Should().Be("QT-RT-20261115-04");

    [Fact]
    public void OpportunityAndAssessmentNumbers()
    {
        QuoteNumbering.NextOpportunityNo(2026, ["OP-2026-009"]).Should().Be("OP-2026-010");
        QuoteNumbering.NextAssessmentNo("QT-FP-20261115-01", ["QT-FP-20261115-01-CR01"]).Should().Be("QT-FP-20261115-01-CR02");
    }
}

public class VarianceAndWorkdayTests
{
    [Theory]
    [InlineData(10, 11.5, 0.15, false)]
    [InlineData(10, 11.6, 0.16, true)]
    [InlineData(10, 8.4, -0.16, true)]
    public void Variance_ThresholdIsPlusMinusFifteenPercent(double quoted, double actual, double rate, bool exceeds)
    {
        var r = VarianceRules.Rate((decimal)quoted, (decimal)actual);
        r.Should().Be((decimal)rate);
        VarianceRules.Exceeds(r).Should().Be(exceeds);
    }

    [Fact]
    public void Calibrate_ScalesThreePointsToActual() =>
        VarianceRules.Calibrate(2m, 4m, 8m, 6m).Should().Be((3m, 6m, 12m));

    [Fact]
    public void AddWorkdays_SkipsWeekendsAndHolidays()
    {
        // 2026-11-13 是週五：+3 個工作日 → 11-16(一)、11-17(二)、11-18(三)
        Workdays.AddWorkdays(new DateOnly(2026, 11, 13), 3, new Dictionary<DateOnly, bool>()).Should().Be(new DateOnly(2026, 11, 18));

        // 11-17 為假日 → 順延至 11-19
        var overrides = new Dictionary<DateOnly, bool> { [new DateOnly(2026, 11, 17)] = false };
        Workdays.AddWorkdays(new DateOnly(2026, 11, 13), 3, overrides).Should().Be(new DateOnly(2026, 11, 19));
    }
}

public class FieldSecurityPolicyTests
{
    [Theory]
    [InlineData(true, false, QuoteFieldAccess.Full)]
    [InlineData(false, true, QuoteFieldAccess.PriceOnly)]
    [InlineData(false, false, QuoteFieldAccess.EffortOnly)]
    public void Resolve(bool cost, bool price, QuoteFieldAccess expected) => FieldSecurityPolicy.Resolve(cost, price).Should().Be(expected);

    [Fact]
    public void SalesSeesPriceButNotCost()
    {
        QuoteFieldAccess.PriceOnly.Price(100m).Should().Be(100m);
        QuoteFieldAccess.PriceOnly.Cost(100m).Should().BeNull();
    }

    [Fact]
    public void TechLeadSeesNoMoney()
    {
        QuoteFieldAccess.EffortOnly.Price(100m).Should().BeNull();
        QuoteFieldAccess.EffortOnly.Cost(100m).Should().BeNull();
    }
}

public class QuoteCalculatorTests
{
    private static QuoteVersion NewQuote()
    {
        var q = new QuoteVersion { Id = Guid.NewGuid(), PricingModel = PricingModel.FixedPrice, TaxRate = 0.05m, Title = "T", VersionLabel = "v1.0" };
        var parent = new QuoteWbsItem { Id = Guid.NewGuid(), Name = "系統分析", SortOrder = 1 };
        var child = new QuoteWbsItem { Id = Guid.NewGuid(), ParentId = parent.Id, Name = "需求訪談", SortOrder = 1, KTech = 1.0m, KReq = 1.3m };
        var a = new WbsRoleAllocation { Id = Guid.NewGuid(), RateRoleId = Guid.NewGuid(), RoleName = "SA", Optimistic = 2, MostLikely = 4, Pessimistic = 9, SnapshotCostRate = 8_000, SnapshotListRate = 16_000 };
        QuoteCalculator.ApplyPert(child, a);
        child.Allocations.Add(a);
        q.WbsItems.AddRange([parent, child]);
        q.Milestones.AddRange(MilestoneRules.FixedPriceDefault.Select((m, i) => new QuotePaymentMilestone { Seq = i + 1, Name = m.Name, Percent = m.Percent, Trigger = m.Trigger }));
        QuoteCalculator.AssignWbsCodes(q.WbsItems);
        return q;
    }

    [Fact]
    public void AssignWbsCodes_HierarchicalNumbering()
    {
        var q = NewQuote();
        q.WbsItems.Select(w => w.WbsCode).Should().Equal("1.0", "1.1");
    }

    [Fact]
    public void Recalculate_WritesTotalsLightAndMilestones()
    {
        var q = NewQuote();
        QuoteCalculator.Recalculate(q, ipBuyout: false);

        q.TotalQuotedDays.Should().Be(5.85m); // 4.5 × 1.3
        q.FinalPrice.Should().Be(93_600m);    // 5.85 × 16,000
        q.MarginLight.Should().Be(MarginLight.Green);
        q.Milestones.Sum(m => m.Amount).Should().Be(q.FinalPrice);
    }

    [Fact]
    public void ContentHash_ChangesWhenScopeChanges()
    {
        var q = NewQuote();
        QuoteCalculator.Recalculate(q, false);
        var before = QuoteCalculator.ComputeHash(q);

        q.WbsItems[1].Allocations[0].MostLikely = 5;
        QuoteCalculator.ComputeHash(q).Should().NotBe(before);
    }
}

public class EnumJsonTests
{
    [Fact]
    public void Enums_SerializeAsStrings_WithoutGlobalConverter()
    {
        JsonSerializer.Serialize(QuoteStatus.PendingApproval).Should().Be("\"PendingApproval\"");
        JsonSerializer.Serialize(MarginLight.Red).Should().Be("\"Red\"");
        JsonSerializer.Serialize(PricingModel.TimeAndMaterials).Should().Be("\"TimeAndMaterials\"");
        JsonSerializer.Serialize(ApprovalRole.Cfo).Should().Be("\"Cfo\"");
        JsonSerializer.Serialize(QuoteFieldAccess.PriceOnly).Should().Be("\"PriceOnly\"");
    }
}
