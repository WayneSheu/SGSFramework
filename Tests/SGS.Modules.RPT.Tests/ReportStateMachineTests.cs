using FluentAssertions;
using SGS.Modules.RPT.Infrastructure.Domain;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using SGSFramework.Core.Abstractions.ReportAccess;
using Xunit;

namespace SGS.Modules.RPT.Tests;

public class ReportStateMachineTests
{
    [Theory]
    [InlineData(ReportTransition.Submit, ReportLifecycleStatus.Draft, ReportLifecycleStatus.InReview)]
    [InlineData(ReportTransition.ApproveReview, ReportLifecycleStatus.InReview, ReportLifecycleStatus.Reviewed)]
    [InlineData(ReportTransition.RejectReview, ReportLifecycleStatus.InReview, ReportLifecycleStatus.Draft)]
    [InlineData(ReportTransition.Sign, ReportLifecycleStatus.Reviewed, ReportLifecycleStatus.Published)]
    [InlineData(ReportTransition.RejectSign, ReportLifecycleStatus.Reviewed, ReportLifecycleStatus.Draft)]
    [InlineData(ReportTransition.Void, ReportLifecycleStatus.Published, ReportLifecycleStatus.Voided)]
    public void Transition_rules_define_from_and_to(ReportTransition transition, ReportLifecycleStatus from, ReportLifecycleStatus to)
    {
        var rule = ReportStateMachine.RuleFor(transition);

        rule.From.Should().Be(from);
        rule.To.Should().Be(to);
        ReportStateMachine.Validate(from, transition, "原因").Should().BeNull();
    }

    [Theory]
    [InlineData(ReportLifecycleStatus.Published, ReportTransition.Submit)]
    [InlineData(ReportLifecycleStatus.Draft, ReportTransition.Sign)]
    [InlineData(ReportLifecycleStatus.InReview, ReportTransition.Sign)]
    [InlineData(ReportLifecycleStatus.Voided, ReportTransition.Void)]
    public void Invalid_transition_for_current_status_is_rejected(ReportLifecycleStatus current, ReportTransition transition)
    {
        ReportStateMachine.Validate(current, transition, "原因").Should().NotBeNull();
    }

    [Theory]
    [InlineData(ReportTransition.RejectReview, ReportLifecycleStatus.InReview)]
    [InlineData(ReportTransition.RejectSign, ReportLifecycleStatus.Reviewed)]
    [InlineData(ReportTransition.Void, ReportLifecycleStatus.Published)]
    public void Reject_and_void_require_reason(ReportTransition transition, ReportLifecycleStatus current)
    {
        ReportStateMachine.Validate(current, transition, "  ").Should().Contain("原因");
    }

    [Fact]
    public void Every_transition_required_action_is_permitted_by_status_matrix_in_source_status()
    {
        foreach (var transition in Enum.GetValues<ReportTransition>())
        {
            var rule = ReportStateMachine.RuleFor(transition);
            ReportStatusActionMatrix.Allows(rule.From, rule.RequiredAction).Should().BeTrue($"{transition} 需要 {rule.RequiredAction}");
        }
    }

    [Fact]
    public void Published_report_offers_no_edit_transitions()
    {
        ReportStateMachine.AvailableFrom(ReportLifecycleStatus.Published).Should().BeEquivalentTo([ReportTransition.Void]);
        ReportStateMachine.AvailableFrom(ReportLifecycleStatus.Voided).Should().BeEmpty();
    }
}
