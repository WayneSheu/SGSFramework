using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class ProjectAccessPolicyTests
{
    private static readonly Guid Pm = Guid.NewGuid();
    private static readonly Guid Tl = Guid.NewGuid();
    private static readonly Guid Be = Guid.NewGuid();
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Outsider = Guid.NewGuid();

    private static readonly (Guid, RaciRole)[] Members =
    [
        (Pm, RaciRole.Pm), (Tl, RaciRole.TechLead), (Be, RaciRole.Be), (Customer, RaciRole.Stakeholder)
    ];

    [Fact]
    public void NonMember_HasNoAccess() => ProjectAccessPolicy.Evaluate(Outsider, Members).Should().Be(ProjectAccessLevel.None);

    [Theory]
    [MemberData(nameof(InternalUsers))]
    public void InternalMembers_GetInternalAccess(Guid user) =>
        ProjectAccessPolicy.Evaluate(user, Members).Should().Be(ProjectAccessLevel.Internal);

    public static IEnumerable<object[]> InternalUsers() => [[Pm], [Tl], [Be]];

    [Fact]
    public void StakeholderOnly_GetsStakeholderProjection() =>
        ProjectAccessPolicy.Evaluate(Customer, Members).Should().Be(ProjectAccessLevel.Stakeholder);

    [Fact]
    public void StakeholderAlsoInternal_GetsInternal()
    {
        var both = Members.Append((Customer, RaciRole.Fe));
        ProjectAccessPolicy.Evaluate(Customer, both).Should().Be(ProjectAccessLevel.Internal);
    }

    [Fact]
    public void IsTechLead_OnlyForTechLeadRole()
    {
        ProjectAccessPolicy.IsTechLead(Tl, Members).Should().BeTrue();
        ProjectAccessPolicy.IsTechLead(Pm, Members).Should().BeFalse();
        ProjectAccessPolicy.IsTechLead(Outsider, Members).Should().BeFalse();
    }

    [Fact]
    public void IsPm_OnlyForPmRole()
    {
        ProjectAccessPolicy.IsPm(Pm, Members).Should().BeTrue();
        ProjectAccessPolicy.IsPm(Tl, Members).Should().BeFalse();
    }
}
