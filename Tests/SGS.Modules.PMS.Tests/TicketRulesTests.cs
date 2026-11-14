using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class TicketWorkflowTests
{
    [Fact]
    public void Incident_FullHappyPath()
    {
        var s = TicketStatus.Received;
        Step(ref s, TicketAction.Triage, TicketType.Incident).Should().Be(TicketStatus.InProgress);
        Step(ref s, TicketAction.Resolve, TicketType.Incident).Should().Be(TicketStatus.PendingVerify);
        Step(ref s, TicketAction.VerifyPass, TicketType.Incident).Should().Be(TicketStatus.PendingRetest);
        Step(ref s, TicketAction.Close, TicketType.Incident).Should().Be(TicketStatus.Closed);
    }

    private static TicketStatus Step(ref TicketStatus current, TicketAction action, TicketType type)
    {
        var r = TicketWorkflow.Next(current, action, type, out var next);
        r.IsOk.Should().BeTrue(r.Detail);
        current = next;
        return next;
    }

    [Fact]
    public void FailedVerification_ReturnsToInProgress()
    {
        TicketWorkflow.Next(TicketStatus.PendingVerify, TicketAction.VerifyFail, TicketType.Incident, out var next).IsOk.Should().BeTrue();
        next.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public void FailedRetest_Reopens()
    {
        TicketWorkflow.Next(TicketStatus.PendingRetest, TicketAction.Reopen, TicketType.Incident, out var next).IsOk.Should().BeTrue();
        next.Should().Be(TicketStatus.InProgress);
    }

    [Theory]
    [InlineData(TicketType.Consultation)]
    [InlineData(TicketType.DataRequest)]
    [InlineData(TicketType.ChangeRequest)]
    public void NonIncident_CanCloseDirectlyFromInProgress(TicketType type)
    {
        TicketWorkflow.Next(TicketStatus.InProgress, TicketAction.Close, type, out var next).IsOk.Should().BeTrue();
        next.Should().Be(TicketStatus.Closed);
    }

    [Fact]
    public void Incident_CannotCloseWithoutVerification() =>
        TicketWorkflow.Next(TicketStatus.InProgress, TicketAction.Close, TicketType.Incident, out _).Outcome
            .Should().Be(RuleOutcome.InvalidTransition);

    [Theory]
    [InlineData(TicketType.Consultation)]
    [InlineData(TicketType.ChangeRequest)]
    [InlineData(TicketType.DataRequest)]
    public void NonIncident_CannotBeResolvedForVerification(TicketType type) =>
        TicketWorkflow.Next(TicketStatus.InProgress, TicketAction.Resolve, type, out _).IsOk.Should().BeFalse();

    [Theory]
    [InlineData(TicketStatus.Received)]
    [InlineData(TicketStatus.PendingVerify)]
    [InlineData(TicketStatus.PendingRetest)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void Resolve_OnlyFromInProgress(TicketStatus status) =>
        TicketWorkflow.Next(status, TicketAction.Resolve, TicketType.Incident, out _).IsOk.Should().BeFalse();

    [Theory]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.PendingVerify)]
    [InlineData(TicketStatus.Closed)]
    public void Triage_OnlyFromReceived(TicketStatus status) =>
        TicketWorkflow.Next(status, TicketAction.Triage, TicketType.Incident, out _).IsOk.Should().BeFalse();

    [Theory]
    [InlineData(TicketStatus.Received)]
    [InlineData(TicketStatus.InProgress)]
    public void Cancel_FromOpenStates(TicketStatus status)
    {
        TicketWorkflow.Next(status, TicketAction.Cancel, TicketType.Incident, out var next).IsOk.Should().BeTrue();
        next.Should().Be(TicketStatus.Cancelled);
    }

    [Theory]
    [InlineData(TicketStatus.PendingVerify)]
    [InlineData(TicketStatus.PendingRetest)]
    [InlineData(TicketStatus.Closed)]
    public void Cancel_NotAllowedOnceVerifiedOrClosed(TicketStatus status) =>
        TicketWorkflow.Next(status, TicketAction.Cancel, TicketType.Incident, out _).IsOk.Should().BeFalse();

    [Fact]
    public void ClosedTicket_IsTerminal()
    {
        foreach (var action in Enum.GetValues<TicketAction>())
            TicketWorkflow.Next(TicketStatus.Closed, action, TicketType.Incident, out _).IsOk.Should().BeFalse(action.ToString());
    }
}

public class TicketSeparationOfDutiesTests
{
    private static readonly Guid Pm = Guid.NewGuid();
    private static readonly Guid Tl = Guid.NewGuid();
    private static readonly Guid Be = Guid.NewGuid();
    private static readonly Guid Fe = Guid.NewGuid();
    private static readonly Guid Customer = Guid.NewGuid();

    private static readonly (Guid, RaciRole)[] Members =
        [(Pm, RaciRole.Pm), (Tl, RaciRole.TechLead), (Be, RaciRole.Be), (Fe, RaciRole.Fe), (Customer, RaciRole.Stakeholder)];

