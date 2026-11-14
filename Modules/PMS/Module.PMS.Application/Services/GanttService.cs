using Microsoft.EntityFrameworkCore;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 專案甘特圖（SOP 啟動規劃：WBS、甘特圖）：WBS 任務（階層、起迄、負責人、進度）、前置相依（FS）、基準線與偏差，
/// 並自動疊加既有資料的里程碑（需求凍結線、Gate 放行、上線檢核表預計上線日、已核准 CR 的新上線日、UAT 驗收）。
/// PM／Tech Lead 維護全部；被指派者只能更新自己任務的進度；內部成員可檢視；客戶與非成員一律 404。
/// </summary>
public sealed class GanttService(PMSDbContext db, TimeProvider clock)
{
    private static readonly (ProjectPhase Phase, string Name)[] SeedPhases =
    [
        (ProjectPhase.Initiation, "啟動與規劃"),
        (ProjectPhase.Requirement, "需求分析"),
        (ProjectPhase.Design, "系統設計"),
        (ProjectPhase.Development, "開發與實作"),
        (ProjectPhase.Testing, "測試與驗證"),
        (ProjectPhase.DeployClose, "部署與結案")
    ];

    // ---------- 查詢 ----------

    public async Task<PmsResult<GanttDto>> GetAsync(Guid userId, Guid projectId, Guid? baselineId, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, projectId, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        var rows = GanttRules.Layout(ToNodes(c.Tasks));
        var rowById = rows.ToDictionary(r => r.Id);
        var taskById = c.Tasks.ToDictionary(t => t.Id);
        var conflicts = GanttRules.Conflicts(rowById, ToDependencies(c.Deps));
        var today = Today();

        // 基準線：省略參數＝最新一份；Guid.Empty＝不對照
        var baselines = await db.ProjectBaselines.AsNoTracking().Where(b => b.ProjectId == projectId)
            .OrderBy(b => b.Number)
            .Select(b => new GanttBaselineDto(b.Id, b.Number, b.Name, b.Note, b.Tasks.Count, b.CreatedBy, b.CreatedAtUtc))
            .ToListAsync(ct).ConfigureAwait(false);

        var selected = baselineId == Guid.Empty ? null : baselineId is { } wanted ? baselines.FirstOrDefault(b => b.Id == wanted) : baselines.LastOrDefault();
        Dictionary<Guid, (DateOnly Start, DateOnly End)> baselineTasks = [];
        if (selected is not null)
        {
            baselineTasks = await db.ProjectBaselineTasks.AsNoTracking().Where(t => t.BaselineId == selected.Id)
                .ToDictionaryAsync(t => t.TaskId, t => (t.StartDate, t.EndDate), ct).ConfigureAwait(false);
        }

        var predecessorsBySuccessor = c.Deps.GroupBy(d => d.SuccessorId).ToDictionary(g => g.Key, g => g.ToList());
        var manage = GanttRules.CanManage(userId, c.Members);

        var tasks = rows.Select(r =>
        {
            var t = taskById[r.Id];
            var predecessors = predecessorsBySuccessor.TryGetValue(r.Id, out var list)
                ? list.Where(d => rowById.ContainsKey(d.PredecessorId))
                      .Select(d => new GanttPredecessorDto(d.Id, d.PredecessorId, rowById[d.PredecessorId].Wbs, taskById[d.PredecessorId].Name, d.LagDays)).ToList()
                : [];

            DateOnly? bs = null, be = null;
            int? sv = null, ev = null;
            if (baselineTasks.TryGetValue(r.Id, out var b))
            {
                bs = b.Start;
                be = b.End;
                sv = GanttRules.VarianceDays(b.Start, r.Start);
                ev = GanttRules.VarianceDays(b.End, r.End);
            }

            return new GanttTaskDto(
                r.Id, t.ParentId, r.Wbs, r.Level, r.HasChildren, t.Name, t.Description, r.Start, r.End,
                r.End.DayNumber - r.Start.DayNumber + 1, t.IsMilestone, r.Percent, t.AssigneeUserId, t.Phase,
                predecessors, conflicts.GetValueOrDefault(r.Id), GanttRules.IsOverdue(r.End, r.Percent, today),
                bs, be, sv, ev,
                CanEdit: manage, CanUpdateProgress: !r.HasChildren && GanttRules.CanUpdateProgress(userId, t.AssigneeUserId, c.Members),
                Convert.ToBase64String(t.RowVersion));
        }).ToList();

        var milestones = await LoadMilestonesAsync(c.Project, ct).ConfigureAwait(false);

        // 範圍：任務、里程碑與基準線的最早與最晚日期
        var dates = rows.SelectMany(r => new[] { r.Start, r.End })
            .Concat(milestones.Select(m => m.Date))
            .Concat(baselineTasks.Values.SelectMany(v => new[] { v.Start, v.End }))
            .ToList();

        int? projectEndVariance = null;
        if (baselineTasks.Count > 0 && rows.Count > 0)
            projectEndVariance = GanttRules.VarianceDays(baselineTasks.Values.Max(v => v.End), rows.Max(r => r.End));

        return PmsResult<GanttDto>.Success(new GanttDto(
            c.Project.Id, c.Project.Code, c.Project.Name, today,
            dates.Count == 0 ? null : dates.Min(), dates.Count == 0 ? null : dates.Max(),
            tasks, milestones, baselines, selected?.Id, projectEndVariance,
            new GanttActionsDto(manage, manage && c.Tasks.Count == 0 && c.Project.Status == ProjectStatus.Active)));
    }

