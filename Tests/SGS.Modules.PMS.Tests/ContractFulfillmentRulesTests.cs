using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class ContractFulfillmentRulesTests
{
    // 2026-11-02 是星期一
    private static readonly DateOnly Mon = new(2026, 11, 2);
    private static readonly WorkCalendar Plain = new();

    private static DateOnly D(int month, int day) => new(2026, month, day);

    // ---------- 協力遲延 ----------

    [Fact]
    public void Delay_IsZeroBeforeOrOnDueDate()
    {
        ContractFulfillmentRules.DelayWorkdays(Mon, null, Mon, Plain).Should().Be(0);
        ContractFulfillmentRules.DelayWorkdays(Mon, null, Mon.AddDays(-3), Plain).Should().Be(0);
    }

    [Fact]
    public void Delay_CountsWorkdaysAfterDueDate_SkippingWeekend()
    {
        // 週五應完成，下週二還沒完成：週一、週二 = 2 個工作日
        var friday = D(11, 6);
        ContractFulfillmentRules.DelayWorkdays(friday, null, D(11, 10), Plain).Should().Be(2);
    }

    [Fact]
    public void Delay_StopsAtCompletedDate()
    {
        ContractFulfillmentRules.DelayWorkdays(Mon, Mon.AddDays(2), Mon.AddDays(30), Plain).Should().Be(2);
    }

    [Fact]
    public void Delay_SkipsHolidayAndCountsMakeupDay()
    {
        var calendar = new WorkCalendar([new HolidayEntry(D(11, 3), false), new HolidayEntry(D(11, 7), true)]);
        // 週一應完成：週二放假、週三、週四、週五、週六補班 → 到週六共 4 個工作日
        ContractFulfillmentRules.DelayWorkdays(Mon, null, D(11, 7), calendar).Should().Be(4);
    }

    [Fact]
    public void NoticeDeadline_IsThirdWorkdayAfterDue()
    {
        ContractFulfillmentRules.NoticeDeadline(Mon, Plain).Should().Be(D(11, 5));
        ContractFulfillmentRules.NoticeDeadline(D(11, 6), Plain).Should().Be(D(11, 11));   // 跨週末
    }

    [Fact]
    public void State_ProgressesFromOnTrackToNoticeOverdue()
    {
        var item = new ContractCoopItem { DueDate = Mon, Status = CoopStatus.Pending };

        ContractFulfillmentRules.State(item, false, Mon, Plain).Should().Be(CoopState.OnTrack);
        ContractFulfillmentRules.State(item, false, D(11, 4), Plain).Should().Be(CoopState.LateNoticeDue);
        ContractFulfillmentRules.State(item, false, D(11, 5), Plain).Should().Be(CoopState.LateNoticeDue);
        ContractFulfillmentRules.State(item, false, D(11, 6), Plain).Should().Be(CoopState.LateNoticeOverdue);
        ContractFulfillmentRules.State(item, true, D(11, 6), Plain).Should().Be(CoopState.LateNotified);

        item.Status = CoopStatus.Completed;
        ContractFulfillmentRules.State(item, false, D(11, 20), Plain).Should().Be(CoopState.Done);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(2, 0, true)]
    [InlineData(2, 2, false)]
    [InlineData(5, 2, true)]
    public void NeedsNotice_OnlyWhenDelayGrewBeyondLastNotice(int delay, int lastExtension, bool expected) =>
        ContractFulfillmentRules.NeedsNotice(new ContractCoopItem { Status = CoopStatus.Pending }, delay, lastExtension).Should().Be(expected);

    [Fact]
    public void NeedsNotice_IsFalseWhenCompleted() =>
        ContractFulfillmentRules.NeedsNotice(new ContractCoopItem { Status = CoopStatus.Completed }, 4, 0).Should().BeFalse();

    [Theory]
    [InlineData(1, 11, 3)]
    [InlineData(3, 11, 5)]
    [InlineData(5, 11, 9)]
    public void NoticeDeadline_UsesConfiguredWorkdays(int days, int month, int expectedDay) =>
        ContractFulfillmentRules.NoticeDeadline(Mon, Plain, days).Should().Be(new DateOnly(2026, month, expectedDay));

    [Fact]
    public void NoticeDeadline_ClampsOutOfRangeValues()
    {
        ContractFulfillmentRules.NoticeDeadline(Mon, Plain, 0).Should().Be(ContractFulfillmentRules.NoticeDeadline(Mon, Plain, 1));
        ContractFulfillmentRules.NoticeDeadline(Mon, Plain, 999).Should().Be(ContractFulfillmentRules.NoticeDeadline(Mon, Plain, 30));
    }

    [Theory]
    [InlineData(null, 3, 3)]
    [InlineData(5, 3, 5)]
    [InlineData(0, 3, 1)]
    [InlineData(99, 3, 30)]
    public void EffectiveNoticeWorkdays_ItemOverridesContract(int? item, int contract, int expected) =>
        ContractFulfillmentRules.EffectiveNoticeWorkdays(item, contract).Should().Be(expected);

    [Fact]
    public void State_RespectsCustomNoticeWorkdays()
    {
        var item = new ContractCoopItem { DueDate = Mon, Status = CoopStatus.Pending };

        // 通知期限 1 個工作日：週二就已超時；5 個工作日：週四仍在期限內
        ContractFulfillmentRules.State(item, false, D(11, 4), Plain, 1).Should().Be(CoopState.LateNoticeOverdue);
        ContractFulfillmentRules.State(item, false, D(11, 5), Plain, 5).Should().Be(CoopState.LateNoticeDue);
    }

    [Fact]
    public void CoopItem_ValidatesOptionalNoticeWorkdays()
    {
        ContractFulfillmentRules.ValidateCoopItem("x", null, Mon, null).Should().BeNull();
        ContractFulfillmentRules.ValidateCoopItem("x", null, Mon, 5).Should().BeNull();
        ContractFulfillmentRules.ValidateCoopItem("x", null, Mon, 0).Should().Contain("通知期限");
        ContractFulfillmentRules.ValidateCoopItem("x", null, Mon, 31).Should().Contain("通知期限");
    }

    [Fact]
    public void NoticeText_Validation()
    {
        ContractFulfillmentRules.ValidateNoticeText("主旨", "內文").Should().BeNull();
        ContractFulfillmentRules.ValidateNoticeText(" ", "內文").Should().Contain("主旨");
        ContractFulfillmentRules.ValidateNoticeText("a\nb", "內文").Should().Contain("換行");
        ContractFulfillmentRules.ValidateNoticeText(new string('a', 201), "內文").Should().Contain("200");
        ContractFulfillmentRules.ValidateNoticeText("主旨", " ").Should().Contain("內文");
        ContractFulfillmentRules.ValidateNoticeText("主旨", new string('a', 3001)).Should().Contain("3000");
    }

    [Fact]
    public void NoticeSignoff_Validation()
    {
        ContractFulfillmentRules.ValidateNoticeSignoff(null, null).Should().BeNull();
        ContractFulfillmentRules.ValidateNoticeSignoff("敬祝 安康", "XX 公司　專案經理　王小明　敬上").Should().BeNull();
        ContractFulfillmentRules.ValidateNoticeSignoff(new string('a', 101), null).Should().Contain("結語");
        ContractFulfillmentRules.ValidateNoticeSignoff("a\nb", null).Should().Contain("換行");
        ContractFulfillmentRules.ValidateNoticeSignoff(null, new string('a', 201)).Should().Contain("署名");
        ContractFulfillmentRules.ValidateNoticeSignoff(null, "a\nb").Should().Contain("換行");
    }

    // ---------- 展延與現行交期 ----------

    [Fact]
    public void TotalExtension_TakesLatestNoticePerItem()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        ContractFulfillmentRules.TotalExtensionWorkdays([(a, 2), (a, 5), (b, 3)]).Should().Be(8);
        ContractFulfillmentRules.TotalExtensionWorkdays([]).Should().Be(0);
    }

    [Fact]
    public void EffectiveDelivery_AddsExtensionInWorkdays()
    {
        var friday = D(11, 6);
        ContractFulfillmentRules.EffectiveDeliveryDate(friday, [], 0, Plain).Should().Be(friday);
        ContractFulfillmentRules.EffectiveDeliveryDate(friday, [], 2, Plain).Should().Be(D(11, 10));
        ContractFulfillmentRules.EffectiveDeliveryDate(null, [], 5, Plain).Should().BeNull();
    }

    [Fact]
    public void EffectiveDelivery_UsesLatestAmendmentDateAsBase()
    {
        var older = new ContractAmendment { NewDeliveryDate = D(12, 1), EffectiveDate = D(11, 1), CreatedAtUtc = DateTimeOffset.Parse("2026-11-01T00:00:00Z") };
        var newer = new ContractAmendment { NewDeliveryDate = D(12, 15), EffectiveDate = D(11, 10), CreatedAtUtc = DateTimeOffset.Parse("2026-11-10T00:00:00Z") };
        var noDate = new ContractAmendment { AmountDelta = 100, EffectiveDate = D(11, 20), CreatedAtUtc = DateTimeOffset.Parse("2026-11-20T00:00:00Z") };

        ContractFulfillmentRules.AmendedDeliveryDate(D(11, 30), [older, newer, noDate]).Should().Be(D(12, 15));
        ContractFulfillmentRules.AmendedDeliveryDate(D(11, 30), [noDate]).Should().Be(D(11, 30));
    }

    [Fact]
    public void CurrentAmountAndWarranty_AddAmendments()
    {
        ContractAmendment[] list =
        [
            new() { AmountDelta = 50_000, WarrantyMonthsDelta = 3 },
            new() { AmountDelta = -10_000, WarrantyMonthsDelta = 0 }
        ];

        ContractFulfillmentRules.CurrentAmount(1_000_000, list).Should().Be(1_040_000);
        ContractFulfillmentRules.CurrentWarrantyMonths(12, list).Should().Be(15);
        ContractFulfillmentRules.CurrentWarrantyMonths(2, [new ContractAmendment { WarrantyMonthsDelta = -10 }]).Should().Be(0);
    }

    // ---------- 審查回合 ----------

    [Fact]
    public void ReviewResponseDue_IsSevenWorkdaysAfterSubmit_OrNullWhenNoDeadline()
    {
        ContractFulfillmentRules.ReviewResponseDue(Mon, 7, Plain).Should().Be(D(11, 11));
        ContractFulfillmentRules.ReviewResponseDue(Mon, null, Plain).Should().BeNull();
    }

    [Fact]
    public void NewRound_RulesByPreviousOutcome()
    {
        ContractFulfillmentRules.ValidateNewRound([], true).Should().BeNull();
        ContractFulfillmentRules.ValidateNewRound([], false).Should().Contain("不需甲方書面簽認");
        ContractFulfillmentRules.ValidateNewRound([new() { RoundNo = 1, Outcome = ReviewOutcome.Pending }], true).Should().Contain("等待甲方回覆");
        ContractFulfillmentRules.ValidateNewRound([new() { RoundNo = 1, Outcome = ReviewOutcome.Approved }], true).Should().Contain("已通過");
        ContractFulfillmentRules.ValidateNewRound([new() { RoundNo = 1, Outcome = ReviewOutcome.DeemedApproved }], true).Should().Contain("已通過");
        ContractFulfillmentRules.ValidateNewRound([new() { RoundNo = 1, Outcome = ReviewOutcome.RevisionRequested }], true).Should().BeNull();
    }

    [Theory]
    [InlineData(1, 2, false)]
    [InlineData(3, 2, false)]
    [InlineData(4, 2, true)]
    [InlineData(10, null, false)]
    public void ExceedsRoundLimit(int roundNo, int? max, bool expected) =>
        ContractFulfillmentRules.ExceedsRoundLimit(roundNo, max).Should().Be(expected);

    [Fact]
    public void Respond_Validation()
    {
        var today = D(11, 20);
        ContractFulfillmentRules.ValidateRespond(ReviewOutcome.Pending, ReviewOutcome.Approved, D(11, 2), D(11, 5), today).Should().BeNull();
        ContractFulfillmentRules.ValidateRespond(ReviewOutcome.Pending, ReviewOutcome.RevisionRequested, D(11, 2), D(11, 5), today).Should().BeNull();
        ContractFulfillmentRules.ValidateRespond(ReviewOutcome.Approved, ReviewOutcome.Approved, D(11, 2), D(11, 5), today).Should().Contain("已有結果");
        ContractFulfillmentRules.ValidateRespond(ReviewOutcome.Pending, ReviewOutcome.DeemedApproved, D(11, 2), D(11, 5), today).Should().Contain("只能是");
        ContractFulfillmentRules.ValidateRespond(ReviewOutcome.Pending, ReviewOutcome.Approved, D(11, 5), D(11, 2), today).Should().Contain("不得早於送審日");
        ContractFulfillmentRules.ValidateRespond(ReviewOutcome.Pending, ReviewOutcome.Approved, D(11, 2), D(11, 25), today).Should().Contain("不得晚於今天");
    }

    [Fact]
    public void ShouldDeemApproved_OnlyAfterDueWhenContractAllows()
    {
        var round = new ContractReviewRound { Outcome = ReviewOutcome.Pending, ResponseDue = D(11, 10) };

        ContractFulfillmentRules.ShouldDeemApproved(round, true, D(11, 10)).Should().BeFalse();
        ContractFulfillmentRules.ShouldDeemApproved(round, true, D(11, 11)).Should().BeTrue();
        ContractFulfillmentRules.ShouldDeemApproved(round, false, D(11, 30)).Should().BeFalse();

        round.ResponseDue = null;
        ContractFulfillmentRules.ShouldDeemApproved(round, true, D(11, 30)).Should().BeFalse();

        round.ResponseDue = D(11, 10);
        round.Outcome = ReviewOutcome.Approved;
        ContractFulfillmentRules.ShouldDeemApproved(round, true, D(11, 30)).Should().BeFalse();
    }

    // ---------- 驗收 ----------

    [Fact]
    public void DeemedDue_Is14WorkdaysAfterDelivery_OrNull()
    {
        ContractFulfillmentRules.DeemedDue(Mon, 14, Plain).Should().Be(D(11, 20));
        ContractFulfillmentRules.DeemedDue(Mon, null, Plain).Should().BeNull();
    }

    [Fact]
    public void Deliver_Validation()
    {
        var today = D(11, 20);
        ContractFulfillmentRules.ValidateDeliver(ContractStatus.Active, AcceptanceStatus.NotDelivered, D(11, 10), today).Should().BeNull();
        ContractFulfillmentRules.ValidateDeliver(ContractStatus.Negotiating, AcceptanceStatus.NotDelivered, D(11, 10), today).Should().Contain("履約中");
        ContractFulfillmentRules.ValidateDeliver(ContractStatus.Accepted, AcceptanceStatus.Accepted, D(11, 10), today).Should().Contain("已驗收");
        ContractFulfillmentRules.ValidateDeliver(ContractStatus.Active, AcceptanceStatus.Delivered, D(11, 10), today).Should().Contain("已登錄交付");
        ContractFulfillmentRules.ValidateDeliver(ContractStatus.Active, AcceptanceStatus.NotDelivered, D(11, 25), today).Should().Contain("不得晚於今天");
        // 瑕疵處理後可重新交付
        ContractFulfillmentRules.ValidateDeliver(ContractStatus.Active, AcceptanceStatus.DefectsReported, D(11, 10), today).Should().BeNull();
    }

    [Fact]
    public void Defects_Validation()
    {
        var today = D(11, 20);
        ContractFulfillmentRules.ValidateDefects(AcceptanceStatus.Delivered, D(11, 25), D(11, 2), D(11, 6), today, "按鈕無法點擊").Should().BeNull();
        ContractFulfillmentRules.ValidateDefects(AcceptanceStatus.NotDelivered, null, D(11, 2), D(11, 6), today, "x").Should().Contain("已交付");
        ContractFulfillmentRules.ValidateDefects(AcceptanceStatus.Delivered, D(11, 25), D(11, 2), D(11, 6), today, " ").Should().Contain("具體瑕疵清單");
        ContractFulfillmentRules.ValidateDefects(AcceptanceStatus.Delivered, D(11, 10), D(11, 2), D(11, 12), today, "x").Should().Contain("視為已通過");
        ContractFulfillmentRules.ValidateDefects(AcceptanceStatus.Delivered, D(11, 25), D(11, 5), D(11, 2), today, "x").Should().Contain("不得早於交付日");
    }

    [Fact]
    public void Confirm_Validation()
    {
        var today = D(11, 20);
        ContractFulfillmentRules.ValidateConfirm(AcceptanceStatus.Delivered, D(11, 2), D(11, 10), today).Should().BeNull();
        ContractFulfillmentRules.ValidateConfirm(AcceptanceStatus.DefectsReported, D(11, 2), D(11, 10), today).Should().BeNull();
        ContractFulfillmentRules.ValidateConfirm(AcceptanceStatus.NotDelivered, null, D(11, 10), today).Should().Contain("先登錄交付");
        ContractFulfillmentRules.ValidateConfirm(AcceptanceStatus.Accepted, D(11, 2), D(11, 10), today).Should().Contain("已驗收完成");
        ContractFulfillmentRules.ValidateConfirm(AcceptanceStatus.Delivered, D(11, 5), D(11, 2), today).Should().Contain("不得早於交付日");
    }

    [Fact]
    public void ShouldDeemAccepted_OnlyDeliveredAndPastDue()
    {
        var a = new ContractAcceptance { Status = AcceptanceStatus.Delivered, DeemedDue = D(11, 20) };
        ContractFulfillmentRules.ShouldDeemAccepted(a, D(11, 20)).Should().BeFalse();
        ContractFulfillmentRules.ShouldDeemAccepted(a, D(11, 21)).Should().BeTrue();

        a.Status = AcceptanceStatus.DefectsReported;
        ContractFulfillmentRules.ShouldDeemAccepted(a, D(12, 31)).Should().BeFalse();

        a.Status = AcceptanceStatus.Delivered; a.DeemedDue = null;
        ContractFulfillmentRules.ShouldDeemAccepted(a, D(12, 31)).Should().BeFalse();
    }

    // ---------- 付款期程 ----------

    private static (string, decimal, PaymentTrigger) T(string name, decimal pct, PaymentTrigger trigger) => (name, pct, trigger);

    [Fact]
    public void Schedule_Valid_Sums100()
    {
        var check = ContractFulfillmentRules.ValidateSchedule(
            [T("簽約款", 30, PaymentTrigger.Signing), T("里程碑", 40, PaymentTrigger.Milestone), T("尾款", 30, PaymentTrigger.Acceptance)], false);
        check.Ok.Should().BeTrue();
    }

    [Fact]
    public void Schedule_Rejections()
    {
        ContractFulfillmentRules.ValidateSchedule([T("a", 60, PaymentTrigger.Signing)], false).Error.Should().Contain("合計須為 100%");
        ContractFulfillmentRules.ValidateSchedule([], false).Error.Should().Contain("至少");
        ContractFulfillmentRules.ValidateSchedule([T("a", 100, PaymentTrigger.Signing)], true).Error.Should().Contain("不可整組修改");
        ContractFulfillmentRules.ValidateSchedule([T(" ", 100, PaymentTrigger.Signing)], false).Error.Should().Contain("名稱");
        ContractFulfillmentRules.ValidateSchedule([T("a", 0, PaymentTrigger.Signing), T("b", 100, PaymentTrigger.Acceptance)], false).Error.Should().Contain("比例");
        ContractFulfillmentRules.ValidateSchedule([T("a", 50, PaymentTrigger.Acceptance), T("b", 50, PaymentTrigger.Acceptance)], false).Error.Should().Contain("只能有一期");
    }

    [Fact]
    public void AutoStatus_ReleasesSigningAndAcceptanceTriggers()
    {
        var signing = new ContractPaymentTerm { Trigger = PaymentTrigger.Signing };
        var final = new ContractPaymentTerm { Trigger = PaymentTrigger.Acceptance };
        var milestone = new ContractPaymentTerm { Trigger = PaymentTrigger.Milestone };

        ContractFulfillmentRules.AutoStatus(signing, ContractStatus.Negotiating, false).Should().Be(PaymentStatus.Pending);
        ContractFulfillmentRules.AutoStatus(signing, ContractStatus.Active, false).Should().Be(PaymentStatus.Invoiceable);
        ContractFulfillmentRules.AutoStatus(final, ContractStatus.Active, false).Should().Be(PaymentStatus.Pending);
        ContractFulfillmentRules.AutoStatus(final, ContractStatus.Accepted, true).Should().Be(PaymentStatus.Invoiceable);
        ContractFulfillmentRules.AutoStatus(milestone, ContractStatus.Accepted, true).Should().Be(PaymentStatus.Pending);

        final.Status = PaymentStatus.Paid;
        ContractFulfillmentRules.AutoStatus(final, ContractStatus.Active, false).Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void Move_FinalPaymentRequiresAcceptance()
    {
        var today = D(11, 20);
        var final = new ContractPaymentTerm { Trigger = PaymentTrigger.Acceptance, Status = PaymentStatus.Pending };

        ContractFulfillmentRules.ValidateMove(final, PaymentStatus.Invoiceable, ContractStatus.Active, false, null, null, today).Should().Contain("驗收完成後");
        ContractFulfillmentRules.ValidateMove(final, PaymentStatus.Invoiceable, ContractStatus.Accepted, true, null, null, today).Should().BeNull();

        final.Status = PaymentStatus.Invoiceable;
        ContractFulfillmentRules.ValidateMove(final, PaymentStatus.Invoiced, ContractStatus.Active, false, "AB-1", D(11, 10), today).Should().Contain("驗收完成後");
    }

    [Fact]
    public void Move_InvoiceAndPayRequireDetails()
    {
        var today = D(11, 20);
        var term = new ContractPaymentTerm { Trigger = PaymentTrigger.Signing, Status = PaymentStatus.Invoiceable };

        ContractFulfillmentRules.ValidateMove(term, PaymentStatus.Invoiced, ContractStatus.Active, false, " ", D(11, 10), today).Should().Contain("發票號碼");
        ContractFulfillmentRules.ValidateMove(term, PaymentStatus.Invoiced, ContractStatus.Active, false, "AB-1", null, today).Should().Contain("開立日期");
        ContractFulfillmentRules.ValidateMove(term, PaymentStatus.Invoiced, ContractStatus.Active, false, "AB-1", D(11, 25), today).Should().Contain("不得晚於今天");
        ContractFulfillmentRules.ValidateMove(term, PaymentStatus.Invoiced, ContractStatus.Active, false, "AB-1", D(11, 10), today).Should().BeNull();

        term.Status = PaymentStatus.Invoiced; term.InvoicedDate = D(11, 10);
        ContractFulfillmentRules.ValidateMove(term, PaymentStatus.Paid, ContractStatus.Active, false, null, D(11, 5), today).Should().Contain("不得早於開立日");
        ContractFulfillmentRules.ValidateMove(term, PaymentStatus.Paid, ContractStatus.Active, false, null, D(11, 12), today).Should().BeNull();
        ContractFulfillmentRules.ValidateMove(term, PaymentStatus.Pending, ContractStatus.Active, false, null, null, today).Should().Contain("不能從");
    }

    [Fact]
    public void Move_ManualReleaseOnlyForMilestoneAndWarrantyEnd()
    {
        var today = D(11, 20);
        var milestone = new ContractPaymentTerm { Trigger = PaymentTrigger.Milestone, Status = PaymentStatus.Pending };
        var signing = new ContractPaymentTerm { Trigger = PaymentTrigger.Signing, Status = PaymentStatus.Pending };

        ContractFulfillmentRules.ValidateMove(milestone, PaymentStatus.Invoiceable, ContractStatus.Active, false, null, null, today).Should().BeNull();
        ContractFulfillmentRules.ValidateMove(signing, PaymentStatus.Invoiceable, ContractStatus.Active, false, null, null, today).Should().Contain("自動放行");
        ContractFulfillmentRules.ValidateMove(milestone, PaymentStatus.Invoiceable, ContractStatus.Negotiating, false, null, null, today).Should().Contain("簽約後");
    }

    [Fact]
    public void Amounts_LastTermAbsorbsRounding()
    {
        var amounts = ContractFulfillmentRules.Amounts(1_000_001m, [33.33m, 33.33m, 33.34m]);

        amounts.Sum().Should().Be(1_000_001m);
        amounts[0].Should().Be(333_300m);
        amounts[^1].Should().Be(1_000_001m - amounts[0] - amounts[1]);
    }

    // ---------- 合約變更、協力事項、通知書驗證 ----------

    [Fact]
    public void Amendment_Validation()
    {
        ContractFulfillmentRules.ValidateAmendment("追加報表", 50_000, null, 0, 1_000_000, null).Should().BeNull();
        ContractFulfillmentRules.ValidateAmendment("延長保固", 0, null, 3, 1_000_000, null).Should().BeNull();
        ContractFulfillmentRules.ValidateAmendment(" ", 1, null, 0, 1_000_000, null).Should().Contain("標題");
        ContractFulfillmentRules.ValidateAmendment("x", 0, null, 0, 1_000_000, null).Should().Contain("至少填寫一項");
        ContractFulfillmentRules.ValidateAmendment("x", -2_000_000, null, 0, 1_000_000, null).Should().Contain("不得為負");
    }

    [Fact]
    public void CoopItem_Validation()
    {
        ContractFulfillmentRules.ValidateCoopItem("提供測試帳號", null, Mon).Should().BeNull();
        ContractFulfillmentRules.ValidateCoopItem(" ", null, Mon).Should().Contain("必填");
        ContractFulfillmentRules.ValidateCoopItem("x", new string('a', 1001), Mon).Should().Contain("1000");
    }

    [Fact]
    public void Notice_Validation()
    {
        var today = D(11, 20);
        ContractFulfillmentRules.ValidateNotice(3, 3, D(11, 27), today, null, null).Should().BeNull();
        ContractFulfillmentRules.ValidateNotice(0, 3, D(11, 27), today, null, null).Should().Contain("尚未逾期");
        ContractFulfillmentRules.ValidateNotice(3, 0, D(11, 27), today, null, null).Should().Contain("順延工作日");
        ContractFulfillmentRules.ValidateNotice(3, 10, D(11, 27), today, null, null).Should().Contain("不應超過");
        ContractFulfillmentRules.ValidateNotice(3, 3, D(11, 10), today, null, null).Should().Contain("不得早於今天");
    }
}
