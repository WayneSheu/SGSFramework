using FluentAssertions;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using SGSFramework.Core.Abstractions.ReportAccess;
using Xunit;

namespace SGSFramework.Core.Tests.ReportAccess;

public class ReportAccessPolicyTests
{
    private static readonly Guid LabA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid LabB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid UserId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid OtherUser = Guid.Parse("22222222-0000-0000-0000-000000000002");

    private static ReportAccessSubject Subject(
        Guid? lab = null,
        bool isLabMember = true,
        bool isSuperAdmin = false,
        string[]? labCategories = null,
        string[]? signerCategories = null,
        params EffectiveReportGrant[] grants) => new()
        {
            UserId = UserId,
            TenantLabId = lab ?? LabA,
            IsSuperAdmin = isSuperAdmin,
            IsLabMember = isLabMember,
            LabCategoryCodes = labCategories ?? ["FOOD"],
            SignerCategoryCodes = signerCategories ?? [],
            Grants = grants
        };

    private static ReportAccessContext Report(
        ReportLifecycleStatus status = ReportLifecycleStatus.Published,
        ConfidentialityLevel confidentiality = ConfidentialityLevel.Normal,
        bool isBlind = false,
        Guid[]? assigned = null,
        Guid? authorId = null,
        Guid? reviewerId = null,
        string category = "FOOD",
        Guid? lab = null) => new()
        {
            CategoryCode = category,
            TenantLabId = lab ?? LabA,
            Status = status,
            Confidentiality = confidentiality,
            IsBlind = isBlind,
            AssignedUserIds = assigned ?? [],
            AuthorId = authorId,
            ReviewerId = reviewerId
        };

    private static EffectiveReportGrant Grant(
        ReportActions actions,
        ReportDataScope scope = ReportDataScope.Lab,
        ConfidentialityLevel max = ConfidentialityLevel.Normal,
        string category = "FOOD",
        Guid? lab = null) => new(category, lab, actions, scope, max);

