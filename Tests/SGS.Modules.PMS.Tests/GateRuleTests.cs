using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class GateRuleTests
{
    private static Dictionary<GateKind, GateStatus> None => new();

    private static Dictionary<GateKind, GateStatus> Approved(params GateKind[] gates) =>
        gates.ToDictionary(g => g, _ => GateStatus.Approved);

    [Fact]
    public void FirstGate_CanBeApproved_ByTechLead() =>
        GateRule.CanDecide(GateKind.Design, GateStatus.Approved, None, actorIsTechLead: true).IsOk.Should().BeTrue();

    [Fact]
    public void NonTechLead_CannotDecide()
    {
        var result = GateRule.CanDecide(GateKind.Design, GateStatus.Approved, None, actorIsTechLead: false);
        result.Outcome.Should().Be(RuleOutcome.RoleNotAllowed);
    }

    [Theory]
    [InlineData(GateKind.Build)]
    [InlineData(GateKind.Verification)]
    [InlineData(GateKind.Release)]
    public void LaterGate_RequiresPreviousApproved(GateKind gate)
    {
        var result = GateRule.CanDecide(gate, GateStatus.Approved, None, actorIsTechLead: true);
        result.Outcome.Should().Be(RuleOutcome.GatePrerequisiteNotMet);
    }

    [Fact]
    public void SkippingAGate_IsRejected()
    {
        // Design 已過，直接放行 Verification（跳過 Build）
        var result = GateRule.CanDecide(GateKind.Verification, GateStatus.Approved, Approved(GateKind.Design), true);
        result.Outcome.Should().Be(RuleOutcome.GatePrerequisiteNotMet);
        result.Detail.Should().Contain("Build");
    }

    [Fact]
    public void SequentialApproval_Works()
    {
        GateRule.CanDecide(GateKind.Build, GateStatus.Approved, Approved(GateKind.Design), true).IsOk.Should().BeTrue();
        GateRule.CanDecide(GateKind.Release, GateStatus.Approved, Approved(GateKind.Design, GateKind.Build, GateKind.Verification), true).IsOk.Should().BeTrue();
    }

    [Fact]
    public void AlreadyApproved_CannotBeDecidedAgain()
    {
        var result = GateRule.CanDecide(GateKind.Design, GateStatus.Rejected, Approved(GateKind.Design), true);
        result.Outcome.Should().Be(RuleOutcome.InvalidTransition);
    }

    [Fact]
    public void Rejection_DoesNotRequirePreviousGate() =>
        GateRule.CanDecide(GateKind.Build, GateStatus.Rejected, None, true).IsOk.Should().BeTrue();

    [Fact]
    public void PendingIsNotAValidDecision() =>
        GateRule.CanDecide(GateKind.Design, GateStatus.Pending, None, true).Outcome.Should().Be(RuleOutcome.InvalidTransition);

    [Theory]
    [InlineData(ProjectPhase.Initiation, ProjectPhase.Requirement)]
    [InlineData(ProjectPhase.Requirement, ProjectPhase.Design)]
    [InlineData(ProjectPhase.Design, ProjectPhase.Development)] // 需 Design Gate
    public void Advance_OneStepAtATime(ProjectPhase from, ProjectPhase to)
    {
        var gates = Approved(GateKind.Design);
        GateRule.CanAdvance(from, to, gates).IsOk.Should().BeTrue();
    }

    [Fact]
    public void Advance_ToDevelopment_RequiresDesignGate()
    {
        var result = GateRule.CanAdvance(ProjectPhase.Design, ProjectPhase.Development, None);
        result.Outcome.Should().Be(RuleOutcome.GatePrerequisiteNotMet);
        result.Detail.Should().Contain("Design");
    }

    [Fact]
    public void Advance_ToTesting_RequiresBuildGate()
    {
        GateRule.CanAdvance(ProjectPhase.Development, ProjectPhase.Testing, Approved(GateKind.Design)).Outcome
            .Should().Be(RuleOutcome.GatePrerequisiteNotMet);
        GateRule.CanAdvance(ProjectPhase.Development, ProjectPhase.Testing, Approved(GateKind.Design, GateKind.Build)).IsOk
            .Should().BeTrue();
    }

    [Fact]
    public void Advance_ToDeployClose_RequiresVerificationGate()
    {
        var gates = Approved(GateKind.Design, GateKind.Build);
        GateRule.CanAdvance(ProjectPhase.Testing, ProjectPhase.DeployClose, gates).Outcome.Should().Be(RuleOutcome.GatePrerequisiteNotMet);
        gates[GateKind.Verification] = GateStatus.Approved;
        GateRule.CanAdvance(ProjectPhase.Testing, ProjectPhase.DeployClose, gates).IsOk.Should().BeTrue();
    }

    [Theory]
    [InlineData(ProjectPhase.Initiation, ProjectPhase.Design)]   // 跳階
    [InlineData(ProjectPhase.Design, ProjectPhase.Requirement)]  // 倒退
    [InlineData(ProjectPhase.Design, ProjectPhase.Design)]       // 原地
    public void Advance_SkipOrBackward_Rejected(ProjectPhase from, ProjectPhase to) =>
        GateRule.CanAdvance(from, to, Approved(GateKind.Design, GateKind.Build, GateKind.Verification)).Outcome
            .Should().Be(RuleOutcome.InvalidTransition);

    [Fact]
    public void RejectedGate_DoesNotSatisfyPhaseRequirement()
    {
        var gates = new Dictionary<GateKind, GateStatus> { [GateKind.Design] = GateStatus.Rejected };
        GateRule.CanAdvance(ProjectPhase.Design, ProjectPhase.Development, gates).Outcome.Should().Be(RuleOutcome.GatePrerequisiteNotMet);
    }

    [Fact]
    public void Order_IsDesignBuildVerificationRelease() =>
        GateRule.Order.Should().Equal(GateKind.Design, GateKind.Build, GateKind.Verification, GateKind.Release);

    [Fact]
    public void Previous_OfFirstGate_IsNull() => GateRule.Previous(GateKind.Design).Should().BeNull();
}
