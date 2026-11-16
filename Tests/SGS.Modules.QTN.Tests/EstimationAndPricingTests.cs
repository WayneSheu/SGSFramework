using FluentAssertions;
using SGS.Modules.QTN.Application.Rules;
using SGS.Modules.QTN.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.QTN.Tests;

public class PertCalculatorTests
{
    [Fact]
    public void Expected_UsesPertFormula()
    {
        // E = (2 + 4×4 + 9) / 6 = 4.5
        PertCalculator.Expected(2m, 4m, 9m).Should().Be(4.5m);
    }

    [Theory]
    [InlineData(1.0, 1.0, 4.5)]
    [InlineData(1.2, 1.3, 7.02)]
    [InlineData(1.5, 1.6, 10.8)]
    public void Quoted_AppliesTechAndRequirementFactors(double kTech, double kReq, double expected)
    {
        PertCalculator.Quoted(2m, 4m, 9m, (decimal)kTech, (decimal)kReq).Should().Be((decimal)expected);
    }

    [Fact]
    public void Quoted_RoundsOnlyOnce()
    {
        // E = (1 + 4×1 + 2)/6 = 1.1666…；×1.2×1.3 = 1.82（若先將 E 四捨五入為 1.17 會得 1.83）
        PertCalculator.Quoted(1m, 1m, 2m, 1.2m, 1.3m).Should().Be(1.82m);
    }

    [Theory]
    [InlineData(-1, 2, 3, "負數")]
    [InlineData(3, 2, 4, "≤")]
    [InlineData(0, 0, 0, "全為 0")]
    [InlineData(1, 2, 20000, "不可超過")]
    public void Validate_RejectsInvalidThreePoints(double o, double m, double p, string fragment)
    {
        PertCalculator.Validate((decimal)o, (decimal)m, (decimal)p).Should().Contain(fragment);
    }

    [Fact]
    public void Validate_AcceptsOrderedThreePoints() => PertCalculator.Validate(1m, 2m, 3m).Should().BeNull();

    [Theory]
    [InlineData(1.0, true)]
    [InlineData(1.2, true)]
    [InlineData(1.5, true)]
    [InlineData(1.3, false)]
    public void TechFactor_OnlyStandardValues(double k, bool valid) => PertCalculator.IsValidTechFactor((decimal)k).Should().Be(valid);

    [Theory]
    [InlineData(1.0, true)]
    [InlineData(1.3, true)]
    [InlineData(1.6, true)]
    [InlineData(1.2, false)]
    public void RequirementFactor_OnlyStandardValues(double k, bool valid) => PertCalculator.IsValidRequirementFactor((decimal)k).Should().Be(valid);
}

public class PricingCalculatorTests
{
    /// <summary>SOP 範本一：牌價 1,266,000 − 優惠 66,000 ＝ 1,200,000（含 5% 稅 1,260,000）。</summary>
    private static PricingInput TemplateOne(decimal discount = 66_000m) => new(
    [
        new PricingLine(WbsPhase.Analysis, 1, 10.5m, 8_000m, 20_000m),          // 210,000
        new PricingLine(WbsPhase.Development, 1, 33.5m, 8_000m, 20_000m),       // 670,000
        new PricingLine(WbsPhase.QualityAssurance, 1, 10m, 6_000m, 14_000m),    // 140,000
        new PricingLine(WbsPhase.Management, 1, 12m, 7_000m, 18_000m)           // 216,000
    ], DirectExpensePerPeriod: 30_000m, PeriodCount: 1, IpBuyout: false, IpLicenseFee: 0m, DiscountAmount: discount, TaxRate: 0.05m);

    [Fact]
    public void TemplateOne_FixedPriceTotals()
    {
        var r = PricingCalculator.Calculate(TemplateOne());

        r.LaborListTotal.Should().Be(1_236_000m);
        r.ListTotal.Should().Be(1_266_000m);
        r.FinalPrice.Should().Be(1_200_000m);
        r.TaxAmount.Should().Be(60_000m);
        r.FinalPriceWithTax.Should().Be(1_260_000m);
        r.TotalQuotedDays.Should().Be(66m);
    }

    [Fact]
    public void Cost_IncludesOverheadAt18PercentOfLabour()
    {
        var r = PricingCalculator.Calculate(TemplateOne());

        // 人力成本 = 10.5×8000 + 33.5×8000 + 10×6000 + 12×7000 = 84,000 + 268,000 + 60,000 + 84,000 = 496,000
        r.LaborCost.Should().Be(496_000m);
        r.Overhead.Should().Be(89_280m);
        r.DirectExpense.Should().Be(30_000m);
        r.TotalCost.Should().Be(615_280m);
        r.MarginRate.Should().Be(Math.Round((1_200_000m - 615_280m) / 1_200_000m, 6));
    }

    [Fact]
    public void TemplateTwo_AgileSprintCapacityTimesSprints()
    {
        // 單一 Sprint：PM 5×18000 + BE 10×18000 + FE 10×16000 + UX 5×15000 + QA 5×14000 = 575,000；3 Sprints = 1,725,000
        var input = new PricingInput(
        [
            new PricingLine(WbsPhase.Management, 1, 5m, 9_000m, 18_000m),
            new PricingLine(WbsPhase.Development, 1, 10m, 9_000m, 18_000m),
            new PricingLine(WbsPhase.Development, 1, 10m, 8_000m, 16_000m),
            new PricingLine(WbsPhase.Analysis, 1, 5m, 7_000m, 15_000m),
            new PricingLine(WbsPhase.QualityAssurance, 1, 5m, 6_000m, 14_000m)
        ], 0m, PeriodCount: 3, IpBuyout: false, IpLicenseFee: 0m, DiscountAmount: 0m, TaxRate: 0.05m);

        var r = PricingCalculator.Calculate(input);

        r.PeriodListPrice.Should().Be(575_000m);
        r.FinalPrice.Should().Be(1_725_000m);
        r.FinalPriceWithTax.Should().Be(1_811_250m);
        r.TotalQuotedDays.Should().Be(105m);
    }