    [Fact]
    public void Lab_scope_grant_allows_view_of_published_report_in_own_lab()
    {
        var decision = ReportAccessPolicy.Evaluate(Subject(grants: Grant(ReportActions.View)), Report(), ReportActions.View);

        decision.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Grant_for_other_category_is_denied()
    {
        var subject = Subject(grants: Grant(ReportActions.View, category: "ROHS"));

        ReportAccessPolicy.Evaluate(subject, Report(), ReportActions.View).IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void Lab_scope_requires_lab_membership()
    {
        var subject = Subject(isLabMember: false, grants: Grant(ReportActions.View));

        ReportAccessPolicy.Evaluate(subject, Report(), ReportActions.View).IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void Grant_restricted_to_another_lab_does_not_apply()
    {
        var subject = Subject(grants: Grant(ReportActions.View, lab: LabB));

        ReportAccessPolicy.Evaluate(subject, Report(), ReportActions.View).IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void Global_scope_allows_cross_branch_view_without_membership()
    {
        var subject = Subject(isLabMember: false, grants: Grant(ReportActions.View, ReportDataScope.Global));

        ReportAccessPolicy.Evaluate(subject, Report(), ReportActions.View).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Assigned_scope_requires_assignment()
    {
        var subject = Subject(grants: Grant(ReportActions.View, ReportDataScope.Assigned));

        ReportAccessPolicy.Evaluate(subject, Report(), ReportActions.View).IsAllowed.Should().BeFalse();
        ReportAccessPolicy.Evaluate(subject, Report(assigned: [UserId]), ReportActions.View).IsAllowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(ConfidentialityLevel.Normal, true)]
    [InlineData(ConfidentialityLevel.Confidential, false)]
    public void Confidentiality_above_grant_maximum_is_denied(ConfidentialityLevel level, bool expected)
    {
        var subject = Subject(grants: Grant(ReportActions.View, max: ConfidentialityLevel.Normal));

        ReportAccessPolicy.Evaluate(subject, Report(confidentiality: level), ReportActions.View).IsAllowed.Should().Be(expected);
    }

    [Fact]
    public void Restricted_report_requires_explicit_assignment_even_with_global_grant()
    {
        var subject = Subject(grants: Grant(ReportActions.View, ReportDataScope.Global, ConfidentialityLevel.Restricted));

        ReportAccessPolicy.Evaluate(subject, Report(confidentiality: ConfidentialityLevel.Restricted), ReportActions.View)
            .IsAllowed.Should().BeFalse();
        ReportAccessPolicy.Evaluate(subject, Report(confidentiality: ConfidentialityLevel.Restricted, assigned: [UserId]), ReportActions.View)
            .IsAllowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(ReportLifecycleStatus.Draft, ReportActions.Edit, true)]
    [InlineData(ReportLifecycleStatus.InReview, ReportActions.Edit, false)]
    [InlineData(ReportLifecycleStatus.Published, ReportActions.Edit, false)]
    [InlineData(ReportLifecycleStatus.Published, ReportActions.Download, true)]
    [InlineData(ReportLifecycleStatus.Draft, ReportActions.Download, false)]
    [InlineData(ReportLifecycleStatus.Voided, ReportActions.Download, false)]
    [InlineData(ReportLifecycleStatus.Voided, ReportActions.ViewAudit, true)]
    public void Status_action_matrix(ReportLifecycleStatus status, ReportActions action, bool expected)
    {
        ReportStatusActionMatrix.Allows(status, action).Should().Be(expected);
    }

    [Fact]
    public void Published_report_is_read_only_even_for_super_admin()
    {
        var subject = Subject(isSuperAdmin: true);

        ReportAccessPolicy.Evaluate(subject, Report(ReportLifecycleStatus.Published), ReportActions.Edit).IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void Author_cannot_review_own_report()
    {
        var subject = Subject(grants: Grant(ReportActions.Review));

        var decision = ReportAccessPolicy.Evaluate(subject, Report(ReportLifecycleStatus.InReview, authorId: UserId), ReportActions.Review);

        decision.IsAllowed.Should().BeFalse();
        decision.Reason.Should().Contain("職能分立");
    }

    [Fact]
    public void Reviewer_cannot_sign_same_report_even_as_super_admin()
    {
        var subject = Subject(isSuperAdmin: true, signerCategories: ["FOOD"]);

        ReportAccessPolicy.Evaluate(subject, Report(ReportLifecycleStatus.Reviewed, authorId: OtherUser, reviewerId: UserId), ReportActions.Sign)
            .IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void Sign_requires_signer_qualification_for_category()
    {
        var grant = Grant(ReportActions.Sign);
        var report = Report(ReportLifecycleStatus.Reviewed, authorId: OtherUser);

        ReportAccessPolicy.Evaluate(Subject(grants: grant), report, ReportActions.Sign).IsAllowed.Should().BeFalse();
        ReportAccessPolicy.Evaluate(Subject(signerCategories: ["ROHS"], grants: grant), report, ReportActions.Sign).IsAllowed.Should().BeFalse();
        ReportAccessPolicy.Evaluate(Subject(signerCategories: ["FOOD"], grants: grant), report, ReportActions.Sign).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Production_action_denied_when_lab_does_not_handle_category()
    {
        var subject = Subject(labCategories: ["ENV"], grants: Grant(ReportActions.Edit));

        ReportAccessPolicy.Evaluate(subject, Report(ReportLifecycleStatus.Draft, assigned: [UserId]), ReportActions.Edit)
            .IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void Draft_is_hidden_from_unassigned_reviewer_but_visible_to_manager()
    {
        var draft = Report(ReportLifecycleStatus.Draft);

        ReportAccessPolicy.Evaluate(Subject(grants: Grant(ReportActions.View | ReportActions.Review)), draft, ReportActions.View)
            .IsAllowed.Should().BeFalse();
        ReportAccessPolicy.Evaluate(Subject(grants: Grant(ReportActions.View | ReportActions.Void)), draft, ReportActions.View)
            .IsAllowed.Should().BeTrue();
        ReportAccessPolicy.Evaluate(Subject(grants: Grant(ReportActions.View, ReportDataScope.Assigned)), Report(ReportLifecycleStatus.Draft, assigned: [UserId]), ReportActions.View)
            .IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Blind_report_masks_identity_unless_granted()
    {
        var blind = Report(isBlind: true);

        ReportAccessPolicy.Evaluate(Subject(grants: Grant(ReportActions.View)), blind, ReportActions.View)
            .CanSeeBlindIdentity.Should().BeFalse();
        ReportAccessPolicy.Evaluate(Subject(grants: Grant(ReportActions.View | ReportActions.ViewBlindIdentity)), blind, ReportActions.View)
            .CanSeeBlindIdentity.Should().BeTrue();
        ReportAccessPolicy.Evaluate(Subject(grants: Grant(ReportActions.View)), Report(isBlind: false), ReportActions.View)
            .CanSeeBlindIdentity.Should().BeTrue();
    }

    [Fact]
    public void Combined_action_flags_are_rejected()
    {
        var act = () => ReportAccessPolicy.Evaluate(Subject(), Report(), ReportActions.View | ReportActions.Edit);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Category_level_check_requires_lab_to_handle_category()
    {
        var subject = Subject(labCategories: ["FOOD"], grants: Grant(ReportActions.View, ReportDataScope.Assigned));

        ReportAccessPolicy.HasCategoryAction(subject, ["FOOD", "ENV"], ReportActions.View).Should().BeTrue();
        ReportAccessPolicy.HasCategoryAction(subject, ["ENV"], ReportActions.View).Should().BeFalse();
        ReportAccessPolicy.HasCategoryAction(subject, ["FOOD"], ReportActions.Sign).Should().BeFalse();
    }
}
