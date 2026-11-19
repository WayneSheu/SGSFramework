using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class CrWorkflowTests
{
    private static RuleResult Step(CrStatus s, CrAction a, out CrStatus next, CrDecision? d = null, bool defer = false) =>
        CrWorkflow.Next(s, a, d, defer, out next);

    [Fact]
    public void Evaluate_FromReceived_GoesToPendingDecision()
    {
        Step(CrStatus.Received, CrAction.Evaluate, out var next).IsOk.Should().BeTrue();
        next.Should().Be(CrStatus.PendingDecision);
    }

    [Fact]
    public void Evaluate_CanBeRedone_BeforeDecision()
    {
        Step(CrStatus.PendingDecision, CrAction.Evaluate, out var next).IsOk.Should().BeTrue();
        next.Should().Be(CrStatus.PendingDecision);
    }

    [Theory]
    [InlineData(CrDecision.A_Delay)]
    [InlineData(CrDecision.B_Swap)]
    public void Decide_AorB_GoesToPendingSign(CrDecision d)
    {
        Step(CrStatus.PendingDecision, CrAction.Decide, out var next, d).IsOk.Should().BeTrue();
        next.Should().Be(CrStatus.PendingSign);
    }

    [Fact]
    public void Decide_C_Reject_GoesToRejected()
    {
        Step(CrStatus.PendingDecision, CrAction.Decide, out var next, CrDecision.C_Reject, defer: false).IsOk.Should().BeTrue();
        next.Should().Be(CrStatus.Rejected);
    }

    [Fact]
    public void Decide_C_Defer_GoesToDeferred()
    {
        Step(CrStatus.PendingDecision, CrAction.Decide, out var next, CrDecision.C_Reject, defer: true).IsOk.Should().BeTrue();
        next.Should().Be(CrStatus.Deferred);
    }

    [Fact]
    public void Decide_WithoutEvaluation_Rejected() =>
        Step(CrStatus.Received, CrAction.Decide, out _, CrDecision.A_Delay).Outcome.Should().Be(RuleOutcome.InvalidTransition);

    [Fact]
    public void Decide_WithoutDecisionValue_Rejected() =>
        Step(CrStatus.PendingDecision, CrAction.Decide, out _).IsOk.Should().BeFalse();

    [Theory]
    [InlineData(CrStatus.Received)]
    [InlineData(CrStatus.PendingDecision)]
    public void Cancel_AllowedBeforeDecision(CrStatus s)
    {
        Step(s, CrAction.Cancel, out var next).IsOk.Should().BeTrue();
        next.Should().Be(CrStatus.Cancelled);
    }

    [Theory]
    [InlineData(CrStatus.PendingSign)]
    [InlineData(CrStatus.Approved)]
    [InlineData(CrStatus.Rejected)]
    public void Cancel_NotAllowedOnceDecided(CrStatus s) =>
        Step(s, CrAction.Cancel, out _).IsOk.Should().BeFalse();

    [Fact]
    public void Reopen_DeferredBackToReceived()
    {
        Step(CrStatus.Deferred, CrAction.Reopen, out var next).IsOk.Should().BeTrue();
        next.Should().Be(CrStatus.Received);
    }

    [Theory]
    [InlineData(CrStatus.Received)]
    [InlineData(CrStatus.PendingDecision)]
    [InlineData(CrStatus.PendingSign)]
    [InlineData(CrStatus.Approved)]
    [InlineData(CrStatus.Rejected)]
    [InlineData(CrStatus.Cancelled)]
    public void Reopen_OnlyDeferred(CrStatus s) =>
        Step(s, CrAction.Reopen, out _).IsOk.Should().BeFalse();

    [Fact]
    public void Evaluate_Locked_AfterDecision() =>
        Step(CrStatus.PendingSign, CrAction.Evaluate, out _).IsOk.Should().BeFalse();

    [Fact]
    public void Sign_OnlyWhilePendingSign()
    {
        Step(CrStatus.PendingSign, CrAction.Sign, out _).IsOk.Should().BeTrue();
        Step(CrStatus.PendingDecision, CrAction.Sign, out _).IsOk.Should().BeFalse();
        Step(CrStatus.Approved, CrAction.Sign, out _).IsOk.Should().BeFalse();
    }

    [Fact]
    public void TerminalStates_AcceptNothing()
    {
        foreach (var s in new[] { CrStatus.Approved, CrStatus.Rejected, CrStatus.Deferred, CrStatus.Cancelled })
            foreach (var a in Enum.GetValues<CrAction>())
            {
                // 暫緩（Deferred）唯一的出口是 PM 重啟，其餘動作一律不允許
                if (s == CrStatus.Deferred && a == CrAction.Reopen) continue;
                Step(s, a, out _, CrDecision.A_Delay).IsOk.Should().BeFalse($"{s}/{a}");
            }
    }
}

