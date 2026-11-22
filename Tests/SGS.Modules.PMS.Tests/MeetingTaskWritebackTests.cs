using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Application.Services;
using SGSFramework.Core.ProjectContracts;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class MeetingTaskWritebackTests
{
    private static readonly DateOnly Meeting = new(2026, 11, 18);

    [Fact]
    public void A_task_runs_from_the_meeting_day_to_the_due_date()
    {
        MeetingTaskWriteback.Schedule(Meeting, new DateOnly(2026, 11, 30)).Should().Be((Meeting, new DateOnly(2026, 11, 30)));
    }

    [Fact]
    public void Without_a_due_date_the_task_gets_seven_days()
    {
        MeetingTaskWriteback.Schedule(Meeting, null).Should().Be((Meeting, Meeting.AddDays(7)));
    }

    [Fact]
    public void A_due_date_on_the_meeting_day_is_a_one_day_task()
    {
        MeetingTaskWriteback.Schedule(Meeting, Meeting).Should().Be((Meeting, Meeting));
    }

    [Fact]
    public void A_due_date_before_the_meeting_collapses_to_that_day_so_end_is_never_before_start()
    {
        var due = new DateOnly(2026, 11, 10);
        MeetingTaskWriteback.Schedule(Meeting, due).Should().Be((due, due));
    }

    [Fact]
    public void A_span_beyond_the_pms_limit_keeps_only_the_due_day()
    {
        var due = Meeting.AddDays(GanttRules.MaxSpanDays + 10);
        var (start, end) = MeetingTaskWriteback.Schedule(Meeting, due);

        (start, end).Should().Be((due, due));
        GanttRules.ValidateTask("x", start, end, false, 0).Should().BeNull("PMS 必須接受排出來的日期");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(400)]
    public void Scheduled_dates_always_pass_the_pms_task_validation(int dueOffsetDays)
    {
        var (start, end) = MeetingTaskWriteback.Schedule(Meeting, Meeting.AddDays(dueOffsetDays - 20));
        GanttRules.ValidateTask("任務", start, end, false, 0).Should().BeNull();
    }

    [Fact]
    public void The_marker_is_stable_and_unique_per_action_item()
    {
        var a = Guid.NewGuid();
        MeetingTaskWriteback.Marker(a).Should().Be($"[MTG:{a:N}]");
        MeetingTaskWriteback.Marker(a).Should().NotBe(MeetingTaskWriteback.Marker(Guid.NewGuid()));
    }

    private static MeetingTaskRequest Request(params MeetingTaskItem[] items) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "訂單系統週會", Meeting, Guid.NewGuid(), "tester", items);

    [Fact]
    public void The_description_carries_the_source_meeting_the_owner_and_the_idempotency_marker()
    {
        var item = new MeetingTaskItem(Guid.NewGuid(), "完成資料庫遷移腳本", "李工程師", null, "要先備份");

        var text = MeetingTaskWriteback.BuildDescription(Request(item), item);

        text.Should().Contain("完成資料庫遷移腳本").And.Contain("訂單系統週會").And.Contain("2026-11-18")
            .And.Contain("李工程師").And.Contain("要先備份").And.EndWith(MeetingTaskWriteback.Marker(item.ActionItemId));
    }

    [Fact]
    public void A_long_description_is_cut_to_the_pms_limit_but_never_loses_the_marker()
    {
        var item = new MeetingTaskItem(Guid.NewGuid(), new string('字', 2000), "某人", null, new string('註', 900));

        var text = MeetingTaskWriteback.BuildDescription(Request(item), item);

        text.Length.Should().BeLessThanOrEqualTo(1000, "PMS 任務說明上限 1000 字");
        text.Should().EndWith(MeetingTaskWriteback.Marker(item.ActionItemId));
    }

    [Fact]
    public void The_null_implementation_fails_every_item_with_a_clear_reason()
    {
        var items = new[] { new MeetingTaskItem(Guid.NewGuid(), "A", null, null, null), new MeetingTaskItem(Guid.NewGuid(), "B", null, null, null) };

        var results = new NullMeetingTaskWriteback().CreateTasksAsync(Request(items)).GetAwaiter().GetResult();

        results.Should().HaveCount(2).And.OnlyContain(r => !r.Succeeded && r.Error!.Contains("未安裝"));
        results.Select(r => r.ActionItemId).Should().Equal(items.Select(i => i.ActionItemId));
        new NullMeetingTaskWriteback().IsAvailable.Should().BeFalse();
    }
}
