using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class RiskRulesTests
{
    private static readonly Guid Pm = Guid.NewGuid();
    private static readonly Guid Tl = Guid.NewGuid();
    private static readonly Guid Be = Guid.NewGuid();
    private static readonly Guid Fe = Guid.NewGuid();
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Outsider = Guid.NewGuid();

    private static readonly IReadOnlyList<(Guid UserId, RaciRole Role)> Members =
    [
        (Pm, RaciRole.Pm), (Tl, RaciRole.TechLead), (Be, RaciRole.Be), (Fe, RaciRole.Fe), (Customer, RaciRole.Stakeholder)
    ];

    [Theory]
    [MemberData(nameof(EditScopeCases))]
    public void EditScope(string who, Guid user, RiskEditScope expected) =>
        RiskRules.EditScope(user, ownerUserId: Be, Members).Should().Be(expected, who);

    public static IEnumerable<object[]> EditScopeCases()
    {
        yield return ["PM 全部欄位", Pm, RiskEditScope.Full];
        yield return ["Tech Lead 全部欄位", Tl, RiskEditScope.Full];
        yield return ["風險負責人（BE）受限", Be, RiskEditScope.OwnerLimited];
        yield return ["非負責人的 FE 不可編輯", Fe, RiskEditScope.None];
        yield return ["客戶 Stakeholder 不可編輯", Customer, RiskEditScope.None];
        yield return ["非成員不可編輯", Outsider, RiskEditScope.None];
    }

    [Fact]
    public void StakeholderOwner_IsNeverEditable()
    {
        // 即使資料被填成客戶是負責人，客戶也不能編輯（客戶不可見風險表）
        RiskRules.EditScope(Customer, ownerUserId: Customer, Members).Should().Be(RiskEditScope.None);
    }

    [Fact]
    public void CanVoid_OnlyPmAndTechLead()
    {
        RiskRules.CanVoid(Pm, Members).Should().BeTrue();
        RiskRules.CanVoid(Tl, Members).Should().BeTrue();
        RiskRules.CanVoid(Be, Members).Should().BeFalse();
        RiskRules.CanVoid(Customer, Members).Should().BeFalse();
        RiskRules.CanVoid(Outsider, Members).Should().BeFalse();
    }

    [Fact]
    public void ValidOwner_InternalMembersOnly()
    {
        RiskRules.IsValidOwner(Pm, Members).Should().BeTrue();
        RiskRules.IsValidOwner(Tl, Members).Should().BeTrue();
        RiskRules.IsValidOwner(Be, Members).Should().BeTrue();
        RiskRules.IsValidOwner(Fe, Members).Should().BeTrue();
        RiskRules.IsValidOwner(Customer, Members).Should().BeFalse();
        RiskRules.IsValidOwner(Outsider, Members).Should().BeFalse();
    }

    [Theory]
    [InlineData("客戶端窗口異動", RiskLevel.Medium, RiskStatus.Open, null, true)]
    [InlineData(" ", RiskLevel.Medium, RiskStatus.Open, null, false)]
    [InlineData(null, RiskLevel.Medium, RiskStatus.Open, null, false)]
    [InlineData("ok", (RiskLevel)0, RiskStatus.Open, null, false)]
    [InlineData("ok", RiskLevel.High, (RiskStatus)9, null, false)]
    public void ValidateFields(string? title, RiskLevel level, RiskStatus status, string? mitigation, bool ok) =>
        (RiskRules.ValidateFields(title, level, status, mitigation) is null).Should().Be(ok);

    [Fact]
    public void ValidateFields_LengthLimits()
    {
        RiskRules.ValidateFields(new string('字', RiskRules.MaxTitleLength), RiskLevel.Low, RiskStatus.Open, null).Should().BeNull();
        RiskRules.ValidateFields(new string('字', RiskRules.MaxTitleLength + 1), RiskLevel.Low, RiskStatus.Open, null).Should().NotBeNull();
        RiskRules.ValidateFields("t", RiskLevel.Low, RiskStatus.Open, new string('字', RiskRules.MaxMitigationLength + 1)).Should().NotBeNull();
    }

    [Fact]
    public void ClosedAt_SetOnClose_KeptWhileClosed_ClearedOnReopen()
    {
        var closedAt = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

        RiskRules.NextClosedAt(RiskStatus.Open, RiskStatus.Closed, null, now).Should().Be(now);
        RiskRules.NextClosedAt(RiskStatus.Mitigating, RiskStatus.Closed, null, now).Should().Be(now);
        RiskRules.NextClosedAt(RiskStatus.Closed, RiskStatus.Closed, closedAt, now).Should().Be(closedAt);
        RiskRules.NextClosedAt(RiskStatus.Closed, RiskStatus.Mitigating, closedAt, now).Should().BeNull();
        RiskRules.NextClosedAt(RiskStatus.Open, RiskStatus.Mitigating, null, now).Should().BeNull();
    }
}