    // ---------- 任務：建立、更新、拖曳排程、進度、位置、刪除 ----------

    public async Task<PmsResult<GanttMutationResult>> CreateTaskAsync(Guid userId, string actor, Guid projectId, CreateGanttTaskRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, projectId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        var invalid = GanttRules.ValidateTask(request.Name, request.Start, request.End, request.IsMilestone, 0);
        if (invalid is not null) return PmsError.Validation(invalid);
        if (request.Description is { Length: > 1000 }) return PmsError.Validation("任務說明不得超過 1000 字。");
        if (c.Tasks.Count >= GanttRules.MaxTasksPerProject) return PmsError.Validation($"每個專案最多 {GanttRules.MaxTasksPerProject} 個任務。");
        if (request.AssigneeUserId is { } assignee && !GanttRules.IsValidAssignee(assignee, c.Members))
            return PmsError.Validation("負責人須為專案內部成員。");

        var parents = c.Tasks.ToDictionary(t => t.Id, t => t.ParentId);
        if (request.ParentId is { } parentId)
        {
            var parent = c.Tasks.FirstOrDefault(t => t.Id == parentId);
            if (parent is null) return PmsError.NotFound("上層任務不存在。");
            if (parent.IsMilestone) return PmsError.Validation("里程碑不能有子任務。");
            if (HasDependencies(c, parentId)) return PmsError.Conflict("PMS.INVALID_TRANSITION", "此任務已設定前置相依，請先移除相依再新增子任務。");
        }

        if (GanttRules.LevelOf(request.ParentId, parents) >= GanttRules.MaxDepth)
            return PmsError.Validation($"任務階層最多 {GanttRules.MaxDepth} 層。");

        var now = clock.GetUtcNow();
        var task = new ProjectTask
        {
            Id = Guid.NewGuid(), ProjectId = projectId, ParentId = request.ParentId, Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            StartDate = request.Start, EndDate = request.End, IsMilestone = request.IsMilestone,
            AssigneeUserId = request.AssigneeUserId, Phase = request.Phase, PercentComplete = 0,
            CreatedBy = actor, CreatedAtUtc = now
        };
        db.ProjectTasks.Add(task);
        c.Tasks.Add(task);

        var siblings = OrderedSiblings(c.Tasks, request.ParentId).Where(t => t.Id != task.Id).Select(t => t.Id).ToList();
        ApplyOrder(c.Tasks, GanttRules.Reposition(siblings, task.Id, request.BeforeTaskId), actor, now, skip: task.Id);

        return await SaveAsync(new GanttMutationResult(task.Id, 0), ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<GanttMutationResult>> UpdateTaskAsync(
        Guid userId, string actor, Guid projectId, Guid taskId, UpdateGanttTaskRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, projectId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        var task = c.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return PmsError.NotFound("任務不存在。");
        if (!UseRowVersion(task, request.RowVersion)) return PmsError.Validation("rowVersion 格式不正確。");

        var hasChildren = HasChildren(c, taskId);
        var start = hasChildren ? task.StartDate : request.Start;
        var end = hasChildren ? task.EndDate : request.End;
        var milestone = hasChildren ? task.IsMilestone : request.IsMilestone;
        var percent = hasChildren ? task.PercentComplete : request.Percent;

        var invalid = GanttRules.ValidateTask(request.Name, start, end, milestone, percent);
        if (invalid is not null) return PmsError.Validation(invalid);
        if (request.Description is { Length: > 1000 }) return PmsError.Validation("任務說明不得超過 1000 字。");
        if (request.AssigneeUserId is { } assignee && assignee != task.AssigneeUserId && !GanttRules.IsValidAssignee(assignee, c.Members))
            return PmsError.Validation("負責人須為專案內部成員。");

        var now = clock.GetUtcNow();
        var datesChanged = start != task.StartDate || end != task.EndDate;
        task.Name = request.Name.Trim();
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.StartDate = start;
        task.EndDate = end;
        task.IsMilestone = milestone;
        task.PercentComplete = (byte)percent;
        task.AssigneeUserId = request.AssigneeUserId;
        task.Phase = request.Phase;
        Touch(task, actor, now);

        var shifted = 0;
        if (datesChanged && request.Cascade && !hasChildren) shifted = Cascade(c, [task.Id], actor, now);

        return await SaveAsync(new GanttMutationResult(task.Id, shifted), ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<GanttMutationResult>> ScheduleTaskAsync(
        Guid userId, string actor, Guid projectId, Guid taskId, ScheduleGanttTaskRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, projectId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        var task = c.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return PmsError.NotFound("任務不存在。");
        if (!UseRowVersion(task, request.RowVersion)) return PmsError.Validation("rowVersion 格式不正確。");

        var now = clock.GetUtcNow();
        List<Guid> movedLeaves;

        if (HasChildren(c, taskId))
        {
            // 摘要任務：整段移動，期間長度不可變；所有葉子任務同步平移
            var row = GanttRules.Layout(ToNodes(c.Tasks)).First(r => r.Id == taskId);
            if (request.End.DayNumber - request.Start.DayNumber != row.End.DayNumber - row.Start.DayNumber)
                return PmsError.Validation("摘要任務的期間由子任務決定，只能整段移動，不可縮放。");

            var delta = request.Start.DayNumber - row.Start.DayNumber;
            if (delta == 0) return PmsResult<GanttMutationResult>.Success(new GanttMutationResult(taskId, 0));

            var leaves = DescendantLeaves(c, taskId);
            foreach (var leaf in leaves)
            {
                leaf.StartDate = leaf.StartDate.AddDays(delta);
                leaf.EndDate = leaf.EndDate.AddDays(delta);
                Touch(leaf, actor, now);
            }

            movedLeaves = leaves.Select(l => l.Id).ToList();
        }
        else
        {
            var invalid = GanttRules.ValidateTask(task.Name, request.Start, request.End, task.IsMilestone, task.PercentComplete);
            if (invalid is not null) return PmsError.Validation(invalid);

            task.StartDate = request.Start;
            task.EndDate = request.End;
            Touch(task, actor, now);
            movedLeaves = [task.Id];
        }

        var shifted = request.Cascade ? Cascade(c, movedLeaves, actor, now) : 0;
        return await SaveAsync(new GanttMutationResult(taskId, shifted), ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<GanttMutationResult>> UpdateProgressAsync(
        Guid userId, string actor, Guid projectId, Guid taskId, UpdateTaskProgressRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadAsync(userId, projectId, track: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        var task = c.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return PmsError.NotFound("任務不存在。");
        if (!GanttRules.CanUpdateProgress(userId, task.AssigneeUserId, c.Members))
            return PmsError.Forbidden("僅專案 PM、Tech Lead 或此任務的負責人可更新進度。");
        if (HasChildren(c, taskId)) return PmsError.Validation("摘要任務的進度由子任務彙總，請更新子任務。");
        if (request.Percent is < 0 or > 100) return PmsError.Validation("完成百分比須介於 0 到 100。");
        if (!UseRowVersion(task, request.RowVersion)) return PmsError.Validation("rowVersion 格式不正確。");

        task.PercentComplete = (byte)request.Percent;
        Touch(task, actor, clock.GetUtcNow());
        return await SaveAsync(new GanttMutationResult(taskId, 0), ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<GanttMutationResult>> MoveTaskAsync(
        Guid userId, string actor, Guid projectId, Guid taskId, MoveGanttTaskRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, projectId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        var task = c.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return PmsError.NotFound("任務不存在。");

        var parents = c.Tasks.ToDictionary(t => t.Id, t => t.ParentId);
        if (request.ParentId is { } parentId)
        {
            var parent = c.Tasks.FirstOrDefault(t => t.Id == parentId);
            if (parent is null) return PmsError.NotFound("上層任務不存在。");
            if (GanttRules.IsSelfOrDescendant(taskId, parentId, parents)) return PmsError.Validation("不可把任務搬到自己或自己的子任務底下。");
            if (parent.IsMilestone) return PmsError.Validation("里程碑不能有子任務。");
            if (parentId != task.ParentId && HasDependencies(c, parentId))
                return PmsError.Conflict("PMS.INVALID_TRANSITION", "目標任務已設定前置相依，請先移除相依再搬入子任務。");
        }

        if (GanttRules.LevelOf(request.ParentId, parents) + SubtreeHeight(c, taskId) >= GanttRules.MaxDepth)
            return PmsError.Validation($"任務階層最多 {GanttRules.MaxDepth} 層。");

        var now = clock.GetUtcNow();
        task.ParentId = request.ParentId;
        Touch(task, actor, now);

        var siblings = OrderedSiblings(c.Tasks, request.ParentId).Where(t => t.Id != taskId).Select(t => t.Id).ToList();
        ApplyOrder(c.Tasks, GanttRules.Reposition(siblings, taskId, request.BeforeTaskId), actor, now, skip: null);

        return await SaveAsync(new GanttMutationResult(taskId, 0), ct).ConfigureAwait(false);
    }

    /// <summary>刪除任務（軟刪除，連同所有子任務），並移除牽涉到的相依。基準線快照不受影響。</summary>
    public async Task<PmsResult<GanttMutationResult>> DeleteTaskAsync(Guid userId, string actor, Guid projectId, Guid taskId, CancellationToken ct)
    {
        var ctx = await LoadManageAsync(userId, projectId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        var task = c.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return PmsError.NotFound("任務不存在。");

        var doomed = new HashSet<Guid> { taskId };
        var queue = new Queue<Guid>([taskId]);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            foreach (var child in c.Tasks.Where(t => t.ParentId == id))
                if (doomed.Add(child.Id)) queue.Enqueue(child.Id);
        }

        var now = clock.GetUtcNow();
        foreach (var t in c.Tasks.Where(t => doomed.Contains(t.Id)))
        {
            t.IsDeleted = true;
            t.DeletedAt = now;
            Touch(t, actor, now);
        }

        db.ProjectTaskDependencies.RemoveRange(c.Deps.Where(d => doomed.Contains(d.PredecessorId) || doomed.Contains(d.SuccessorId)));
        return await SaveAsync(new GanttMutationResult(null, doomed.Count), ct).ConfigureAwait(false);
    }

    // ---------- 相依 ----------

    public async Task<PmsResult<GanttMutationResult>> AddDependencyAsync(
        Guid userId, string actor, Guid projectId, AddGanttDependencyRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, projectId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        var lagError = GanttRules.ValidateLag(request.LagDays);
        if (lagError is not null) return PmsError.Validation(lagError);

        var pred = c.Tasks.FirstOrDefault(t => t.Id == request.PredecessorId);
        var succ = c.Tasks.FirstOrDefault(t => t.Id == request.SuccessorId);
        if (pred is null || succ is null) return PmsError.NotFound("任務不存在。");
        if (HasChildren(c, pred.Id) || HasChildren(c, succ.Id))
            return PmsError.Validation("相依只能設定在沒有子任務的任務之間（摘要任務的日期由子任務決定）。");
        if (c.Deps.Any(d => d.PredecessorId == pred.Id && d.SuccessorId == succ.Id))
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "這組前置相依已存在。");
        if (GanttRules.WouldCreateCycle(ToDependencies(c.Deps), pred.Id, succ.Id))
            return PmsError.Validation("此相依會形成循環（任務互相等待），無法建立。");

        var dependency = new ProjectTaskDependency { Id = Guid.NewGuid(), ProjectId = projectId, PredecessorId = pred.Id, SuccessorId = succ.Id, LagDays = request.LagDays };
        db.ProjectTaskDependencies.Add(dependency);
        c.Deps.Add(dependency);

        var shifted = request.Cascade ? Cascade(c, [pred.Id], actor, clock.GetUtcNow()) : 0;
        return await SaveAsync(new GanttMutationResult(succ.Id, shifted), ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<GanttMutationResult>> RemoveDependencyAsync(Guid userId, Guid projectId, Guid dependencyId, CancellationToken ct)
    {
        var ctx = await LoadManageAsync(userId, projectId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var dependency = ctx.Value!.Deps.FirstOrDefault(d => d.Id == dependencyId);
        if (dependency is null) return PmsError.NotFound("相依不存在。");

        db.ProjectTaskDependencies.Remove(dependency);
        return await SaveAsync(new GanttMutationResult(dependency.SuccessorId, 0), ct).ConfigureAwait(false);
    }

    // ---------- 基準線 ----------

    public async Task<PmsResult<GanttBaselineDto>> CreateBaselineAsync(Guid userId, string actor, Guid projectId, CreateBaselineRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, projectId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        if (string.IsNullOrWhiteSpace(request.Name)) return PmsError.Validation("基準線名稱為必填。");
        if (request.Name.Trim().Length > 100) return PmsError.Validation("基準線名稱不得超過 100 字。");
        if (request.Note is { Length: > 500 }) return PmsError.Validation("基準線備註不得超過 500 字。");
        if (c.Tasks.Count == 0) return PmsError.Validation("尚無任務，無法建立基準線。");

        var existing = await db.ProjectBaselines.Where(b => b.ProjectId == projectId).Select(b => b.Number).ToListAsync(ct).ConfigureAwait(false);
        if (existing.Count >= GanttRules.MaxBaselines) return PmsError.Validation($"每個專案最多 {GanttRules.MaxBaselines} 份基準線，請先刪除不再需要的。");

        var now = clock.GetUtcNow();
        var rows = GanttRules.Layout(ToNodes(c.Tasks));
        var taskById = c.Tasks.ToDictionary(t => t.Id);
        var baseline = new ProjectBaseline
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Number = (existing.Count == 0 ? 0 : existing.Max()) + 1,
            Name = request.Name.Trim(), Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedBy = actor, CreatedAtUtc = now,
            Tasks = rows.Select(r => new ProjectBaselineTask
            {
                Id = Guid.NewGuid(), TaskId = r.Id, Name = taskById[r.Id].Name, StartDate = r.Start, EndDate = r.End, IsMilestone = taskById[r.Id].IsMilestone
            }).ToList()
        };
        db.ProjectBaselines.Add(baseline);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        db.ChangeTracker.Clear();
        return PmsResult<GanttBaselineDto>.Success(new GanttBaselineDto(baseline.Id, baseline.Number, baseline.Name, baseline.Note, baseline.Tasks.Count, actor, now));
    }

    public async Task<PmsResult<bool>> DeleteBaselineAsync(Guid userId, Guid projectId, Guid baselineId, CancellationToken ct)
    {
        var ctx = await LoadManageAsync(userId, projectId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var baseline = await db.ProjectBaselines.FirstOrDefaultAsync(b => b.Id == baselineId && b.ProjectId == projectId, ct).ConfigureAwait(false);
        if (baseline is null) return PmsError.NotFound("基準線不存在。");

        db.ProjectBaselines.Remove(baseline);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        db.ChangeTracker.Clear();
        return PmsResult<bool>.Success(true);
    }

    // ---------- 預設計畫 ----------

    public async Task<PmsResult<GanttMutationResult>> SeedPhasesAsync(Guid userId, string actor, Guid projectId, SeedGanttPhasesRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, projectId, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        if (c.Tasks.Count > 0) return PmsError.Conflict("PMS.INVALID_TRANSITION", "專案已有任務，不可再套用預設階段計畫。");
        if (c.Project.Status != ProjectStatus.Active) return PmsError.Conflict("PMS.INVALID_TRANSITION", "專案已結案或作廢。");
        if (request.StartDate == default) return PmsError.Validation("請指定計畫開始日。");
        if (request.WeeksPerPhase is < 1 or > 12) return PmsError.Validation("每階段週數須介於 1 到 12。");

        var now = clock.GetUtcNow();
        var start = request.StartDate;
        ProjectTask? previous = null;
        var order = 10;
        foreach (var (phase, name) in SeedPhases)
        {
            var end = start.AddDays(request.WeeksPerPhase * 7 - 1);
            var task = new ProjectTask
            {
                Id = Guid.NewGuid(), ProjectId = projectId, Name = name, StartDate = start, EndDate = end, Phase = phase,
                SortOrder = order, CreatedBy = actor, CreatedAtUtc = now
            };
            db.ProjectTasks.Add(task);
            if (previous is not null)
                db.ProjectTaskDependencies.Add(new ProjectTaskDependency { Id = Guid.NewGuid(), ProjectId = projectId, PredecessorId = previous.Id, SuccessorId = task.Id, LagDays = 0 });

            previous = task;
            start = end.AddDays(1);
            order += 10;
        }

        return await SaveAsync(new GanttMutationResult(null, SeedPhases.Length), ct).ConfigureAwait(false);
    }

    // ---------- 內部輔助 ----------

    private sealed record Context(
        Project Project, IReadOnlyList<(Guid UserId, RaciRole Role)> Members, List<ProjectTask> Tasks, List<ProjectTaskDependency> Deps);

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(SlaCalculator.Offset).DateTime);

    private async Task<PmsResult<Context>> LoadAsync(Guid userId, Guid projectId, bool track, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().Include(p => p.Members).Include(p => p.Gates)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound("專案不存在。");

        var members = project.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        if (ProjectAccessPolicy.Evaluate(userId, members) != ProjectAccessLevel.Internal) return PmsError.NotFound("專案不存在。");

        var tasks = await (track ? db.ProjectTasks.AsQueryable() : db.ProjectTasks.AsNoTracking())
            .Where(t => t.ProjectId == projectId && !t.IsDeleted).ToListAsync(ct).ConfigureAwait(false);
        var deps = await (track ? db.ProjectTaskDependencies.AsQueryable() : db.ProjectTaskDependencies.AsNoTracking())
            .Where(d => d.ProjectId == projectId).ToListAsync(ct).ConfigureAwait(false);

        return PmsResult<Context>.Success(new Context(project, members, tasks, deps));
    }

    /// <summary>載入（追蹤）並要求維護權限：PM／Tech Lead；專案須進行中。</summary>
    private async Task<PmsResult<Context>> LoadManageAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, projectId, track: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        if (!GanttRules.CanManage(userId, ctx.Value!.Members)) return PmsError.Forbidden("僅專案 PM 或 Tech Lead 可維護甘特圖。");
        if (ctx.Value.Project.Status != ProjectStatus.Active) return PmsError.Conflict("PMS.INVALID_TRANSITION", "專案已結案或作廢，不可再調整排程。");
        return ctx;
    }

    private async Task<PmsResult<GanttMutationResult>> SaveAsync(GanttMutationResult result, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "任務已被他人更新，請重新載入後再試。");
        }

        db.ChangeTracker.Clear();
        return PmsResult<GanttMutationResult>.Success(result);
    }

    private static IReadOnlyList<GanttTaskNode> ToNodes(IEnumerable<ProjectTask> tasks) =>
        tasks.Select(t => new GanttTaskNode(t.Id, t.ParentId, t.SortOrder, t.Name, t.StartDate, t.EndDate, t.IsMilestone, t.PercentComplete)).ToList();

    private static IReadOnlyList<GanttDependency> ToDependencies(IEnumerable<ProjectTaskDependency> deps) =>
        deps.Select(d => new GanttDependency(d.Id, d.PredecessorId, d.SuccessorId, d.LagDays)).ToList();

    private static bool HasChildren(Context c, Guid taskId) => c.Tasks.Any(t => t.ParentId == taskId);

    private static bool HasDependencies(Context c, Guid taskId) => c.Deps.Any(d => d.PredecessorId == taskId || d.SuccessorId == taskId);

    private static List<ProjectTask> OrderedSiblings(IEnumerable<ProjectTask> tasks, Guid? parentId) =>
        tasks.Where(t => t.ParentId == parentId).OrderBy(t => t.SortOrder).ThenBy(t => t.Name, StringComparer.Ordinal).ThenBy(t => t.Id).ToList();

    private static void ApplyOrder(IEnumerable<ProjectTask> tasks, IReadOnlyList<Guid> order, string actor, DateTimeOffset now, Guid? skip)
    {
        var byId = tasks.ToDictionary(t => t.Id);
        for (var i = 0; i < order.Count; i++)
        {
            if (!byId.TryGetValue(order[i], out var t)) continue;
            var sortOrder = (i + 1) * 10;
            if (t.SortOrder == sortOrder) continue;

            t.SortOrder = sortOrder;
            if (t.Id != skip) Touch(t, actor, now);
        }
    }

    /// <summary>此任務底下最深的子孫層數（無子孫為 0），用於判斷搬動後是否超過階層上限。</summary>
    private static int SubtreeHeight(Context c, Guid taskId)
    {
        var height = 0;
        var frontier = new List<Guid> { taskId };
        while (true)
        {
            var next = c.Tasks.Where(t => t.ParentId is { } p && frontier.Contains(p)).Select(t => t.Id).ToList();
            if (next.Count == 0 || height > GanttRules.MaxDepth) return height;
            height++;
            frontier = next;
        }
    }

    private static List<ProjectTask> DescendantLeaves(Context c, Guid taskId)
    {
        var result = new List<ProjectTask>();
        var stack = new Stack<Guid>([taskId]);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            var children = c.Tasks.Where(t => t.ParentId == id).ToList();
            if (children.Count == 0 && id != taskId) result.Add(c.Tasks.First(t => t.Id == id));
            foreach (var child in children) stack.Push(child.Id);
        }

        return result;
    }

    private bool UseRowVersion(ProjectTask task, string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion)) return false;
        try
        {
            var bytes = Convert.FromBase64String(rowVersion);
            if (bytes.Length == 0) return false;
            db.Entry(task).Property(x => x.RowVersion).OriginalValue = bytes;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>連動順延後繼任務（只動葉子任務），回傳被順延的數量。</summary>
    private static int Cascade(Context c, IReadOnlyCollection<Guid> movedIds, string actor, DateTimeOffset now)
    {
        var leaves = c.Tasks.Where(t => !HasChildren(c, t.Id)).ToDictionary(t => t.Id, t => (t.StartDate, t.EndDate));
        var shifts = GanttRules.CascadeShifts(leaves, ToDependencies(c.Deps), movedIds);
        if (shifts.Count == 0) return 0;

        var byId = c.Tasks.ToDictionary(t => t.Id);
        foreach (var shift in shifts)
        {
            var t = byId[shift.TaskId];
            t.StartDate = shift.Start;
            t.EndDate = shift.End;
            Touch(t, actor, now);
        }

        return shifts.Count;
    }

    private static void Touch(ProjectTask t, string actor, DateTimeOffset now)
    {
        t.UpdatedBy = actor;
        t.UpdatedAtUtc = now;
    }

    // ---------- 里程碑彙整（唯讀，來自既有功能） ----------

    private async Task<IReadOnlyList<GanttMilestoneDto>> LoadMilestonesAsync(Project project, CancellationToken ct)
    {
        var list = new List<GanttMilestoneDto>();
        if (project.FreezeLineDate is { } freeze) list.Add(new GanttMilestoneDto(GanttMilestoneKind.FreezeLine, freeze, "需求凍結線", null));

        foreach (var gate in project.Gates.Where(g => g.Status == GateStatus.Approved && g.ApprovedAt is not null))
            list.Add(new GanttMilestoneDto(GanttMilestoneKind.Gate, ToTaipeiDate(gate.ApprovedAt!.Value), $"{GateName(gate.Gate)} Gate 放行", "Approved"));

        var releases = await db.ReleaseChecklists.AsNoTracking().Where(r => r.ProjectId == project.Id)
            .Select(r => new { r.Version, r.PlannedDate, r.Status }).ToListAsync(ct).ConfigureAwait(false);
        foreach (var r in releases)
            list.Add(new GanttMilestoneDto(GanttMilestoneKind.Release, r.PlannedDate, $"預計上線 {r.Version}", r.Status.ToString()));

        var crs = await db.ChangeRequests.AsNoTracking()
            .Where(c => c.ProjectId == project.Id && c.Status == CrStatus.Approved && c.Decision == CrDecision.A_Delay && c.NewReleaseDate != null)
            .Select(c => new { c.CrNo, c.NewReleaseDate }).ToListAsync(ct).ConfigureAwait(false);
        foreach (var cr in crs)
            list.Add(new GanttMilestoneDto(GanttMilestoneKind.CrDelay, cr.NewReleaseDate!.Value, $"{cr.CrNo} 順延上線日", "Approved"));

        var uats = await db.UatSignoffs.AsNoTracking()
            .Where(u => u.ProjectId == project.Id && u.ResultAt != null && (u.Status == UatStatus.Accepted || u.Status == UatStatus.Rejected))
            .Select(u => new { u.Version, u.Status, u.ResultAt }).ToListAsync(ct).ConfigureAwait(false);
        foreach (var u in uats)
            list.Add(new GanttMilestoneDto(GanttMilestoneKind.Uat, ToTaipeiDate(u.ResultAt!.Value),
                $"UAT {u.Version} {(u.Status == UatStatus.Accepted ? "驗收通過" : "驗收不通過")}", u.Status.ToString()));

        return list.OrderBy(m => m.Date).ThenBy(m => m.Kind).ToList();
    }

    private static DateOnly ToTaipeiDate(DateTimeOffset value) => DateOnly.FromDateTime(value.ToOffset(SlaCalculator.Offset).DateTime);

    private static string GateName(GateKind gate) => gate switch
    {
        GateKind.Design => "Design",
        GateKind.Build => "Build",
        GateKind.Verification => "Verification",
        _ => "Release"
    };
}