public class CrSignoffRulesTests
{
    private static readonly Guid Pm = Guid.NewGuid();
    private static readonly Guid Tl = Guid.NewGuid();
    private static readonly Guid Be = Guid.NewGuid();

    private static readonly (Guid, RaciRole)[] Members = [(Pm, RaciRole.Pm), (Tl, RaciRole.TechLead), (Be, RaciRole.Be)];

    [Fact]
    public void DualSignoffDisabled_OneInternalSignerCompletesInternalCr()
    {
        CrSignoffRules.Pending(CrKind.Internal, [], dualSignoff: false).Should().Equal(CrSignRole.Pm, CrSignRole.TechLead);
        CrSignoffRules.IsComplete(CrKind.Internal, [CrSignRole.Pm], dualSignoff: false).Should().BeTrue();
        CrSignoffRules.IsComplete(CrKind.Internal, [CrSignRole.TechLead], dualSignoff: false).Should().BeTrue();
        CrSignoffRules.IsComplete(CrKind.Internal, [], dualSignoff: false).Should().BeFalse();
    }

    [Fact]
    public void DualSignoffDisabled_CustomerCrStillNeedsCustomer()
    {
        CrSignoffRules.Pending(CrKind.Customer, [CrSignRole.Pm], dualSignoff: false).Should().Equal(CrSignRole.Customer);
        CrSignoffRules.IsComplete(CrKind.Customer, [CrSignRole.Pm], dualSignoff: false).Should().BeFalse();
        CrSignoffRules.IsComplete(CrKind.Customer, [CrSignRole.TechLead, CrSignRole.Customer], dualSignoff: false).Should().BeTrue();
    }

    [Fact]
    public void DualSignoffDisabled_SecondInternalSignerIsRejected() =>
        CrSignoffRules.ValidateSign(CrKind.Internal, CrSignRole.TechLead, [CrSignRole.Pm], dualSignoff: false).IsOk.Should().BeFalse();

    [Fact]
    public void DualSignoffDisabled_CustomerMaySignAfterOneInternalSigner()
    {
        CrSignoffRules.ValidateSign(CrKind.Customer, CrSignRole.Customer, [CrSignRole.Pm], dualSignoff: false).IsOk.Should().BeTrue();
        CrSignoffRules.ValidateSign(CrKind.Customer, CrSignRole.Customer, [], dualSignoff: false).IsOk.Should().BeFalse();
        CrSignoffRules.ValidateSign(CrKind.Customer, CrSignRole.Customer, [CrSignRole.Pm], dualSignoff: true).IsOk.Should().BeFalse();
    }

    [Fact]
    public void CustomerCr_NeedsThreeRoles() =>
        CrSignoffRules.Required(CrKind.Customer).Should().Equal(CrSignRole.Pm, CrSignRole.TechLead, CrSignRole.Customer);

    [Fact]
    public void InternalCr_NeedsPmAndTechLead() =>
        CrSignoffRules.Required(CrKind.Internal).Should().Equal(CrSignRole.Pm, CrSignRole.TechLead);

    [Fact]
    public void Pending_ShrinksAsRolesSign()
    {
        CrSignoffRules.Pending(CrKind.Customer, [CrSignRole.Pm]).Should().Equal(CrSignRole.TechLead, CrSignRole.Customer);
        CrSignoffRules.IsComplete(CrKind.Internal, [CrSignRole.Pm, CrSignRole.TechLead]).Should().BeTrue();
        CrSignoffRules.IsComplete(CrKind.Customer, [CrSignRole.Pm, CrSignRole.TechLead]).Should().BeFalse();
    }

    [Fact]
    public void RoleOf_MapsProjectRole()
    {
        CrSignoffRules.RoleOf(Pm, Members).Should().Be(CrSignRole.Pm);
        CrSignoffRules.RoleOf(Tl, Members).Should().Be(CrSignRole.TechLead);
        CrSignoffRules.RoleOf(Be, Members).Should().BeNull();
        CrSignoffRules.RoleOf(Guid.NewGuid(), Members).Should().BeNull();
    }

    [Fact]
    public void Sign_Twice_Rejected() =>
        CrSignoffRules.ValidateSign(CrKind.Internal, CrSignRole.Pm, [CrSignRole.Pm]).Outcome.Should().Be(RuleOutcome.InvalidTransition);

    [Fact]
    public void InternalCr_CustomerRole_NotRequired() =>
        CrSignoffRules.ValidateSign(CrKind.Internal, CrSignRole.Customer, [CrSignRole.Pm, CrSignRole.TechLead]).Outcome.Should().Be(RuleOutcome.RoleNotAllowed);

