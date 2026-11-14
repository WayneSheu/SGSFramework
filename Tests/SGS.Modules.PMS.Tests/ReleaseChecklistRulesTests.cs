using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class ReleaseChecklistRulesTests
{
    private static readonly Guid Pm = Guid.NewGuid();
    private static readonly Guid Tl = Guid.NewGuid();
    private static readonly Guid BeAuthor = Guid.NewGuid();
    private static readonly Guid Fe = Guid.NewGuid();
    private static readonly Guid Outsider = Guid.NewGuid();

    private static readonly IReadOnlyList<(Guid UserId, RaciRole Role)> Members =
    [
        (Pm, RaciRole.Pm), (Tl, RaciRole.TechLead), (BeAuthor, RaciRole.Be), (Fe, RaciRole.Fe)
    ];

    private static readonly ReleaseOpenItems NoOpen = new([], []);

    [Fact]
    public void Template_Has17Items_Sec11_Rel6_AndNineBlockingRulesMatchSop()
    {
        ReleaseChecklistTemplate.Items.Should().HaveCount(17);
        ReleaseChecklistTemplate.Items.Count(i => i.Code.StartsWith("SEC-")).Should().Be(11);
        ReleaseChecklistTemplate.Items.Count(i => i.Code.StartsWith("REL-")).Should().Be(6);
        ReleaseChecklistTemplate.Items.Select(i => i.Code).Should().OnlyHaveUniqueItems();
        // SOP 附錄 A：警告項只有 SEC-07 與 SEC-11
        ReleaseChecklistTemplate.Items.Where(i => i.Severity == ReleaseSeverity.Warning).Select(i => i.Code)
            .Should().BeEquivalentTo(["SEC-07", "SEC-11"]);
        ReleaseChecklistTemplate.Find("rel-06").Should().NotBeNull();
        ReleaseChecklistTemplate.Find("XYZ-01").Should().BeNull();
    }

    [Fact]
    public void OpenItems_PassAndNotApplicableAreDone()
    {
        var open = ReleaseChecklistRules.OpenItems(
        [
            ("SEC-01", ReleaseSeverity.Blocking, ReleaseItemResult.Pass),
            ("SEC-02", ReleaseSeverity.Blocking, ReleaseItemResult.NotApplicable),
            ("SEC-03", ReleaseSeverity.Blocking, ReleaseItemResult.Fail),
            ("SEC-04", ReleaseSeverity.Blocking, ReleaseItemResult.Pending),
            ("SEC-07", ReleaseSeverity.Warning, ReleaseItemResult.Pending),
            ("SEC-11", ReleaseSeverity.Warning, ReleaseItemResult.Pass)
        ]);

        open.Blocking.Should().Equal("SEC-03", "SEC-04");
        open.Warning.Should().Equal("SEC-07");
    }

    [Theory]
    [InlineData(ReleaseChecklistStatus.Draft, ReleaseSeverity.Blocking, ReleaseItemResult.Pass, null, true)]
    [InlineData(ReleaseChecklistStatus.Draft, ReleaseSeverity.Blocking, ReleaseItemResult.Fail, null, true)]
    [InlineData(ReleaseChecklistStatus.Draft, ReleaseSeverity.Warning, ReleaseItemResult.NotApplicable, null, true)]
    [InlineData(ReleaseChecklistStatus.Draft, ReleaseSeverity.Blocking, ReleaseItemResult.NotApplicable, "本專案無上傳功能", true)]
    [InlineData(ReleaseChecklistStatus.Draft, ReleaseSeverity.Blocking, ReleaseItemResult.NotApplicable, null, false)]
    [InlineData(ReleaseChecklistStatus.Draft, ReleaseSeverity.Blocking, ReleaseItemResult.Pending, null, false)]
    [InlineData(ReleaseChecklistStatus.Released, ReleaseSeverity.Blocking, ReleaseItemResult.Pass, null, false)]
    [InlineData(ReleaseChecklistStatus.Confirmed, ReleaseSeverity.Warning, ReleaseItemResult.Pass, null, false)]
    public void ValidateItemUpdate(ReleaseChecklistStatus status, ReleaseSeverity severity, ReleaseItemResult result, string? note, bool ok) =>
        (ReleaseChecklistRules.ValidateItemUpdate(status, severity, result, note) is null).Should().Be(ok);

    [Fact]
    public void CrossVerifier_MustNotBeMainAuthor_SoD()
    {
        var verdict = Sign(ReleaseSignRole.CrossVerifier, BeAuthor, mainAuthor: BeAuthor);
        verdict.IsOk.Should().BeFalse();
        verdict.Result.Outcome.Should().Be(RuleOutcome.SeparationOfDuties);

        Sign(ReleaseSignRole.CrossVerifier, Fe, mainAuthor: BeAuthor).IsOk.Should().BeTrue();
    }

    [Fact]
    public void CrossVerifier_PmAndOutsiderNotAllowed()
    {
        Sign(ReleaseSignRole.CrossVerifier, Pm, mainAuthor: BeAuthor).Result.Outcome.Should().Be(RuleOutcome.RoleNotAllowed);
        Sign(ReleaseSignRole.CrossVerifier, Outsider, mainAuthor: BeAuthor).Result.Outcome.Should().Be(RuleOutcome.RoleNotAllowed);
    }

    [Fact]
    public void TechLead_RequiresCrossVerifierFirst()
    {
        var verdict = Sign(ReleaseSignRole.TechLead, Tl, signed: []);
        verdict.IsOk.Should().BeFalse();
        verdict.Result.Outcome.Should().Be(RuleOutcome.InvalidTransition);
    }

    [Fact]
    public void TechLead_OnlyTechLead()
    {
        Sign(ReleaseSignRole.TechLead, Pm, signed: [ReleaseSignRole.CrossVerifier]).Result.Outcome.Should().Be(RuleOutcome.RoleNotAllowed);
        Sign(ReleaseSignRole.TechLead, Tl, signed: [ReleaseSignRole.CrossVerifier]).IsOk.Should().BeTrue();
    }

    [Fact]
    public void TechLead_BlockedByOpenBlockingItems_ListsThem()
    {
        var open = new ReleaseOpenItems(["SEC-08", "REL-06"], []);
        var verdict = Sign(ReleaseSignRole.TechLead, Tl, signed: [ReleaseSignRole.CrossVerifier], open: open);

        verdict.IsOk.Should().BeFalse();
        verdict.Result.Outcome.Should().Be(RuleOutcome.GatePrerequisiteNotMet);
        verdict.BlockingCodes.Should().Equal("SEC-08", "REL-06");
    }

    [Fact]
    public void TechLead_OpenWarning_NeedsExceptionNote()
    {
        var open = new ReleaseOpenItems([], ["SEC-11"]);
        Sign(ReleaseSignRole.TechLead, Tl, signed: [ReleaseSignRole.CrossVerifier], open: open, note: null).IsOk.Should().BeFalse();
        Sign(ReleaseSignRole.TechLead, Tl, signed: [ReleaseSignRole.CrossVerifier], open: open, note: "  ").IsOk.Should().BeFalse();
        Sign(ReleaseSignRole.TechLead, Tl, signed: [ReleaseSignRole.CrossVerifier], open: open, note: "SEC-11 預計 11/30 補齊").IsOk.Should().BeTrue();
    }

    [Fact]
    public void Pm_OnlyAfterRelease_AndOnlyPm()
    {
        Sign(ReleaseSignRole.Pm, Pm, status: ReleaseChecklistStatus.Draft).IsOk.Should().BeFalse();
        Sign(ReleaseSignRole.Pm, Tl, status: ReleaseChecklistStatus.Released).Result.Outcome.Should().Be(RuleOutcome.RoleNotAllowed);
        Sign(ReleaseSignRole.Pm, Pm, status: ReleaseChecklistStatus.Released,
            signed: [ReleaseSignRole.CrossVerifier, ReleaseSignRole.TechLead]).IsOk.Should().BeTrue();
    }

    [Fact]
    public void SameRoleCannotSignTwice()
    {
        Sign(ReleaseSignRole.CrossVerifier, Fe, mainAuthor: BeAuthor, signed: [ReleaseSignRole.CrossVerifier]).IsOk.Should().BeFalse();
    }

    [Fact]
    public void CrossVerifier_CannotSignAfterRelease() =>
        Sign(ReleaseSignRole.CrossVerifier, Fe, mainAuthor: BeAuthor, status: ReleaseChecklistStatus.Released, signed: []).IsOk.Should().BeFalse();

    [Fact]
    public void SignableRoles_ReflectIdentityAndProgress()
    {
        ReleaseChecklistRules.SignableRoles(Fe, Members, BeAuthor, ReleaseChecklistStatus.Draft, []).Should().Equal(ReleaseSignRole.CrossVerifier);
        ReleaseChecklistRules.SignableRoles(BeAuthor, Members, BeAuthor, ReleaseChecklistStatus.Draft, []).Should().BeEmpty();
        // Tech Lead 本人可交叉驗證（非主要作者）；交叉驗證完成後才能放行
        ReleaseChecklistRules.SignableRoles(Tl, Members, BeAuthor, ReleaseChecklistStatus.Draft, [ReleaseSignRole.CrossVerifier])
            .Should().Equal(ReleaseSignRole.TechLead);
        ReleaseChecklistRules.SignableRoles(Pm, Members, BeAuthor, ReleaseChecklistStatus.Released,
            [ReleaseSignRole.CrossVerifier, ReleaseSignRole.TechLead]).Should().Equal(ReleaseSignRole.Pm);
        ReleaseChecklistRules.SignableRoles(Outsider, Members, BeAuthor, ReleaseChecklistStatus.Draft, []).Should().BeEmpty();
    }

    private static ReleaseSignVerdict Sign(
        ReleaseSignRole role, Guid actor, Guid? mainAuthor = null,
        ReleaseChecklistStatus status = ReleaseChecklistStatus.Draft,
        IReadOnlyCollection<ReleaseSignRole>? signed = null, ReleaseOpenItems? open = null, string? note = null) =>
        ReleaseChecklistRules.ValidateSign(role, actor, Members, mainAuthor ?? BeAuthor, status, signed ?? [], open ?? NoOpen, note);
}
