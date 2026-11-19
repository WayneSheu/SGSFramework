using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Application.Services;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class ContractRulesTests
{
    private static readonly Guid Sales = Guid.NewGuid();
    private static readonly Guid Pm = Guid.NewGuid();
    private static readonly Guid Legal = Guid.NewGuid();
    private static readonly Guid Exec = Guid.NewGuid();

    private static readonly IReadOnlyList<(Guid UserId, ContractMemberRole Role)> Members =
    [
        (Sales, ContractMemberRole.Sales), (Pm, ContractMemberRole.Pm), (Legal, ContractMemberRole.Legal), (Exec, ContractMemberRole.Executive)
    ];

    private static Contract Standard() => new()
    {
        Title = "測試合約", CustomerName = "客戶", TotalAmount = 1_000_000m, DeliveryDate = new DateOnly(2027, 6, 30)
    };

    private static List<ContractReviewItem> Items(ReviewResult result = ReviewResult.Compliant, string? note = null) =>
        ContractRules.ReviewCatalog.Select(s => new ContractReviewItem { Code = s.Code, Result = result, Note = note }).ToList();

    // ---------- 條款驗證 ----------

    [Fact]
    public void StandardTerms_AreValid() => ContractRules.ValidateTerms(Standard()).Should().BeNull();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Title_Required(string title)
    {
        var c = Standard(); c.Title = title;
        ContractRules.ValidateTerms(c).Should().Contain("合約名稱");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void CoopNoticeWorkdays_MustBeInRange(int days)
    {
        var c = Standard(); c.CoopNoticeWorkdays = days;
        ContractRules.ValidateTerms(c).Should().Contain("通知期限");
    }

    [Fact]
    public void CoopNoticeWorkdays_DefaultIsThree() => Standard().CoopNoticeWorkdays.Should().Be(3);

    [Fact]
    public void FixedAmountCap_RequiresAmount()
    {
        var c = Standard(); c.LiabilityCap = LiabilityCapType.FixedAmount; c.LiabilityCapAmount = null;
        ContractRules.ValidateTerms(c).Should().Contain("固定金額");
    }

    [Fact]
    public void DeliveryBeforeStart_IsRejected()
    {
        var c = Standard(); c.StartDate = new DateOnly(2027, 7, 1);
        ContractRules.ValidateTerms(c).Should().Contain("交付日");
    }

    [Theory]
    [InlineData("a@b.com", true)]
    [InlineData("a@b", false)]
    [InlineData("a b@c.com", false)]
    [InlineData("@b.com", false)]
    public void ContactEmail_IsChecked(string email, bool ok)
    {
        var c = Standard(); c.CustomerContactEmail = email;
        (ContractRules.ValidateTerms(c) is null).Should().Be(ok);
    }

    // ---------- 自動比對乙方底線 ----------

    [Fact]
    public void StandardTerms_HaveNoFlags() => ContractRules.AutoFlags(Standard()).Should().BeEmpty();

    [Fact]
    public void UnlimitedLiability_IsCritical()
    {
        var c = Standard(); c.LiabilityCap = LiabilityCapType.None;
        var flag = ContractRules.AutoFlags(c).Single(f => f.ItemCode == "LIABILITY");
        flag.Severity.Should().Be(ContractFlagSeverity.Critical);
    }

    [Fact]
    public void BadTerms_FlagEachReviewItem()
    {
        var c = Standard();
        c.OpenEndedScope = true; c.ReviewDeadlineWorkdays = null; c.IpRule = IpRule.AllToCustomer; c.ScanRule = ScanRule.AllIncludingLow;
        c.DeemedAcceptanceWorkdays = null; c.ExcludesIndirectLoss = false; c.IncidentNotifyHours = 1;

        var codes = ContractRules.AutoFlags(c).Select(f => f.ItemCode).Distinct().ToList();

        codes.Should().BeEquivalentTo("SCOPE", "DOC_REVIEW", "IPR", "VULN_SCAN", "ACCEPTANCE", "LIABILITY", "SECURITY");
        ContractRules.WorstSeverity(ContractRules.AutoFlags(c)).Should().Be(ContractFlagSeverity.High);
    }

    [Fact]
    public void FixedCapAboveTotal_IsMediumFlag()
    {
        var c = Standard(); c.LiabilityCap = LiabilityCapType.FixedAmount; c.LiabilityCapAmount = 2_000_000m;
        ContractRules.AutoFlags(c).Should().ContainSingle(f => f.Severity == ContractFlagSeverity.Medium && f.ItemCode == "LIABILITY");
    }

    [Fact]
    public void WorstSeverity_OfNoFlags_IsNull() => ContractRules.WorstSeverity([]).Should().BeNull();

    // ---------- 送審 ----------

    [Fact]
    public void Submit_AllCompliantStandard_HasNoProblems() =>
        ContractRules.ValidateSubmit(Standard(), Items(), Members).Should().BeEmpty();

    [Fact]
    public void Submit_PendingItems_AreListed()
    {
        var items = Items(); items[0].Result = ReviewResult.Pending;
        ContractRules.ValidateSubmit(Standard(), items, Members).Should().Contain(p => p.Contains("需求範疇"));
    }

    [Fact]
    public void Submit_CompliantButFlagged_IsRejected()
    {
        var c = Standard(); c.LiabilityCap = LiabilityCapType.None;
        ContractRules.ValidateSubmit(c, Items(), Members).Should().Contain(p => p.Contains("賠償責任") && p.Contains("不可標為"));
    }

    [Fact]
    public void Submit_DeviationWithoutNote_IsRejected()
    {
        var c = Standard(); c.LiabilityCap = LiabilityCapType.None;
        var items = Items(); items.Single(i => i.Code == "LIABILITY").Result = ReviewResult.Deviation;
        ContractRules.ValidateSubmit(c, items, Members).Should().Contain(p => p.Contains("請填寫說明"));
    }

    [Fact]
    public void Submit_DeviationWithNote_Passes()
    {
        var c = Standard(); c.LiabilityCap = LiabilityCapType.None;
        var items = Items(); var item = items.Single(i => i.Code == "LIABILITY"); item.Result = ReviewResult.Deviation; item.Note = "客戶堅持，報主管核決";
        ContractRules.ValidateSubmit(c, items, Members).Should().BeEmpty();
    }

    [Fact]
    public void Submit_MissingRoles_AreListed()
    {
        var problems = ContractRules.ValidateSubmit(Standard(), Items(), [(Sales, ContractMemberRole.Sales)]);
        problems.Should().Contain(p => p.Contains("合約 PM")).And.Contain(p => p.Contains("核決主管"));
    }

    [Fact]
    public void Submit_WithDeviationButNoLegal_Fails()
    {
        var c = Standard(); c.LiabilityCap = LiabilityCapType.None;
        var items = Items(); var item = items.Single(i => i.Code == "LIABILITY"); item.Result = ReviewResult.Deviation; item.Note = "x";
        var members = Members.Where(m => m.Role != ContractMemberRole.Legal).ToList();
        ContractRules.ValidateSubmit(c, items, members).Should().Contain(p => p.Contains("法務"));
    }

    [Fact]
    public void Submit_RequiresAmountAndDeliveryDate()
    {
        var c = Standard(); c.TotalAmount = 0; c.DeliveryDate = null;
        var problems = ContractRules.ValidateSubmit(c, Items(), Members);
        problems.Should().Contain(p => p.Contains("總價金")).And.Contain(p => p.Contains("交付日"));
    }

    // ---------- 核決步驟與職責分離 ----------

    [Fact]
    public void RequiredSteps_StandardSkipsLegal() =>
        ContractRules.RequiredSteps(Items(), []).Should().Equal(ContractApprovalStep.Pm, ContractApprovalStep.Executive);

    [Fact]
    public void RequiredSteps_DeviationAddsLegal() =>
        ContractRules.RequiredSteps(Items(ReviewResult.Deviation, "x"), []).Should().Equal(ContractApprovalStep.Pm, ContractApprovalStep.Legal, ContractApprovalStep.Executive);

    [Fact]
    public void NextStep_FollowsOrderAndSkipsApproved()
    {
        var required = new[] { ContractApprovalStep.Pm, ContractApprovalStep.Legal, ContractApprovalStep.Executive };
        ContractRules.NextStep(required, []).Should().Be(ContractApprovalStep.Pm);
        ContractRules.NextStep(required, [Approval(ContractApprovalStep.Pm, Pm)]).Should().Be(ContractApprovalStep.Legal);
        ContractRules.NextStep(required, [Approval(ContractApprovalStep.Pm, Pm), Approval(ContractApprovalStep.Legal, Legal), Approval(ContractApprovalStep.Executive, Exec)])
            .Should().BeNull();
    }

    [Fact]
    public void Decision_OutOfOrder_IsRejected() =>
        ContractRules.ValidateDecision(ContractApprovalStep.Executive, ApprovalDecision.Approved, null, Exec, Sales,
            ContractApprovalStep.Pm, Members, []).Should().Contain("目前輪到");

    [Fact]
    public void Decision_RequiresMatchingRole() =>
        ContractRules.ValidateDecision(ContractApprovalStep.Pm, ApprovalDecision.Approved, null, Sales, Sales,
            ContractApprovalStep.Pm, Members, []).Should().Contain("合約 PM");

    [Fact]
    public void Decision_SubmitterCannotDecide()
    {
        var members = new List<(Guid, ContractMemberRole)> { (Pm, ContractMemberRole.Pm), (Pm, ContractMemberRole.Sales) };
        ContractRules.ValidateDecision(ContractApprovalStep.Pm, ApprovalDecision.Approved, null, Pm, Pm,
            ContractApprovalStep.Pm, members, []).Should().Contain("職責分離");
    }

    [Fact]
    public void Decision_SamePersonCannotApproveTwoSteps()
    {
        var both = Guid.NewGuid();
        var members = new List<(Guid, ContractMemberRole)> { (both, ContractMemberRole.Legal), (both, ContractMemberRole.Executive), (Sales, ContractMemberRole.Sales) };
        var cycle = new[] { Approval(ContractApprovalStep.Legal, both) };
        ContractRules.ValidateDecision(ContractApprovalStep.Executive, ApprovalDecision.Approved, null, both, Sales,
            ContractApprovalStep.Executive, members, cycle).Should().Contain("同一人");
    }

    [Theory]
    [InlineData(ApprovalDecision.Returned)]
    [InlineData(ApprovalDecision.Rejected)]
    public void Decision_ReturnOrRejectRequiresComment(ApprovalDecision decision) =>
        ContractRules.ValidateDecision(ContractApprovalStep.Pm, decision, " ", Pm, Sales, ContractApprovalStep.Pm, Members, []).Should().Contain("意見");

    [Fact]
    public void Decision_ValidApproval_Passes() =>
        ContractRules.ValidateDecision(ContractApprovalStep.Pm, ApprovalDecision.Approved, null, Pm, Sales, ContractApprovalStep.Pm, Members, []).Should().BeNull();

    [Fact]
    public void Decision_SeparationDisabled_SubmitterMayDecide()
    {
        var members = new List<(Guid, ContractMemberRole)> { (Pm, ContractMemberRole.Pm), (Pm, ContractMemberRole.Sales) };
        ContractRules.ValidateDecision(ContractApprovalStep.Pm, ApprovalDecision.Approved, null, Pm, Pm,
            ContractApprovalStep.Pm, members, [], enforceSeparation: false).Should().BeNull();
    }

    [Fact]
    public void Decision_SeparationDisabled_SamePersonMayApproveTwoSteps()
    {
        var both = Guid.NewGuid();
        var members = new List<(Guid, ContractMemberRole)> { (both, ContractMemberRole.Legal), (both, ContractMemberRole.Executive), (Sales, ContractMemberRole.Sales) };
        var cycle = new[] { Approval(ContractApprovalStep.Legal, both) };
        ContractRules.ValidateDecision(ContractApprovalStep.Executive, ApprovalDecision.Approved, null, both, Sales,
            ContractApprovalStep.Executive, members, cycle, enforceSeparation: false).Should().BeNull();
    }

    [Fact]
    public void Decision_SeparationDisabled_StillRequiresOrderAndRole()
    {
        ContractRules.ValidateDecision(ContractApprovalStep.Executive, ApprovalDecision.Approved, null, Exec, Sales,
            ContractApprovalStep.Pm, Members, [], enforceSeparation: false).Should().Contain("目前輪到");
        ContractRules.ValidateDecision(ContractApprovalStep.Pm, ApprovalDecision.Approved, null, Sales, Sales,
            ContractApprovalStep.Pm, Members, [], enforceSeparation: false).Should().Contain("合約 PM");
    }

    [Fact]
    public void Decision_WhenAllDone_IsRejected() =>
        ContractRules.ValidateDecision(ContractApprovalStep.Pm, ApprovalDecision.Approved, null, Pm, Sales, null, Members, []).Should().Contain("已完成");

    // ---------- 簽約與快照 ----------

    [Fact]
    public void Hash_ChangesWhenTermsOrReviewChange()
    {
        var c = Standard(); var items = Items();
        var baseline = ContractRules.ComputeSnapshotHash(c, items);

        baseline.Should().HaveLength(64).And.Be(ContractRules.ComputeSnapshotHash(c, items));

        c.TotalAmount += 1;
        ContractRules.ComputeSnapshotHash(c, items).Should().NotBe(baseline);

        c.TotalAmount -= 1;
        items[0].Note = "改了";
        ContractRules.ComputeSnapshotHash(c, items).Should().NotBe(baseline);
    }

    [Fact]
    public void Hash_IsIndependentOfItemOrder()
    {
        var c = Standard(); var items = Items();
        ContractRules.ComputeSnapshotHash(c, items).Should().Be(ContractRules.ComputeSnapshotHash(c, items.AsEnumerable().Reverse()));
    }

    [Fact]
    public void Sign_RequiresPendingApprovalAllStepsAndMatchingHash()
    {
        var c = Standard(); var hash = ContractRules.ComputeSnapshotHash(c, Items());
        c.Status = ContractStatus.PendingApproval; c.SnapshotHash = hash;

        ContractRules.ValidateSign(c, null, hash).Should().BeNull();
        ContractRules.ValidateSign(c, ContractApprovalStep.Executive, hash).Should().Contain("核決步驟");
        ContractRules.ValidateSign(c, null, "deadbeef").Should().Contain("重新送審");

        c.Status = ContractStatus.Reviewing;
        ContractRules.ValidateSign(c, null, hash).Should().Contain("待核決");
    }

    // ---------- 狀態轉移 ----------

    [Theory]
    [InlineData(ContractStatus.Quoting, ContractStatus.Reviewing, true)]
    [InlineData(ContractStatus.Reviewing, ContractStatus.Negotiating, true)]
    [InlineData(ContractStatus.Negotiating, ContractStatus.Quoting, false)]
    [InlineData(ContractStatus.PendingApproval, ContractStatus.Active, false)]
    [InlineData(ContractStatus.PendingApproval, ContractStatus.Cancelled, false)]
    [InlineData(ContractStatus.Quoting, ContractStatus.Active, false)]
    [InlineData(ContractStatus.Active, ContractStatus.Accepted, true)]
    [InlineData(ContractStatus.Accepted, ContractStatus.Warranty, true)]
    [InlineData(ContractStatus.Warranty, ContractStatus.Closed, true)]
    [InlineData(ContractStatus.Closed, ContractStatus.Active, false)]
    [InlineData(ContractStatus.Lost, ContractStatus.Quoting, false)]
    public void Transitions(ContractStatus from, ContractStatus to, bool allowed) =>
        ContractRules.CanTransition(from, to).Should().Be(allowed);

    [Fact]
    public void EditAndBindWindows()
    {
        ContractRules.CanEditTerms(ContractStatus.Negotiating).Should().BeTrue();
        ContractRules.CanEditTerms(ContractStatus.PendingApproval).Should().BeFalse();
        ContractRules.CanEditTerms(ContractStatus.Active).Should().BeFalse();
        ContractRules.CanBindProjects(ContractStatus.Active).Should().BeTrue();
        ContractRules.CanBindProjects(ContractStatus.Negotiating).Should().BeFalse();
    }

    // ---------- 合約編號 ----------

    [Fact]
    public void ContractNo_StartsAtOneAndIncrementsWithinYear()
    {
        ContractRules.NextContractNo(2026, []).Should().Be("CT-2026-001");
        ContractRules.NextContractNo(2026, ["CT-2026-001", "CT-2026-009", "CT-2025-050", "XX"]).Should().Be("CT-2026-010");
        ContractRules.NextContractNo(2026, ["CT-2026-999"]).Should().Be("CT-2026-1000");
        ContractRules.NextContractNo(2027, ["CT-2026-050"]).Should().Be("CT-2027-001");
    }

    // ---------- 成員授權 ----------

    [Fact]
    public void MemberRoles()
    {
        ContractRules.CanManage(Sales, Members).Should().BeTrue();
        ContractRules.CanManage(Pm, Members).Should().BeTrue();
        ContractRules.CanManage(Legal, Members).Should().BeFalse();
        ContractRules.CanReview(Legal, Members).Should().BeTrue();
        ContractRules.CanReview(Exec, Members).Should().BeFalse();
        ContractRules.IsMember(Guid.NewGuid(), Members).Should().BeFalse();
    }

    // ---------- 預設附件與驗證 ----------

    [Fact]
    public void DefaultSeeds_AreValid()
    {
        ContractRules.ValidateDeliverableItems(ContractRules.DefaultDeliverables).Should().BeNull();
        ContractRules.ValidateSlaLevels(ContractRules.DefaultSlaLevels).Should().BeNull();
        ContractRules.ReviewCatalog.Should().HaveCount(7);
        ContractRules.DefaultDeliverables.Should().HaveCount(7);
        ContractRules.DefaultSlaLevels.Select(l => l.Level).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void SlaLevels_RejectDuplicatesAndBadHours()
    {
        ContractRules.ValidateSlaLevels([new(1, "a", "d", 2, "t"), new(1, "b", "d", 2, "t")]).Should().Contain("重複");
        ContractRules.ValidateSlaLevels([new(1, "a", "d", 0, "t")]).Should().Contain("回應時間");
        ContractRules.ValidateSlaLevels([]).Should().Contain("1~5");
    }

    [Fact]
    public void DeliverableItems_DeliveryDate_Optional_ButMustBeSane()
    {
        var ok = new DeliverableSeed("設計", "SA/SD", "PDF", "簽認", true, new DateOnly(2026, 12, 1));
        var none = new DeliverableSeed("設計", "SA/SD", "PDF", "簽認", true);
        var bad = new DeliverableSeed("設計", "SA/SD", "PDF", "簽認", true, new DateOnly(1999, 1, 1));

        ContractRules.ValidateDeliverableItems([ok, none]).Should().BeNull();
        ContractRules.ValidateDeliverableItems([bad]).Should().Contain("指定交付日");
    }

    [Fact]
    public void DeliverableItems_RejectEmptyList() =>
        ContractRules.ValidateDeliverableItems([]).Should().Contain("至少");

    // ---------- 附件檔名 ----------

    [Fact]
    public void AttachmentName_FollowsConvention()
    {
        var name = ContractAttachmentService.GenerateName(new DateOnly(2026, 11, 14), "CT-2026-001", ContractAttachmentKind.Signed, "用印 正本_v1?", ".PDF");
        name.Should().Be("20261114_CT-2026-001_用印正本_用印-正本v1.pdf");
    }

    [Fact]
    public void AttachmentName_FallsBackAndDropsBadExtension()
    {
        ContractAttachmentService.GenerateName(new DateOnly(2026, 1, 2), "CT-2026-002", ContractAttachmentKind.Draft, "***", ".ex e").Should().Be("20260102_CT-2026-002_合約草稿_附件");
    }

    private static ContractApproval Approval(ContractApprovalStep step, Guid by) =>
        new() { Step = step, Decision = ApprovalDecision.Approved, DecidedBy = by };
}