    [Fact]
    public void Customer_MustSignLast()
    {
        CrSignoffRules.ValidateSign(CrKind.Customer, CrSignRole.Customer, [CrSignRole.Pm]).IsOk.Should().BeFalse();
        CrSignoffRules.ValidateSign(CrKind.Customer, CrSignRole.Customer, [CrSignRole.Pm, CrSignRole.TechLead]).IsOk.Should().BeTrue();
    }

    [Fact]
    public void Proxy_Allowed_ForPm_WithReason_AfterInternalSign() =>
        CrSignoffRules.ValidateCustomerProxy(CrKind.Customer, Pm, Members, [CrSignRole.Pm, CrSignRole.TechLead], "客戶以 Email 同意").IsOk.Should().BeTrue();

    [Fact]
    public void Proxy_Rejected_ForNonPm() =>
        CrSignoffRules.ValidateCustomerProxy(CrKind.Customer, Tl, Members, [CrSignRole.Pm, CrSignRole.TechLead], "x").Outcome.Should().Be(RuleOutcome.RoleNotAllowed);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Proxy_Requires_Reason(string? reason) =>
        CrSignoffRules.ValidateCustomerProxy(CrKind.Customer, Pm, Members, [CrSignRole.Pm, CrSignRole.TechLead], reason).IsOk.Should().BeFalse();

    [Fact]
    public void Proxy_Rejected_ForInternalCr() =>
        CrSignoffRules.ValidateCustomerProxy(CrKind.Internal, Pm, Members, [CrSignRole.Pm, CrSignRole.TechLead], "x").IsOk.Should().BeFalse();

    [Fact]
    public void Proxy_Rejected_BeforePmAndTechLeadSigned() =>
        CrSignoffRules.ValidateCustomerProxy(CrKind.Customer, Pm, Members, [CrSignRole.Pm], "x").IsOk.Should().BeFalse();
}

public class CrInputRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 14);

    [Fact]
    public void Layers_AreNormalized_Deduped_AndOrdered()
    {
        var (layers, error) = CrInputRules.NormalizeLayers(["domain", "API", "Api", " Presentation "]);
        error.Should().BeNull();
        layers.Should().Equal("Presentation", "Api", "Domain");
    }

    [Fact]
    public void Layers_EmptyOrUnknown_Rejected()
    {
        CrInputRules.NormalizeLayers([]).Error.Should().NotBeNull();
        CrInputRules.NormalizeLayers(null).Error.Should().NotBeNull();
        CrInputRules.NormalizeLayers(["Api", "Database"]).Error.Should().Contain("Database");
    }

    [Theory]
    [InlineData("Domain", true)]
    [InlineData("Infrastructure", true)]
    [InlineData("Api", false)]
    [InlineData("Presentation", false)]
    public void HighRisk_OnlyDomainAndInfrastructure(string layer, bool expected) =>
        CrInputRules.IsHighRisk([layer]).Should().Be(expected);

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, 10000, 0)]
    [InlineData(0, 0, -0.5)]
    public void Hours_OutOfRange_Rejected(double be, double fe, double verify) =>
        CrInputRules.ValidateHours((decimal)be, (decimal)fe, (decimal)verify).Should().NotBeNull();

    [Fact]
    public void Hours_InRange_Accepted() => CrInputRules.ValidateHours(16, 8, 4).Should().BeNull();

    [Fact]
    public void Decision_A_RequiresFutureReleaseDate()
    {
        CrInputRules.ValidateDecision(CrKind.Customer, CrDecision.A_Delay, 100, null, null, null, Today).Should().Contain("上線日");
        CrInputRules.ValidateDecision(CrKind.Customer, CrDecision.A_Delay, 100, Today.AddDays(-1), null, null, Today).Should().Contain("早於");
        CrInputRules.ValidateDecision(CrKind.Customer, CrDecision.A_Delay, 100, Today, null, null, Today).Should().BeNull();
    }

    [Fact]
    public void Decision_B_RequiresSwappedItems()
    {
        CrInputRules.ValidateDecision(CrKind.Internal, CrDecision.B_Swap, null, null, " ", null, Today).Should().NotBeNull();
        CrInputRules.ValidateDecision(CrKind.Internal, CrDecision.B_Swap, null, null, "報表匯出", null, Today).Should().BeNull();
    }

    [Fact]
    public void Decision_C_RequiresRationale_AndNoQuote()
    {
        CrInputRules.ValidateDecision(CrKind.Customer, CrDecision.C_Reject, null, null, null, null, Today).Should().NotBeNull();
        CrInputRules.ValidateDecision(CrKind.Customer, CrDecision.C_Reject, null, null, null, "成本過高", Today).Should().BeNull();
        CrInputRules.ValidateDecision(CrKind.Customer, CrDecision.C_Reject, 100, null, null, "成本過高", Today).Should().NotBeNull();
    }

    [Fact]
    public void CustomerCr_NeedsQuote_InternalCr_MustNotHaveQuote()
    {
        CrInputRules.ValidateDecision(CrKind.Customer, CrDecision.B_Swap, null, null, "x", null, Today).Should().Contain("報價");
        CrInputRules.ValidateDecision(CrKind.Internal, CrDecision.B_Swap, 100, null, "x", null, Today).Should().Contain("內部 CR 不報價");
    }

    [Fact]
    public void NegativeQuote_Rejected() =>
        CrInputRules.ValidateDecision(CrKind.Customer, CrDecision.B_Swap, -1, null, "x", null, Today).Should().NotBeNull();
}

