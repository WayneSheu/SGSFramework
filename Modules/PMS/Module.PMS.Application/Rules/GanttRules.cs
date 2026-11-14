using System.Globalization;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

/// <summary>任務節點（排程運算用，與持久化實體分離）。</summary>
public sealed record GanttTaskNode(
    Guid Id, Guid? ParentId, int SortOrder, string Name, DateOnly Start, DateOnly End, bool IsMilestone, int Percent);

/// <summary>任務前置相依（完成後才開始，FS）。</summary>
public sealed record GanttDependency(Guid Id, Guid PredecessorId, Guid SuccessorId, int LagDays);

/// <summary>排版後的一列：WBS 編號、層級與（摘要任務已彙總的）有效起迄與進度。</summary>
public sealed record GanttRow(
    Guid Id, Guid? ParentId, string Wbs, int Level, bool HasChildren, DateOnly Start, DateOnly End, int Percent);

/// <summary>連動順延的結果：被順延的任務與其新起迄日。</summary>
public sealed record GanttShift(Guid TaskId, DateOnly Start, DateOnly End, int Days);

/// <summary>
/// 專案甘特圖排程規則（SOP 啟動規劃：WBS、甘特圖），純函式：
/// WBS 編號與摘要任務彙總、相依循環偵測、相依衝突、連動順延、基準線偏差、逾期判定。
/// 日期為日曆日（含起訖日）；相依僅限無子任務的任務之間（Finish-to-Start，可設落後天數）。
/// </summary>
public static class GanttRules
{
    public const int MaxDepth = 5;
    public const int MaxSpanDays = 3650;
    public const int MaxLagDays = 365;
    public const int MaxTasksPerProject = 500;
    public const int MaxBaselines = 10;

    // ---------- 驗證 ----------

    public static string? ValidateTask(string? name, DateOnly start, DateOnly end, bool isMilestone, int percent)
    {
        if (string.IsNullOrWhiteSpace(name)) return "任務名稱為必填。";
        if (name.Trim().Length > 200) return "任務名稱不得超過 200 字。";
        if (start == default || end == default) return "開始日與結束日為必填。";
        if (end < start) return "結束日不可早於開始日。";
        if (isMilestone && start != end) return "里程碑的開始日與結束日須為同一天。";
        if (end.DayNumber - start.DayNumber + 1 > MaxSpanDays) return $"單一任務期間不得超過 {MaxSpanDays} 天。";
        if (percent is < 0 or > 100) return "完成百分比須介於 0 到 100。";
        return null;
    }

    public static string? ValidateLag(int lagDays) =>
        lagDays is < -MaxLagDays or > MaxLagDays ? $"落後天數須介於 -{MaxLagDays} 到 {MaxLagDays}。" : null;

    // ---------- 排版：WBS、層級與摘要彙總 ----------

    /// <summary>
    /// 依階層與排序展開成扁平列（先序走訪），編號 1、1.1、1.2…；有子任務者的起迄取子孫的最早與最晚，
    /// 進度取子任務依期間天數加權平均。上層不存在的孤兒任務視為頂層；防範上層鏈循環。
    /// </summary>
    public static IReadOnlyList<GanttRow> Layout(IReadOnlyCollection<GanttTaskNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var byId = nodes.ToDictionary(n => n.Id);
        var children = nodes
            .GroupBy(n => n.ParentId is { } p && byId.ContainsKey(p) && p != n.Id ? p : Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.SortOrder).ThenBy(n => n.Name, StringComparer.Ordinal).ThenBy(n => n.Id).ToList());

        var rows = new List<GanttRow>(nodes.Count);
        var visited = new HashSet<Guid>();

        void Visit(Guid parentKey, string prefix, int level)
        {
            if (!children.TryGetValue(parentKey, out var list)) return;

            var index = 0;
            foreach (var node in list)
            {
                if (!visited.Add(node.Id)) continue;

                index++;
                var wbs = prefix.Length == 0 ? index.ToString(CultureInfo.InvariantCulture) : $"{prefix}.{index}";
                var position = rows.Count;
                rows.Add(new GanttRow(node.Id, node.ParentId, wbs, level, false, node.Start, node.End, node.Percent));
                Visit(node.Id, wbs, level + 1);

                // 有子任務：以其後代（已走訪）彙總
                var descendantEnd = rows.Count;
                if (descendantEnd > position + 1)
                    rows[position] = Rollup(rows[position], rows.GetRange(position + 1, descendantEnd - position - 1), node.Id);
            }
        }

        Visit(Guid.Empty, string.Empty, 0);

        // 若上層鏈有循環導致部分節點未被走訪，補在最後（不遺漏任務）
        foreach (var node in nodes.Where(n => !visited.Contains(n.Id)).OrderBy(n => n.SortOrder))
            rows.Add(new GanttRow(node.Id, node.ParentId, (rows.Count + 1).ToString(CultureInfo.InvariantCulture), 0, false, node.Start, node.End, node.Percent));

