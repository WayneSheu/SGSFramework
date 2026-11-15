using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public sealed class ContractPhase3RulesTests
{
    private static readonly TimeSpan Tw = TimeSpan.FromHours(8);
    private static readonly OvertimeTerms T = OvertimeTerms.Default;

    private static DateTimeOffset At(int month, int day, int hour, int minute = 0) => new(2026, month, day, hour, minute, 0, Tw);

    // 2026-11-16 週一；11-18 設為國定假日
    private static WorkCalendar Calendar() => new([new HolidayEntry(new DateOnly(2026, 11, 18), false)]);

    // ---------- 加班倍率 ----------

    [Theory]
    [InlineData(16, 10, 1.0)]   // 平日標準服務時間
    [InlineData(16, 19, 1.5)]   // 平日加班
    [InlineData(16, 23, 2.0)]   // 深夜
    [InlineData(17, 5, 2.0)]    // 凌晨
    [InlineData(21, 10, 2.0)]   // 週六例假日
    [InlineData(18, 10, 2.5)]   // 國定假日
    public void Multiplier_ByTime(int day, int hour, double expected) =>
        OvertimeRules.Multiplier(new DateTime(2026, 11, day, hour, 0, 0), Calendar(), T).Should().Be((decimal)expected);

    [Fact]
    public void Analyze_SplitsStandardAndOvertime()
    {
        var result = OvertimeRules.Analyze(At(11, 16, 17), At(11, 16, 20), Calendar(), T);

        result.TotalHours.Should().Be(3m);
        result.OvertimeHours.Should().Be(2m);
        result.WeightedHours.Should().Be(3m); // 2 小時 × 1.5
    }

    [Fact]
    public void Analyze_CrossesIntoNight()
    {
        var result = OvertimeRules.Analyze(At(11, 16, 21), At(11, 16, 23), Calendar(), T);

        result.OvertimeHours.Should().Be(2m);
        result.WeightedHours.Should().Be(1m * 1.5m + 1m * 2.0m);
    }

    [Fact]
    public void Analyze_IgnoresIncompleteSlice() =>
        OvertimeRules.Analyze(At(11, 16, 19), At(11, 16, 19, 10), Calendar(), T).TotalHours.Should().Be(0m);

    // ---------- 申請驗證 ----------

    private static readonly DateOnly Today = new(2026, 11, 16);

    [Fact]
    public void ValidateRequest_Valid() =>
        OvertimeRules.ValidateRequest("夜間上線", At(11, 28, 10), At(11, 28, 12), Today, new WorkCalendar(), null, T).Should().BeNull();

    [Fact]
    public void ValidateRequest_RequiresFiveWorkdaysNotice() =>
        OvertimeRules.ValidateRequest("夜間上線", At(11, 21, 10), At(11, 21, 12), Today, new WorkCalendar(), null, T).Should().Contain("提前 5 個工作日");

    [Fact]
    public void ValidateRequest_RejectsAllStandardHours() =>
        OvertimeRules.ValidateRequest("上班時間支援", At(11, 30, 10), At(11, 30, 12), Today, new WorkCalendar(), null, T).Should().Contain("標準服務時間");

    [Fact]
    public void ValidateRequest_RejectsBadInputs()
    {
        var calendar = new WorkCalendar();
        OvertimeRules.ValidateRequest(" ", At(11, 28, 10), At(11, 28, 12), Today, calendar, null, T).Should().Contain("事由");
        OvertimeRules.ValidateRequest("x", At(11, 28, 12), At(11, 28, 10), Today, calendar, null, T).Should().Contain("晚於");
        OvertimeRules.ValidateRequest("x", At(11, 28, 10, 7), At(11, 28, 12), Today, calendar, null, T).Should().Contain("15 分鐘");
        OvertimeRules.ValidateRequest("x", At(11, 28, 10), At(12, 8, 10), Today, calendar, null, T).Should().Contain("7 天");
    }

    [Fact]
    public void ValidateDecision_Rules()
    {
        var requester = Guid.NewGuid();
        var exec = Guid.NewGuid();

        OvertimeRules.ValidateDecision(SupportStatus.Pending, true, null, exec, requester, true).Should().BeNull();
        OvertimeRules.ValidateDecision(SupportStatus.Pending, true, null, requester, requester, true).Should().Contain("自己");
        OvertimeRules.ValidateDecision(SupportStatus.Pending, true, null, exec, requester, false).Should().Contain("核決主管");
        OvertimeRules.ValidateDecision(SupportStatus.Approved, true, null, exec, requester, true).Should().Contain("處理完畢");
        OvertimeRules.ValidateDecision(SupportStatus.Pending, false, "", exec, requester, true).Should().Contain("原因");
        OvertimeRules.ValidateDecision(SupportStatus.Pending, false, "預算不足", exec, requester, true).Should().BeNull();
    }

    [Fact]
    public void ValidateComplete_Rules()
    {
        var now = At(11, 29, 12);

        OvertimeRules.ValidateComplete(SupportStatus.Pending, At(11, 28, 19), At(11, 28, 21), now, null, T).Should().Contain("核准後");
        OvertimeRules.ValidateComplete(SupportStatus.Approved, At(11, 28, 19), At(11, 28, 21), now, null, T).Should().BeNull();
        OvertimeRules.ValidateComplete(SupportStatus.Approved, At(11, 28, 21), At(11, 28, 19), now, null, T).Should().Contain("晚於");
        OvertimeRules.ValidateComplete(SupportStatus.Approved, At(11, 29, 19), At(11, 29, 21), now, null, T).Should().Contain("不得晚於現在");
    }

    [Fact]
    public void ValidateCancel_OnlyOpenRequests()
    {
        OvertimeRules.ValidateCancel(SupportStatus.Pending).Should().BeNull();
        OvertimeRules.ValidateCancel(SupportStatus.Approved).Should().BeNull();
        OvertimeRules.ValidateCancel(SupportStatus.Completed).Should().NotBeNull();
        OvertimeRules.ValidateCancel(SupportStatus.Rejected).Should().NotBeNull();
    }

    // ---------- 保固 ----------

    [Theory]
    [InlineData(100, 0)]
    [InlineData(60, 1)]
    [InlineData(31, 1)]
    [InlineData(30, 2)]
    [InlineData(8, 2)]
    [InlineData(7, 3)]
    [InlineData(0, 3)]
    [InlineData(-1, 4)]
    public void WarrantyStage_ByDaysLeft(int daysLeft, int stage) => ContractWarrantyRules.StageFor(daysLeft).Should().Be(stage);

    [Fact]
    public void WarrantyReminder_OncePerStage()
    {
        ContractWarrantyRules.ShouldRemind(ContractWarrantyRules.Stage30, ContractWarrantyRules.Stage60).Should().BeTrue();
        ContractWarrantyRules.ShouldRemind(ContractWarrantyRules.Stage30, ContractWarrantyRules.Stage30).Should().BeFalse();
        ContractWarrantyRules.ShouldRemind(0, 0).Should().BeFalse();
    }

    [Fact]
    public void WarrantyEnd_AndDaysLeft()
    {
        var end = ContractWarrantyRules.WarrantyEnd(new DateOnly(2026, 1, 31), 12);

        end.Should().Be(new DateOnly(2027, 1, 31));
        ContractWarrantyRules.WarrantyEnd(null, 12).Should().BeNull();
        ContractWarrantyRules.WarrantyEnd(new DateOnly(2026, 1, 1), 0).Should().BeNull();
        ContractWarrantyRules.DaysLeft(end!.Value, new DateOnly(2027, 1, 24)).Should().Be(7);
    }

    // ---------- SLA 覆寫 ----------

    [Fact]
    public void SlaLevel_MapsPriority()
    {
        ContractSlaRules.LevelFor(TicketPriority.P1).Should().Be(1);
        ContractSlaRules.LevelFor(TicketPriority.P2).Should().Be(2);
        ContractSlaRules.LevelFor(TicketPriority.P3).Should().Be(3);
    }

    [Fact]
    public void SlaOverride_ClockHours()
    {
        var received = At(11, 16, 10);
        ContractSlaRules.OverrideDue(received, 4m, null, Calendar()).Should().Be(received.AddHours(4));
    }

    [Fact]
    public void SlaOverride_Workdays_SkipsHoliday()
    {
        // 週一受理，+2 個工作日；11-18 為假日 → 11-17、11-19
        var due = ContractSlaRules.OverrideDue(At(11, 16, 10), null, 2, Calendar());
        due.Should().NotBeNull();
        DateOnly.FromDateTime(due!.Value.ToOffset(Tw).DateTime).Should().Be(new DateOnly(2026, 11, 19));
    }

    [Fact]
    public void SlaOverride_NoneConfigured_ReturnsNull() =>
        ContractSlaRules.OverrideDue(At(11, 16, 10), null, null, Calendar()).Should().BeNull();

    [Fact]
    public void SlaValidateDue_Rules()
    {
        ContractSlaRules.ValidateDue(null, null).Should().BeNull();
        ContractSlaRules.ValidateDue(4m, null).Should().BeNull();
        ContractSlaRules.ValidateDue(null, 3).Should().BeNull();
        ContractSlaRules.ValidateDue(4m, 3).Should().Contain("其中一種");
        ContractSlaRules.ValidateDue(0m, null).Should().NotBeNull();
        ContractSlaRules.ValidateDue(721m, null).Should().NotBeNull();
        ContractSlaRules.ValidateDue(null, 0).Should().NotBeNull();
        ContractSlaRules.ValidateDue(null, 91).Should().NotBeNull();
    }

    // ---------- 產業合規 ----------

    [Fact]
    public void Compliance_CatalogCoversThreeIndustries()
    {
        ContractComplianceCatalog.For(ContractIndustry.Finance).Should().HaveCount(3);
        ContractComplianceCatalog.For(ContractIndustry.Medical).Should().HaveCount(4);
        ContractComplianceCatalog.For(ContractIndustry.Manufacturing).Should().HaveCount(4);
        ContractComplianceCatalog.For(ContractIndustry.General).Should().BeEmpty();
        ContractComplianceCatalog.All.Select(s => s.Code).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Compliance_SubmitRequiresAllHandled()
    {
        var items = new List<ContractComplianceItem>
        {
            new() { Code = "FIN_AUDIT", Status = ComplianceStatus.Included },
            new() { Code = "FIN_DR", Status = ComplianceStatus.NotApplicable },
            new() { Code = "FIN_INCIDENT", Status = ComplianceStatus.Pending }
        };

        var problems = ContractComplianceCatalog.ValidateForSubmit(ContractIndustry.Finance, items);

        problems.Should().HaveCount(2);
        problems.Should().Contain(p => p.Contains("不適用"));
        problems.Should().Contain(p => p.Contains("尚未確認"));
        ContractComplianceCatalog.ValidateForSubmit(ContractIndustry.General, []).Should().BeEmpty();
    }

    [Fact]
    public void Compliance_ValidateUpdate()
    {
        ContractComplianceCatalog.ValidateUpdate("FIN_DR", ContractIndustry.Finance, ComplianceStatus.Included, null).Should().BeNull();
        ContractComplianceCatalog.ValidateUpdate("FIN_DR", ContractIndustry.Medical, ComplianceStatus.Included, null).Should().NotBeNull();
        ContractComplianceCatalog.ValidateUpdate("NOPE", ContractIndustry.Finance, ComplianceStatus.Included, null).Should().NotBeNull();
        ContractComplianceCatalog.ValidateUpdate("FIN_DR", ContractIndustry.Finance, ComplianceStatus.NotApplicable, " ").Should().Contain("原因");
    }

    // ---------- 參數化（提前日數、窗口上限、倍率） ----------

    [Fact]
    public void Terms_CustomRates_ChangeWeightedHours()
    {
        var terms = new OvertimeTerms(5, 7, 1.34m, 1.67m, 2.0m); // 例：法規調整後的倍率

        var result = OvertimeRules.Analyze(At(11, 16, 21), At(11, 16, 23), Calendar(), terms);

        result.WeightedHours.Should().Be(1m * 1.34m + 1m * 1.67m);
        result.Segments.Should().Contain(s => s.Kind == OvertimeKind.Weekday && s.Multiplier == 1.34m);
    }

    [Fact]
    public void Terms_AdvanceWorkdays_Configurable()
    {
        var calendar = new WorkCalendar();
        var shortNotice = new OvertimeTerms(2, 7, 1.5m, 2.0m, 2.5m);

        // 週一申請，週六 11-21 支援：預設 5 日不足，改 2 日即可
        OvertimeRules.ValidateRequest("x", At(11, 21, 10), At(11, 21, 12), Today, calendar, null, T).Should().Contain("提前 5 個工作日");
        OvertimeRules.ValidateRequest("x", At(11, 21, 10), At(11, 21, 12), Today, calendar, null, shortNotice).Should().BeNull();
    }

    [Fact]
    public void Terms_MaxWindowDays_Configurable()
    {
        var calendar = new WorkCalendar();
        var longWindow = new OvertimeTerms(5, 10, 1.5m, 2.0m, 2.5m);

        OvertimeRules.ValidateRequest("x", At(11, 28, 10), At(12, 8, 10), Today, calendar, null, T).Should().Contain("7 天");
        OvertimeRules.ValidateRequest("x", At(11, 28, 10), At(12, 8, 10), Today, calendar, null, longWindow).Should().BeNull();
    }

    [Fact]
    public void Terms_Validate_Rules()
    {
        OvertimeTerms.Default.Validate().Should().BeNull();
        new OvertimeTerms(-1, 7, 1.5m, 2m, 2.5m).Validate().Should().Contain("工作日");
        new OvertimeTerms(5, 0, 1.5m, 2m, 2.5m).Validate().Should().Contain("窗口");
        new OvertimeTerms(5, 7, 0.9m, 2m, 2.5m).Validate().Should().Contain("倍率");
        new OvertimeTerms(5, 7, 1.5m, 2m, 5.5m).Validate().Should().Contain("倍率");
        new OvertimeTerms(5, 7, 1.555m, 2m, 2.5m).Validate().Should().Contain("兩位小數");
        new OvertimeTerms(5, 7, 2.5m, 2m, 2.0m).Validate().Should().Contain("≤");
    }

    // ---------- 甲方提出佐證 ----------

    private static readonly DateOnly SupportStart = new(2026, 11, 28);

    [Fact]
    public void ClientRequest_Valid() =>
        OvertimeRules.ValidateClientRequest(new DateOnly(2026, 11, 10), "Email", "RE: 週六上線支援", Today, SupportStart).Should().BeNull();

    [Fact]
    public void ClientRequest_Rejects()
    {
        OvertimeRules.ValidateClientRequest(null, "Email", null, Today, SupportStart).Should().Contain("日期");
        OvertimeRules.ValidateClientRequest(new DateOnly(2026, 11, 20), "Email", null, Today, SupportStart).Should().Contain("今天");
        OvertimeRules.ValidateClientRequest(new DateOnly(2026, 11, 16), " ", null, Today, SupportStart).Should().Contain("方式");
        OvertimeRules.ValidateClientRequest(new DateOnly(2024, 1, 1), "Email", null, Today, SupportStart).Should().Contain("365");
        OvertimeRules.ValidateClientRequest(new DateOnly(2026, 11, 16), "Email", new string('x', 501), Today, SupportStart).Should().Contain("佐證");
        OvertimeRules.ValidateClientRequest(new DateOnly(2026, 11, 16), "Email", null, Today, new DateOnly(2026, 11, 15)).Should().Contain("支援開始日");
    }

    [Fact]
    public void Terms_RequiresApproval_DefaultsTrue_AndAffectsEquality()
    {
        OvertimeTerms.Default.RequiresApproval.Should().BeTrue();
        (OvertimeTerms.Default with { RequiresApproval = false }).Should().NotBe(OvertimeTerms.Default);
    }

    // ---------- 首次回應期限（只計標準服務時間） ----------

    private static DateTimeOffset Due(DateTimeOffset received, decimal hours) => ContractSlaRules.ResponseDue(received, hours, Calendar());

    [Fact]
    public void ResponseDue_WithinServiceHours() => Due(At(11, 16, 10), 2m).Should().Be(At(11, 16, 12));

    [Fact]
    public void ResponseDue_CarriesOverToNextWorkday() => Due(At(11, 16, 17), 2m).Should().Be(At(11, 17, 10)); // 週一 17:00 → 當日 1 小時 + 次日 09:00 起 1 小時

    [Fact]
    public void ResponseDue_SkipsWeekend() => Due(At(11, 20, 17), 2m).Should().Be(At(11, 23, 10)); // 週五 17:00 → 下週一 10:00

    [Fact]
    public void ResponseDue_ReceivedOnWeekend_StartsNextWorkday() => Due(At(11, 21, 10), 1m).Should().Be(At(11, 23, 10));

    [Fact]
    public void ResponseDue_ReceivedBeforeServiceHours_StartsAtOpen() => Due(At(11, 16, 7), 1m).Should().Be(At(11, 16, 10));

    [Fact]
    public void ResponseDue_SkipsHoliday() => Due(At(11, 17, 17, 30), 1m).Should().Be(At(11, 19, 9, 30)); // 11-18 國定假日

    [Fact]
    public void ResponseDue_RoundsUpToQuarterHour() => Due(At(11, 16, 10), 0.1m).Should().Be(At(11, 16, 10, 15));

    [Fact]
    public void ResponseLate_Judgement()
    {
        ContractSlaRules.IsResponseLate(At(11, 16, 12), At(11, 16, 12)).Should().BeFalse();
        ContractSlaRules.IsResponseLate(At(11, 16, 12), At(11, 16, 12, 1)).Should().BeTrue();
        ContractSlaRules.IsResponseLate(null, At(11, 16, 12)).Should().BeFalse();
        ContractSlaRules.IsResponseLate(At(11, 16, 12), null).Should().BeFalse();
    }

    [Fact]
    public void Agreement_Validate()
    {
        ContractSlaRules.ValidateAgreement(new DateOnly(2026, 11, 10), "Email", "RE: SLA 調整", Today).Should().BeNull();
        ContractSlaRules.ValidateAgreement(null, "Email", null, Today).Should().Contain("日期");
        ContractSlaRules.ValidateAgreement(new DateOnly(2026, 11, 20), "Email", null, Today).Should().Contain("今天");
        ContractSlaRules.ValidateAgreement(new DateOnly(2024, 1, 1), "Email", null, Today).Should().Contain("365");
        ContractSlaRules.ValidateAgreement(new DateOnly(2026, 11, 10), " ", null, Today).Should().Contain("方式");
        ContractSlaRules.ValidateAgreement(new DateOnly(2026, 11, 10), "Email", new string('x', 501), Today).Should().Contain("佐證");
    }

    // ---------- 附件一交付項目：狀態、記錄與提醒 ----------

    private static DeliverableState State(DateOnly? due, DateOnly? done, DateOnly today) => ContractDeliverableRules.StateOf(due, done, today, Calendar());

    [Fact]
    public void Deliverable_State_Rules()
    {
        var today = new DateOnly(2026, 11, 16); // 週一；11-18 假日

        State(null, null, today).Should().Be(DeliverableState.NoDate);
        State(null, new DateOnly(2026, 11, 10), today).Should().Be(DeliverableState.Delivered);
        State(new DateOnly(2026, 12, 31), null, today).Should().Be(DeliverableState.OnTrack);
        State(new DateOnly(2026, 11, 20), null, today).Should().Be(DeliverableState.DueSoon);   // 11-17、19、20 → 3 個工作日
        State(new DateOnly(2026, 11, 16), null, today).Should().Be(DeliverableState.DueSoon);   // 當日到期
        State(new DateOnly(2026, 11, 15), null, today).Should().Be(DeliverableState.Overdue);
        State(new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 15), today).Should().Be(DeliverableState.Delivered);
        State(new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 16), today).Should().Be(DeliverableState.DeliveredLate);
    }

    [Fact]
    public void Deliverable_ValidateDelivered()
    {
        var today = new DateOnly(2026, 11, 16);
        var start = new DateOnly(2026, 11, 1);

        ContractDeliverableRules.ValidateDelivered(null, null, today, start).Should().BeNull(); // 取消記錄不需交付方式
        ContractDeliverableRules.ValidateDelivered(new DateOnly(2026, 11, 10), "備註", today, start, "Email").Should().BeNull();
        ContractDeliverableRules.ValidateDelivered(new DateOnly(2026, 11, 17), null, today, start, "Email").Should().Contain("今天");
        ContractDeliverableRules.ValidateDelivered(new DateOnly(2026, 10, 30), null, today, start, "Email").Should().Contain("合約起日");
        ContractDeliverableRules.ValidateDelivered(new DateOnly(2026, 11, 10), new string('x', 201), today, start, "Email").Should().Contain("200");
        ContractDeliverableRules.ValidateDelivered(new DateOnly(2026, 11, 10), null, today, start).Should().Contain("交付方式");
        ContractDeliverableRules.ValidateDelivered(new DateOnly(2026, 11, 10), null, today, start, " ").Should().Contain("交付方式");
        ContractDeliverableRules.ValidateDelivered(new DateOnly(2026, 11, 10), null, today, start, new string('x', 51)).Should().Contain("50");
    }

    [Fact]
    public void Deliverable_ValidateDeliveryDate()
    {
        var start = new DateOnly(2026, 11, 1);

        ContractDeliverableRules.ValidateDeliveryDate("SRS", null, start).Should().BeNull();
        ContractDeliverableRules.ValidateDeliveryDate("SRS", new DateOnly(2026, 12, 1), start).Should().BeNull();
        ContractDeliverableRules.ValidateDeliveryDate("SRS", new DateOnly(2026, 10, 1), start).Should().Contain("合約起日");
        ContractDeliverableRules.ValidateDeliveryDate("SRS", new DateOnly(1999, 1, 1), null).Should().Contain("不正確");
    }

    [Fact]
    public void Deliverable_ReminderMessage_ShowsWorkdays()
    {
        var today = new DateOnly(2026, 11, 20);

        ContractDeliverableRules.ReminderMessage("SRS", new DateOnly(2026, 11, 16), DeliverableState.Overdue, today, Calendar()).Should().Contain("已逾指定交付日");
        ContractDeliverableRules.ReminderMessage("SRS", new DateOnly(2026, 11, 24), DeliverableState.DueSoon, today, Calendar()).Should().Contain("將於 2026-11-24 到期");
    }

    // ---------- 附件一甲方簽認進度與代登錄 ----------

    private static ContractReviewRound Round(ReviewOutcome outcome, DateOnly? due) => new() { RoundNo = 1, Outcome = outcome, ResponseDue = due };

    [Fact]
    public void Signoff_State_Rules()
    {
        var today = new DateOnly(2026, 11, 16);

        ContractSignoffRules.StateOf(false, null, today).Should().Be(SignoffState.NotRequired);
        ContractSignoffRules.StateOf(false, Round(ReviewOutcome.Approved, null), today).Should().Be(SignoffState.NotRequired);
        ContractSignoffRules.StateOf(true, null, today).Should().Be(SignoffState.NotSubmitted);
        ContractSignoffRules.StateOf(true, Round(ReviewOutcome.Pending, new DateOnly(2026, 11, 20)), today).Should().Be(SignoffState.InReview);
        ContractSignoffRules.StateOf(true, Round(ReviewOutcome.Pending, new DateOnly(2026, 11, 16)), today).Should().Be(SignoffState.InReview); // 期限當日仍可回覆
        ContractSignoffRules.StateOf(true, Round(ReviewOutcome.Pending, new DateOnly(2026, 11, 15)), today).Should().Be(SignoffState.Overdue);
        ContractSignoffRules.StateOf(true, Round(ReviewOutcome.Pending, null), today).Should().Be(SignoffState.InReview);
        ContractSignoffRules.StateOf(true, Round(ReviewOutcome.RevisionRequested, null), today).Should().Be(SignoffState.RevisionRequested);
        ContractSignoffRules.StateOf(true, Round(ReviewOutcome.Approved, null), today).Should().Be(SignoffState.Approved);
        ContractSignoffRules.StateOf(true, Round(ReviewOutcome.DeemedApproved, null), today).Should().Be(SignoffState.DeemedApproved);
    }

    [Fact]
    public void Signoff_ValidateChannel()
    {
        ContractSignoffRules.ValidateChannel("Email").Should().BeNull();
        ContractSignoffRules.ValidateChannel(null).Should().Contain("簽認");
        ContractSignoffRules.ValidateChannel("  ").Should().Contain("簽認");
        ContractSignoffRules.ValidateChannel(new string('x', 51)).Should().Contain("50");
    }

    [Fact]
    public void Signoff_DeliveredBeforeSignoff_OnlyForUnfinished()
    {
        ContractSignoffRules.DeliveredBeforeSignoff(SignoffState.NotSubmitted).Should().BeTrue();
        ContractSignoffRules.DeliveredBeforeSignoff(SignoffState.InReview).Should().BeTrue();
        ContractSignoffRules.DeliveredBeforeSignoff(SignoffState.Overdue).Should().BeTrue();
        ContractSignoffRules.DeliveredBeforeSignoff(SignoffState.RevisionRequested).Should().BeTrue();
        ContractSignoffRules.DeliveredBeforeSignoff(SignoffState.Approved).Should().BeFalse();
        ContractSignoffRules.DeliveredBeforeSignoff(SignoffState.DeemedApproved).Should().BeFalse();
        ContractSignoffRules.DeliveredBeforeSignoff(SignoffState.NotRequired).Should().BeFalse();
        ContractSignoffRules.Describe(SignoffState.NotSubmitted).Should().Be("待送審");
    }
}