public class CrSlaAndHashTests
{
    private static readonly TimeSpan Tw = TimeSpan.FromHours(8);

    [Fact]
    public void EvalDue_IsTwoWorkdays_SkippingWeekend() =>
        // 週五 14:30 受理 → 週二 14:30
        CrSla.EvalDue(new DateTimeOffset(2026, 11, 13, 14, 30, 0, Tw), new WorkCalendar())
            .Should().Be(new DateTimeOffset(2026, 11, 17, 14, 30, 0, Tw));

    [Fact]
    public void EvalDue_SkipsHolidays()
    {
        var cal = new WorkCalendar([new HolidayEntry(new DateOnly(2026, 11, 16), false)]);
        CrSla.EvalDue(new DateTimeOffset(2026, 11, 13, 14, 30, 0, Tw), cal)
            .Should().Be(new DateTimeOffset(2026, 11, 18, 14, 30, 0, Tw));
    }

    [Fact]
    public void EvalStatus_ResolvedByEvaluation()
    {
        var due = new DateTimeOffset(2026, 11, 17, 14, 30, 0, Tw);
        var now = due.AddDays(5);
        CrSla.EvalStatus(due, due.AddHours(-1), now, CrStatus.PendingDecision).Should().Be(SlaStatus.MetOnTime);
        CrSla.EvalStatus(due, due.AddHours(1), now, CrStatus.PendingDecision).Should().Be(SlaStatus.MetLate);
        CrSla.EvalStatus(due, null, now, CrStatus.Received).Should().Be(SlaStatus.Overdue);
        CrSla.EvalStatus(due, null, now, CrStatus.Cancelled).Should().Be(SlaStatus.NotApplicable);
    }

    private static CrContent Content(Action<Dictionary<string, object?>>? tweak = null) => new(
        "PRJ-CR-001", CrKind.Customer, "新增匯出", "需求描述", "Api,Domain", CrApiImpact.Compatible, CrCouplingImpact.Isolated, false,
        16m, 8m, 4m, CrDecision.A_Delay, 120000m, new DateOnly(2026, 12, 15), null, "說明");

    [Fact]
    public void Hash_IsStable_AndHex64()
    {
        var h1 = CrContentHash.Compute(Content());
        var h2 = CrContentHash.Compute(Content());
        h1.Should().Be(h2).And.HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Hash_ChangesWhenAnyContentChanges()
    {
        var baseHash = CrContentHash.Compute(Content());
        CrContentHash.Compute(Content() with { Title = "新增匯出 PDF" }).Should().NotBe(baseHash);
        CrContentHash.Compute(Content() with { QuoteAmount = 130000m }).Should().NotBe(baseHash);
        CrContentHash.Compute(Content() with { HoursBe = 17m }).Should().NotBe(baseHash);
        CrContentHash.Compute(Content() with { SecurityImpact = true }).Should().NotBe(baseHash);
        CrContentHash.Compute(Content() with { NewReleaseDate = new DateOnly(2026, 12, 16) }).Should().NotBe(baseHash);
        CrContentHash.Compute(Content() with { Decision = CrDecision.B_Swap }).Should().NotBe(baseHash);
    }

    [Fact]
    public void Hash_IgnoresSurroundingWhitespace_InText() =>
        CrContentHash.Compute(Content() with { Title = "  新增匯出  " }).Should().Be(CrContentHash.Compute(Content()));

    [Theory]
    [InlineData("PRJ", 1, "PRJ-CR-001")]
    [InlineData("LIMS2", 42, "LIMS2-CR-042")]
    [InlineData("PRJ", 1000, "PRJ-CR-1000")]
    public void CrNo_Format(string code, int seq, string expected) => CrNumbering.Format(code, seq).Should().Be(expected);
}