        return rows;
    }

    private static GanttRow Rollup(GanttRow self, List<GanttRow> descendants, Guid selfId)
    {
        // 只用「直接子任務」計算進度；起迄用所有葉節點（無子任務者）取最早與最晚
        var leaves = descendants.Where(d => !descendants.Any(x => x.ParentId == d.Id)).ToList();
        var start = leaves.Min(l => l.Start);
        var end = leaves.Max(l => l.End);

        var direct = descendants.Where(d => d.ParentId == selfId).ToList();
        var totalWeight = direct.Sum(d => Weight(d));
        var percent = totalWeight == 0 ? 0 : (int)Math.Round(direct.Sum(d => d.Percent * (double)Weight(d)) / totalWeight, MidpointRounding.AwayFromZero);

        return self with { HasChildren = true, Start = start, End = end, Percent = percent };
    }

    private static int Weight(GanttRow row) => Math.Max(1, row.End.DayNumber - row.Start.DayNumber + 1);

    // ---------- 階層與排序 ----------

    /// <summary>某任務的層級（頂層為 0）；上層鏈循環時回傳 <see cref="MaxDepth"/> + 1。</summary>
    public static int LevelOf(Guid? parentId, IReadOnlyDictionary<Guid, Guid?> parents)
    {
        var level = 0;
        var current = parentId;
        var guard = 0;
        while (current is { } id)
        {
            level++;
            if (++guard > MaxDepth + 2) return MaxDepth + 1;
            current = parents.TryGetValue(id, out var next) ? next : null;
        }

        return level;
    }

    /// <summary><paramref name="candidateId"/> 是否為 <paramref name="ancestorId"/> 自己或其後代（用於禁止把任務搬到自己底下）。</summary>
    public static bool IsSelfOrDescendant(Guid ancestorId, Guid candidateId, IReadOnlyDictionary<Guid, Guid?> parents)
    {
        var current = (Guid?)candidateId;
        var guard = 0;
        while (current is { } id)
        {
            if (id == ancestorId) return true;
            if (++guard > 1000) return true;
            current = parents.TryGetValue(id, out var next) ? next : null;
        }

        return false;
    }

    /// <summary>把 <paramref name="movingId"/> 排到 <paramref name="beforeId"/> 之前（空＝排到最後），回傳新的同層順序。</summary>
    public static IReadOnlyList<Guid> Reposition(IReadOnlyList<Guid> orderedSiblings, Guid movingId, Guid? beforeId)
    {
        var list = orderedSiblings.Where(id => id != movingId).ToList();
        var index = beforeId is { } b && list.IndexOf(b) is var i and >= 0 ? i : list.Count;
        list.Insert(index, movingId);
        return list;
    }

    // ---------- 相依 ----------

    /// <summary>新增「前置→後繼」是否會形成循環（含自我相依）。</summary>
    public static bool WouldCreateCycle(IReadOnlyCollection<GanttDependency> existing, Guid predecessorId, Guid successorId)
    {
        if (predecessorId == successorId) return true;

        var next = existing.GroupBy(d => d.PredecessorId).ToDictionary(g => g.Key, g => g.Select(d => d.SuccessorId).ToList());
        var stack = new Stack<Guid>([successorId]);
        var seen = new HashSet<Guid>();
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == predecessorId) return true;
            if (!seen.Add(current) || !next.TryGetValue(current, out var successors)) continue;
            foreach (var s in successors) stack.Push(s);
        }

        return false;
    }

    /// <summary>後繼任務最早可開始日：前置結束日 + 落後天數 + 1。</summary>
    public static DateOnly EarliestStart(DateOnly predecessorEnd, int lagDays) => predecessorEnd.AddDays(lagDays + 1);

    /// <summary>各後繼任務目前違反前置相依的天數（最大值；未違反者不列出）。</summary>
    public static IReadOnlyDictionary<Guid, int> Conflicts(IReadOnlyDictionary<Guid, GanttRow> rows, IReadOnlyCollection<GanttDependency> dependencies)
    {
        var result = new Dictionary<Guid, int>();
        foreach (var dep in dependencies)
        {
            if (!rows.TryGetValue(dep.PredecessorId, out var pred) || !rows.TryGetValue(dep.SuccessorId, out var succ)) continue;

            var days = EarliestStart(pred.End, dep.LagDays).DayNumber - succ.Start.DayNumber;
            if (days > 0 && (!result.TryGetValue(dep.SuccessorId, out var current) || days > current)) result[dep.SuccessorId] = days;
        }

        return result;
    }

    /// <summary>
    /// 連動順延：自 <paramref name="movedIds"/> 起，沿相依往後，把開始日早於「前置結束日 + 落後 + 1」的後繼任務整段往後推（保留期間長度）。
    /// 只往後推、不往前拉，且只動被牽連的任務（不會順手修正其他既有衝突）。回傳被順延的任務。
    /// </summary>
    public static IReadOnlyList<GanttShift> CascadeShifts(
        IReadOnlyDictionary<Guid, (DateOnly Start, DateOnly End)> leaves, IReadOnlyCollection<GanttDependency> dependencies, IReadOnlyCollection<Guid> movedIds)
    {
        var outgoing = dependencies.GroupBy(d => d.PredecessorId).ToDictionary(g => g.Key, g => g.ToList());

        // 被牽連的任務＝自 movedIds 沿相依可達的後繼
        var affected = new HashSet<Guid>();
        var queue = new Queue<Guid>(movedIds);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!outgoing.TryGetValue(id, out var deps)) continue;
            foreach (var dep in deps)
                if (affected.Add(dep.SuccessorId)) queue.Enqueue(dep.SuccessorId);
        }

        if (affected.Count == 0) return [];

        // 拓撲排序（Kahn）：只考慮與被牽連集合相關的邊
        var relevant = dependencies.Where(d => affected.Contains(d.SuccessorId) && (affected.Contains(d.PredecessorId) || movedIds.Contains(d.PredecessorId))).ToList();
        var indegree = affected.ToDictionary(id => id, _ => 0);
        foreach (var d in relevant)
            if (affected.Contains(d.PredecessorId)) indegree[d.SuccessorId]++;

        var current = leaves.ToDictionary(kv => kv.Key, kv => kv.Value);
        var original = leaves.ToDictionary(kv => kv.Key, kv => kv.Value);
        var order = new Queue<Guid>(affected.Where(id => indegree[id] == 0));
        var processed = 0;

        var relevantByPredecessor = relevant.GroupBy(d => d.PredecessorId).ToDictionary(g => g.Key, g => g.ToList());
        var relevantBySuccessor = relevant.GroupBy(d => d.SuccessorId).ToDictionary(g => g.Key, g => g.ToList());

        while (order.Count > 0)
        {
            var id = order.Dequeue();
            processed++;

            // 以所有前置的目前結束日決定此任務最早可開始日
            if (relevantBySuccessor.TryGetValue(id, out var incoming) && current.TryGetValue(id, out var self))
            {
                var required = incoming
                    .Where(d => current.ContainsKey(d.PredecessorId))
                    .Select(d => EarliestStart(current[d.PredecessorId].End, d.LagDays))
                    .DefaultIfEmpty(self.Start)
                    .Max();

                if (self.Start < required)
                {
                    var shift = required.DayNumber - self.Start.DayNumber;
                    current[id] = (self.Start.AddDays(shift), self.End.AddDays(shift));
                }
            }

            if (!relevantByPredecessor.TryGetValue(id, out var outs)) continue;
            foreach (var d in outs)
                if (affected.Contains(d.SuccessorId) && --indegree[d.SuccessorId] == 0) order.Enqueue(d.SuccessorId);
        }

        _ = processed; // 相依已確保無循環；若仍有殘留（理論上不會），未處理者維持原日期

        return affected
            .Where(id => current.TryGetValue(id, out var now) && original.TryGetValue(id, out var was) && now.Start != was.Start)
            .Select(id => new GanttShift(id, current[id].Start, current[id].End, current[id].Start.DayNumber - original[id].Start.DayNumber))
            .OrderBy(s => s.Start)
            .ToList();
    }

    // ---------- 基準線偏差與逾期 ----------

    /// <summary>偏差天數：正數為比基準線延後、負數為提前。</summary>
    public static int VarianceDays(DateOnly baseline, DateOnly current) => current.DayNumber - baseline.DayNumber;

    /// <summary>逾期：尚未完成且結束日已過。</summary>
    public static bool IsOverdue(DateOnly end, int percent, DateOnly today) => percent < 100 && end < today;

    // ---------- 權限 ----------

    /// <summary>維護甘特圖（新增、編輯、搬移、刪除、相依、基準線）：專案 PM、Tech Lead。</summary>
    public static bool CanManage(Guid userId, IReadOnlyList<(Guid UserId, RaciRole Role)> members) =>
        ProjectAccessPolicy.IsPm(userId, members) || ProjectAccessPolicy.IsTechLead(userId, members);

    /// <summary>更新進度：PM／Tech Lead，或被指派的內部成員（僅限自己的任務）。</summary>
    public static bool CanUpdateProgress(Guid userId, Guid? assigneeUserId, IReadOnlyList<(Guid UserId, RaciRole Role)> members) =>
        CanManage(userId, members)
        || (assigneeUserId == userId && ProjectAccessPolicy.Evaluate(userId, members) == ProjectAccessLevel.Internal);

    /// <summary>負責人須為專案內部成員。</summary>
    public static bool IsValidAssignee(Guid assigneeUserId, IReadOnlyList<(Guid UserId, RaciRole Role)> members) =>
        ProjectAccessPolicy.Evaluate(assigneeUserId, members) == ProjectAccessLevel.Internal;
}
