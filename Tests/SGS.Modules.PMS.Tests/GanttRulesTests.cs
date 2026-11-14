using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class GanttRulesTests
{
    private static DateOnly D(int day) => new(2026, 11, day);

    private static GanttTaskNode Node(Guid id, Guid? parent, int order, string name, int start, int end, int percent = 0, bool milestone = false) =>
        new(id, parent, order, name, D(start), D(end), milestone, percent);

    // ---------- 驗證 ----------

    [Theory]
    [InlineData("需求訪談", 2, 5, false, 0, true)]
    [InlineData("  ", 2, 5, false, 0, false)]
    [InlineData(null, 2, 5, false, 0, false)]
    [InlineData("x", 5, 2, false, 0, false)]            // 結束早於開始
    [InlineData("x", 5, 5, true, 0, true)]               // 里程碑同一天
    [InlineData("x", 5, 6, true, 0, false)]              // 里程碑跨天
    [InlineData("x", 2, 5, false, 101, false)]
    [InlineData("x", 2, 5, false, -1, false)]
    public void ValidateTask(string? name, int start, int end, bool milestone, int percent, bool ok) =>
        (GanttRules.ValidateTask(name, D(start), D(end), milestone, percent) is null).Should().Be(ok);

    [Fact]
    public void ValidateTask_RejectsUnsetDatesAndHugeSpan()
    {
        GanttRules.ValidateTask("x", default, D(2), false, 0).Should().NotBeNull();
        GanttRules.ValidateTask("x", D(1), D(1).AddDays(GanttRules.MaxSpanDays), false, 0).Should().NotBeNull();
        GanttRules.ValidateTask(new string('字', 201), D(1), D(2), false, 0).Should().NotBeNull();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-365, true)]
    [InlineData(365, true)]
    [InlineData(366, false)]
    [InlineData(-366, false)]
    public void ValidateLag(int lag, bool ok) => (GanttRules.ValidateLag(lag) is null).Should().Be(ok);

    // ---------- 排版與彙總 ----------

    [Fact]
    public void Layout_AssignsWbs_PreorderAndLevels()
    {
        Guid a = Guid.NewGuid(), a1 = Guid.NewGuid(), a2 = Guid.NewGuid(), b = Guid.NewGuid(), a11 = Guid.NewGuid();
        var rows = GanttRules.Layout(
        [
            Node(b, null, 20, "B", 10, 12),
            Node(a, null, 10, "A", 1, 9),
            Node(a2, a, 20, "A2", 5, 9),
            Node(a1, a, 10, "A1", 1, 4),
            Node(a11, a1, 10, "A1-1", 1, 2)
        ]);

        rows.Select(r => (r.Id, r.Wbs, r.Level)).Should().Equal(
            (a, "1", 0), (a1, "1.1", 1), (a11, "1.1.1", 2), (a2, "1.2", 1), (b, "2", 0));
    }

    [Fact]
    public void Layout_SummaryTask_UsesDescendantExtentAndWeightedProgress()
    {
        Guid parent = Guid.NewGuid(), c1 = Guid.NewGuid(), c2 = Guid.NewGuid();
        var rows = GanttRules.Layout(
        [
            Node(parent, null, 1, "階段", 20, 21, percent: 99),    // 自身欄位應被忽略
            Node(c1, parent, 1, "短任務", 1, 2, percent: 100),       // 2 天、100%
            Node(c2, parent, 2, "長任務", 3, 8, percent: 0)          // 6 天、0%
        ]).ToDictionary(r => r.Id);

        var summary = rows[parent];
        summary.HasChildren.Should().BeTrue();
        summary.Start.Should().Be(D(1));
        summary.End.Should().Be(D(8));
        summary.Percent.Should().Be(25);   // (100*2 + 0*6) / 8
        rows[c1].HasChildren.Should().BeFalse();
    }

    [Fact]
    public void Layout_NestedSummaryRollsUpThroughLevels()
    {
        Guid root = Guid.NewGuid(), mid = Guid.NewGuid(), leaf1 = Guid.NewGuid(), leaf2 = Guid.NewGuid();
        var rows = GanttRules.Layout(
        [
            Node(root, null, 1, "R", 1, 1),
            Node(mid, root, 1, "M", 1, 1),
            Node(leaf1, mid, 1, "L1", 3, 4, percent: 50),
            Node(leaf2, root, 2, "L2", 10, 12, percent: 100)
        ]).ToDictionary(r => r.Id);

        rows[mid].Start.Should().Be(D(3));
        rows[mid].End.Should().Be(D(4));
        rows[root].Start.Should().Be(D(3));
        rows[root].End.Should().Be(D(12));
    }

    [Fact]
    public void Layout_OrphanAndCyclicParentsAreNotLost()
    {
        Guid orphan = Guid.NewGuid(), x = Guid.NewGuid(), y = Guid.NewGuid();
        var rows = GanttRules.Layout(
        [
            Node(orphan, Guid.NewGuid(), 1, "孤兒（上層不存在）", 1, 2),
            Node(x, y, 2, "X", 3, 4),
            Node(y, x, 3, "Y", 5, 6)   // X、Y 互為上層（資料異常）
        ]);

        rows.Select(r => r.Id).Should().BeEquivalentTo([orphan, x, y]);
        rows.Should().HaveCount(3);
    }

    [Fact]
    public void Layout_Empty_ReturnsEmpty() => GanttRules.Layout([]).Should().BeEmpty();

    // ---------- 階層 ----------

    [Fact]
    public void LevelOf_CountsAncestors()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?> { [a] = null, [b] = a, [c] = b };
        GanttRules.LevelOf(null, parents).Should().Be(0);
        GanttRules.LevelOf(a, parents).Should().Be(1);
        GanttRules.LevelOf(c, parents).Should().Be(3);
    }

    [Fact]
    public void LevelOf_CyclicChain_ExceedsMaxDepth()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?> { [a] = b, [b] = a };
        GanttRules.LevelOf(a, parents).Should().BeGreaterThan(GanttRules.MaxDepth);
    }

    [Fact]
    public void IsSelfOrDescendant_BlocksMovingUnderSelf()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid(), other = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?> { [a] = null, [b] = a, [c] = b, [other] = null };
        GanttRules.IsSelfOrDescendant(a, a, parents).Should().BeTrue();
        GanttRules.IsSelfOrDescendant(a, c, parents).Should().BeTrue();
        GanttRules.IsSelfOrDescendant(c, a, parents).Should().BeFalse();
        GanttRules.IsSelfOrDescendant(a, other, parents).Should().BeFalse();
    }

    [Fact]
    public void Reposition_InsertsBeforeOrAppends()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        GanttRules.Reposition([a, b, c], c, a).Should().Equal(c, a, b);
        GanttRules.Reposition([a, b, c], a, null).Should().Equal(b, c, a);
        GanttRules.Reposition([a, b, c], b, c).Should().Equal(a, b, c);
        GanttRules.Reposition([a, b], c, b).Should().Equal(a, c, b);          // 新加入的同層任務
        GanttRules.Reposition([a, b], c, Guid.NewGuid()).Should().Equal(a, b, c); // 找不到「之前」就放最後
    }

    // ---------- 相依 ----------

    [Fact]
    public void Cycle_Detection()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        GanttDependency[] deps = [new(Guid.NewGuid(), a, b, 0), new(Guid.NewGuid(), b, c, 0)];

        GanttRules.WouldCreateCycle(deps, a, a).Should().BeTrue();        // 自我相依
        GanttRules.WouldCreateCycle(deps, c, a).Should().BeTrue();        // a→b→c→a
        GanttRules.WouldCreateCycle(deps, b, a).Should().BeTrue();        // a→b→a
        GanttRules.WouldCreateCycle(deps, a, c).Should().BeFalse();       // 捷徑不構成循環
        GanttRules.WouldCreateCycle(deps, Guid.NewGuid(), a).Should().BeFalse();
    }

    [Theory]
    [InlineData(5, 0, 6)]
    [InlineData(5, 2, 8)]
    [InlineData(5, -1, 5)]
    public void EarliestStart_IsNextDayPlusLag(int end, int lag, int expected) =>
        GanttRules.EarliestStart(D(end), lag).Should().Be(D(expected));

    [Fact]
    public void Conflicts_ReportMaxViolationPerSuccessor()
    {
        Guid p1 = Guid.NewGuid(), p2 = Guid.NewGuid(), s = Guid.NewGuid(), ok = Guid.NewGuid();
        var rows = new[]
        {
            new GanttRow(p1, null, "1", 0, false, D(1), D(10), 0),
            new GanttRow(p2, null, "2", 0, false, D(1), D(12), 0),
            new GanttRow(s, null, "3", 0, false, D(8), D(15), 0),     // 應 ≥ 13（p2 結束 12 +1）→ 衝突 5 天
            new GanttRow(ok, null, "4", 0, false, D(11), D(14), 0)    // p1 結束 10 → 最早 11 → 無衝突
        }.ToDictionary(r => r.Id);

        var conflicts = GanttRules.Conflicts(rows, [
            new GanttDependency(Guid.NewGuid(), p1, s, 0),
            new GanttDependency(Guid.NewGuid(), p2, s, 0),
            new GanttDependency(Guid.NewGuid(), p1, ok, 0)]);

        conflicts.Should().ContainSingle().Which.Should().Be(new KeyValuePair<Guid, int>(s, 5));
    }

    // ---------- 連動順延 ----------

    [Fact]
    public void Cascade_PushesChainForwardKeepingDurations()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        // A 被拉長到 11/10；B 原 11/5~11/8、C 原 11/9~11/12，FS 相依 A→B→C
        var leaves = new Dictionary<Guid, (DateOnly, DateOnly)>
        {
            [a] = (D(1), D(10)), [b] = (D(5), D(8)), [c] = (D(9), D(12))
        };

        var shifts = GanttRules.CascadeShifts(leaves,
            [new GanttDependency(Guid.NewGuid(), a, b, 0), new GanttDependency(Guid.NewGuid(), b, c, 0)], [a]).ToDictionary(s => s.TaskId);

        shifts[b].Start.Should().Be(D(11));      // B：4 天，11~14
        shifts[b].End.Should().Be(D(14));
        shifts[b].Days.Should().Be(6);
        shifts[c].Start.Should().Be(D(15));      // C：4 天，15~18
        shifts[c].End.Should().Be(D(18));
    }

    [Fact]
    public void Cascade_NeverPullsEarlier_AndSkipsAlreadyLateTasks()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        var leaves = new Dictionary<Guid, (DateOnly, DateOnly)> { [a] = (D(1), D(3)), [b] = (D(20), D(22)) };

        GanttRules.CascadeShifts(leaves, [new GanttDependency(Guid.NewGuid(), a, b, 0)], [a]).Should().BeEmpty();
    }

    [Fact]
    public void Cascade_HonorsLag_AndOnlyTouchesAffectedBranch()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), unrelated = Guid.NewGuid(), unrelatedPred = Guid.NewGuid();
        var leaves = new Dictionary<Guid, (DateOnly, DateOnly)>
        {
            [a] = (D(1), D(5)), [b] = (D(6), D(8)),
            [unrelatedPred] = (D(1), D(20)), [unrelated] = (D(2), D(3))   // 既有衝突，但與被移動的 a 無關
        };

        var shifts = GanttRules.CascadeShifts(leaves,
            [new GanttDependency(Guid.NewGuid(), a, b, 2), new GanttDependency(Guid.NewGuid(), unrelatedPred, unrelated, 0)], [a]);

        shifts.Should().ContainSingle();
        shifts[0].TaskId.Should().Be(b);
        shifts[0].Start.Should().Be(D(8));   // a 結束 5 + 落後 2 + 1
        shifts[0].End.Should().Be(D(10));
    }

    [Fact]
    public void Cascade_MultiplePredecessors_UsesLatest()
    {
        Guid p1 = Guid.NewGuid(), p2 = Guid.NewGuid(), s = Guid.NewGuid();
        var leaves = new Dictionary<Guid, (DateOnly, DateOnly)> { [p1] = (D(1), D(10)), [p2] = (D(1), D(14)), [s] = (D(5), D(6)) };

        var shifts = GanttRules.CascadeShifts(leaves,
            [new GanttDependency(Guid.NewGuid(), p1, s, 0), new GanttDependency(Guid.NewGuid(), p2, s, 0)], [p1, p2]);

        shifts.Should().ContainSingle().Which.Start.Should().Be(D(15));
    }

    [Fact]
    public void Cascade_NoSuccessors_ReturnsEmpty() =>
        GanttRules.CascadeShifts(new Dictionary<Guid, (DateOnly, DateOnly)> { [Guid.NewGuid()] = (D(1), D(2)) }, [], [Guid.NewGuid()]).Should().BeEmpty();

    // ---------- 偏差與逾期 ----------

    [Fact]
    public void Variance_PositiveIsLater()
    {
        GanttRules.VarianceDays(D(10), D(13)).Should().Be(3);
        GanttRules.VarianceDays(D(10), D(8)).Should().Be(-2);
        GanttRules.VarianceDays(D(10), D(10)).Should().Be(0);
    }

    [Theory]
    [InlineData(5, 50, 10, true)]
    [InlineData(5, 100, 10, false)]   // 已完成不算逾期
    [InlineData(10, 0, 10, false)]    // 結束日當天尚未逾期
    [InlineData(11, 0, 10, false)]
    public void Overdue(int end, int percent, int today, bool expected) =>
        GanttRules.IsOverdue(D(end), percent, D(today)).Should().Be(expected);

    // ---------- 權限 ----------

    private static readonly Guid Pm = Guid.NewGuid();
    private static readonly Guid Tl = Guid.NewGuid();
    private static readonly Guid Be = Guid.NewGuid();
    private static readonly Guid Fe = Guid.NewGuid();
    private static readonly Guid Customer = Guid.NewGuid();

    private static readonly IReadOnlyList<(Guid UserId, RaciRole Role)> Members =
        [(Pm, RaciRole.Pm), (Tl, RaciRole.TechLead), (Be, RaciRole.Be), (Fe, RaciRole.Fe), (Customer, RaciRole.Stakeholder)];

    [Fact]
    public void Manage_OnlyPmAndTechLead()
    {
        GanttRules.CanManage(Pm, Members).Should().BeTrue();
        GanttRules.CanManage(Tl, Members).Should().BeTrue();
        GanttRules.CanManage(Be, Members).Should().BeFalse();
        GanttRules.CanManage(Customer, Members).Should().BeFalse();
    }

    [Fact]
    public void Progress_AssigneeOwnTasksOnly()
    {
        GanttRules.CanUpdateProgress(Be, Be, Members).Should().BeTrue();     // 被指派者
        GanttRules.CanUpdateProgress(Fe, Be, Members).Should().BeFalse();    // 別人的任務
        GanttRules.CanUpdateProgress(Pm, Be, Members).Should().BeTrue();     // PM 可更新所有
        GanttRules.CanUpdateProgress(Be, null, Members).Should().BeFalse();  // 未指派
        GanttRules.CanUpdateProgress(Customer, Customer, Members).Should().BeFalse(); // 客戶不可
    }

    [Fact]
    public void Assignee_InternalMembersOnly()
    {
        GanttRules.IsValidAssignee(Be, Members).Should().BeTrue();
        GanttRules.IsValidAssignee(Customer, Members).Should().BeFalse();
        GanttRules.IsValidAssignee(Guid.NewGuid(), Members).Should().BeFalse();
    }
}
