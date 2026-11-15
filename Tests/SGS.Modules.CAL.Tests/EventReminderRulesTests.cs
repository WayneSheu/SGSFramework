using FluentAssertions;
using SGS.Modules.CAL.Application.Rules;
using Xunit;

namespace SGS.Modules.CAL.Tests;

public sealed class EventReminderRulesTests
{
    private static readonly TimeSpan Tw = TimeSpan.FromHours(8);

    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, Tw);

    [Fact]
    public void ValidateReminders_Rules()
    {
        EventReminderRules.ValidateReminders(null).Should().BeNull();
        EventReminderRules.ValidateReminders([]).Should().BeNull();
        EventReminderRules.ValidateReminders([15, 60, 1440]).Should().BeNull();
        EventReminderRules.ValidateReminders([1, 20160]).Should().BeNull();
        EventReminderRules.ValidateReminders([0]).Should().Contain("1 分鐘");
        EventReminderRules.ValidateReminders([20161]).Should().Contain("14 天");
        EventReminderRules.ValidateReminders([15, 15]).Should().Contain("重複");
        EventReminderRules.ValidateReminders([1, 2, 3, 4, 5, 6]).Should().Contain("5 組");
    }

    [Fact]
    public void ValidateAttendees_Rules()
    {
        EventReminderRules.ValidateAttendees(null).Should().BeNull();
        EventReminderRules.ValidateAttendees([Guid.NewGuid()]).Should().BeNull();
        EventReminderRules.ValidateAttendees([Guid.Empty]).Should().Contain("不正確");
        EventReminderRules.ValidateAttendees(Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToList()).Should().Contain("50");
    }

    [Fact]
    public void ShouldSend_OnlyWhenDue_NotSent_AndBeforeStart()
    {
        var start = At(20, 10);

        EventReminderRules.ShouldSend(start, 30, null, At(20, 9, 29)).Should().BeFalse();   // 還沒到提醒時間
        EventReminderRules.ShouldSend(start, 30, null, At(20, 9, 30)).Should().BeTrue();    // 剛好到
        EventReminderRules.ShouldSend(start, 30, null, At(20, 9, 59)).Should().BeTrue();    // 過了但活動未開始（排程延遲仍會補寄）
        EventReminderRules.ShouldSend(start, 30, null, At(20, 10)).Should().BeFalse();      // 活動已開始，不再提醒
        EventReminderRules.ShouldSend(start, 30, At(20, 9, 31), At(20, 9, 40)).Should().BeFalse(); // 已寄過
    }

    [Fact]
    public void DueAt_IsStartMinusMinutes() => EventReminderRules.DueAt(At(20, 10), 1440).Should().Be(At(19, 10));

    [Fact]
    public void Reschedule_ResetsOnlyWhenStartChanges()
    {
        EventReminderRules.ShouldResetOnReschedule(At(20, 10), At(20, 10)).Should().BeFalse();
        EventReminderRules.ShouldResetOnReschedule(At(20, 10), At(21, 10)).Should().BeTrue();
        // 同一時刻以不同時區表示不算改期
        EventReminderRules.ShouldResetOnReschedule(At(20, 10), At(20, 10).ToOffset(TimeSpan.Zero)).Should().BeFalse();
    }

    [Theory]
    [InlineData(15, "15 分鐘前")]
    [InlineData(60, "1 小時前")]
    [InlineData(90, "90 分鐘前")]
    [InlineData(120, "2 小時前")]
    [InlineData(1440, "1 天前")]
    [InlineData(2880, "2 天前")]
    public void Describe_UsesLargestWholeUnit(int minutes, string expected) => EventReminderRules.Describe(minutes).Should().Be(expected);
}