    [Fact]
    public void FixerAndVerifier_Different_Engineers_Allowed() =>
        TicketSeparationOfDuties.ValidateAssignment(Be, Fe, Members).IsOk.Should().BeTrue();

    [Fact]
    public void TechLead_CanVerify() =>
        TicketSeparationOfDuties.ValidateAssignment(Be, Tl, Members).IsOk.Should().BeTrue();

    [Fact]
    public void SamePerson_IsSodViolation() =>
        TicketSeparationOfDuties.ValidateAssignment(Be, Be, Members).Outcome.Should().Be(RuleOutcome.SeparationOfDuties);

    [Theory]
    [MemberData(nameof(IneligibleUsers))]
    public void NonEngineers_CannotBeFixerOrVerifier(Guid user)
    {
        TicketSeparationOfDuties.ValidateAssignment(user, Be, Members).Outcome.Should().Be(RuleOutcome.RoleNotAllowed);
        TicketSeparationOfDuties.ValidateAssignment(Be, user, Members).Outcome.Should().Be(RuleOutcome.RoleNotAllowed);
    }

    public static IEnumerable<object[]> IneligibleUsers() => [[Pm], [Customer], [Guid.NewGuid()]];

    [Fact]
    public void EmptyIds_Rejected() =>
        TicketSeparationOfDuties.ValidateAssignment(Guid.Empty, Be, Members).IsOk.Should().BeFalse();

    [Fact]
    public void Resolve_OnlyByAssignedFixer()
    {
        TicketSeparationOfDuties.ValidateResolve(Be, Be).IsOk.Should().BeTrue();
        TicketSeparationOfDuties.ValidateResolve(Fe, Be).Outcome.Should().Be(RuleOutcome.RoleNotAllowed);
        TicketSeparationOfDuties.ValidateResolve(Be, null).IsOk.Should().BeFalse();
    }

    [Fact]
    public void Verify_OnlyByAssignedVerifier_NotFixer()
    {
        TicketSeparationOfDuties.ValidateVerify(Fe, Be, Fe).IsOk.Should().BeTrue();
        TicketSeparationOfDuties.ValidateVerify(Be, Be, Fe).Outcome.Should().Be(RuleOutcome.SeparationOfDuties);
        TicketSeparationOfDuties.ValidateVerify(Tl, Be, Fe).Outcome.Should().Be(RuleOutcome.RoleNotAllowed);
        TicketSeparationOfDuties.ValidateVerify(Fe, Be, null).IsOk.Should().BeFalse();
    }

    [Fact]
    public void Verify_FixerAlsoMarkedVerifier_StillRejected() =>
        // 防禦：即使資料被竄改成修復人＝驗證人，驗證仍以 SoD 拒絕
        TicketSeparationOfDuties.ValidateVerify(Be, Be, Be).Outcome.Should().Be(RuleOutcome.SeparationOfDuties);
}

public class TicketNumberingAndIssueNamingTests
{
    [Theory]
    [InlineData("PRJ", 1, "PRJ-001")]
    [InlineData("PRJ", 42, "PRJ-042")]
    [InlineData("LIMS2", 999, "LIMS2-999")]
    [InlineData("PRJ", 1000, "PRJ-1000")]
    public void Format(string code, int seq, string expected) => TicketNumbering.Format(code, seq).Should().Be(expected);

    [Fact]
    public void Format_RejectsNonPositive()
    {
        var act = () => TicketNumbering.Format("PRJ", 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IssueName_IsStandard()
    {
        var r = DeliverableNaming.GenerateIssue(new DateOnly(2026, 11, 14), "prj-001", "結帳錯誤畫面", "PNG");
        r.Success.Should().BeTrue();
        r.FileName.Should().Be("20261114_PRJ-001_結帳錯誤畫面.png");
        DeliverableNaming.Check(NamingKind.Issue, r.FileName!).Valid.Should().BeTrue();
    }

    [Fact]
    public void IssueName_CleansUnderscoresAndPathChars()
    {
        var r = DeliverableNaming.GenerateIssue(new DateOnly(2026, 11, 14), "PRJ-001", @"a_b\c/d:e", ".png");
        r.Success.Should().BeTrue();
        r.FileName.Should().NotContainAny("\\", "/", ":");
        DeliverableNaming.Check(NamingKind.Issue, r.FileName!).Valid.Should().BeTrue();
    }

    [Theory]
    [InlineData("PRJ-1")]
    [InlineData("001")]
    [InlineData("")]
    public void IssueName_RejectsBadTicketNo(string ticketNo) =>
        DeliverableNaming.GenerateIssue(new DateOnly(2026, 11, 14), ticketNo, "x", ".png").Success.Should().BeFalse();

    [Fact]
    public void IssueName_RejectsEmptyDescription_AndBadExtension()
    {
        DeliverableNaming.GenerateIssue(new DateOnly(2026, 11, 14), "PRJ-001", " ", ".png").Success.Should().BeFalse();
        DeliverableNaming.GenerateIssue(new DateOnly(2026, 11, 14), "PRJ-001", "x", "").Success.Should().BeFalse();
    }
}