    [Fact]
    public void IpBuyout_EnforcesThirtyPercentFloor()
    {
        var input = TemplateOne(discount: 0m) with { IpBuyout = true, IpLicenseFee = 1_000m };
        var r = PricingCalculator.Calculate(input);

        r.IpLicenseFee.Should().Be(379_800m); // 1,266,000 × 30%
        r.ListTotal.Should().Be(1_645_800m);
    }

    [Fact]
    public void IpFee_IgnoredWithoutBuyout()
    {
        var r = PricingCalculator.Calculate(TemplateOne() with { IpLicenseFee = 500_000m });
        r.IpLicenseFee.Should().Be(0m);
    }

    [Fact]
    public void Discount_ClampedToListTotal()
    {
        var r = PricingCalculator.Calculate(TemplateOne(discount: 9_999_999m));
        r.FinalPrice.Should().Be(0m);
        r.MarginRate.Should().Be(-1m);
    }
}

public class MilestoneRulesTests
{
    [Fact]
    public void FixedPriceDefault_ThirtyTwentyThirtyTwenty_MatchesTemplateOne()
    {
        var amounts = MilestoneRules.Allocate(MilestoneRules.FixedPriceDefault, 1_200_000m, 1_260_000m);

        amounts.Select(a => a.AmountWithTax).Should().Equal(378_000m, 252_000m, 378_000m, 252_000m);
        amounts.Sum(a => a.Amount).Should().Be(1_200_000m);
    }

    [Fact]
    public void Allocate_PutsRoundingRemainderOnLastInstalment()
    {
        var seeds = MilestoneRules.Defaults(PricingModel.TimeAndMaterials, 3);
        var amounts = MilestoneRules.Allocate(seeds, 1_000_001m, 1_050_001m);

        amounts.Sum(a => a.Amount).Should().Be(1_000_001m);
        amounts.Sum(a => a.AmountWithTax).Should().Be(1_050_001m);
        seeds.Sum(s => s.Percent).Should().Be(100m);
    }

    [Theory]
    [InlineData(PricingModel.FixedPrice, 1, 4)]
    [InlineData(PricingModel.TimeAndMaterials, 3, 3)]
    [InlineData(PricingModel.Retainer, 12, 12)]
    [InlineData(PricingModel.Hybrid, 1, 2)]
    public void Defaults_ByPricingModel(PricingModel model, int periods, int expectedCount)
    {
        var seeds = MilestoneRules.Defaults(model, periods, 0.17m);
        seeds.Should().HaveCount(expectedCount);
        seeds.Sum(s => s.Percent).Should().Be(100m);
        MilestoneRules.Validate(seeds).Should().BeNull();
    }

    [Fact]
    public void Validate_RejectsWhenNotHundredPercent() =>
        MilestoneRules.Validate([new MilestoneSeed("A", 60m, "x"), new MilestoneSeed("B", 30m, "y")]).Should().Contain("100%");
}

public class PhaseRatioRulesTests
{
    [Fact]
    public void Evaluate_ComputesRatiosAgainstBaseline()
    {
        var ratios = PhaseRatioRules.Evaluate([(WbsPhase.Analysis, 18m), (WbsPhase.Development, 55m), (WbsPhase.QualityAssurance, 15m), (WbsPhase.Management, 12m)]);

        ratios.Should().OnlyContain(r => r.InRange);
        ratios.Single(r => r.Phase == WbsPhase.Development).Ratio.Should().Be(0.55m);
    }

    [Fact]
    public void FixedPrice_MissingSupportPhase_BlocksSubmission()
    {
        var ratios = PhaseRatioRules.Evaluate([(WbsPhase.Development, 50m), (WbsPhase.Analysis, 10m)]);

        PhaseRatioRules.BlockingError(PricingModel.FixedPrice, ratios).Should().Contain("品質保證與測試").And.Contain("專案管理與部署");
        PhaseRatioRules.BlockingError(PricingModel.TimeAndMaterials, ratios).Should().BeNull();
    }

    [Fact]
    public void OutOfRange_IsWarningOnly()
    {
        var ratios = PhaseRatioRules.Evaluate([(WbsPhase.Analysis, 13m), (WbsPhase.Development, 39m), (WbsPhase.QualityAssurance, 10m), (WbsPhase.Management, 12m)]);

        PhaseRatioRules.BlockingError(PricingModel.FixedPrice, ratios).Should().BeNull();
        PhaseRatioRules.Warnings(PricingModel.FixedPrice, ratios).Should().NotBeEmpty();
    }
}

public class StrategyRulesTests
{
    [Fact]
    public void Warns_WhenModelDiffersFromCategorySuggestion() =>
        StrategyRules.Warnings(ProjectCategory.Refactor, PricingModel.FixedPrice, 1.5m, 0.5m).Should().Contain(w => w.Contains("混合計價"));

    [Fact]
    public void NoWarnings_WhenWithinStrategy() =>
        StrategyRules.Warnings(ProjectCategory.StandardCustom, PricingModel.FixedPrice, 1.1m, 0.40m).Should().BeEmpty();

    [Fact]
    public void MarginNotCompared_WhenCallerCannotSeeCost() =>
        StrategyRules.Warnings(ProjectCategory.StandardCustom, PricingModel.FixedPrice, 1.1m, null).Should().BeEmpty();
}
