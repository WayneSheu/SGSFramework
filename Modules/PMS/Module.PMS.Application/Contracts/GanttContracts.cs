using System.Text.Json.Serialization;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Contracts;

/// <summary>圖上疊加的里程碑種類（資料來自既有功能，不另存）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GanttMilestoneKind
{
    FreezeLine = 1,
    Gate = 2,
    Release = 3,
    CrDelay = 4,
    Uat = 5
}

public sealed record GanttPredecessorDto(Guid DependencyId, Guid PredecessorId, string PredecessorWbs, string PredecessorName, int LagDays);

/// <param name="Start">有效開始日（摘要任務為子任務彙總）。</param>
/// <param name="ConflictDays">違反前置相依的天數（0＝無衝突）。</param>
/// <param name="StartVarianceDays">相對基準線的開始偏差天數（正數＝延後）；無基準線或基準線無此任務為空。</param>
/// <param name="CanEdit">可維護此任務（PM／Tech Lead）。</param>
/// <param name="CanUpdateProgress">可更新此任務進度（PM／Tech Lead 或被指派者；摘要任務不可）。</param>
public sealed record GanttTaskDto(
    Guid Id, Guid? ParentId, string Wbs, int Level, bool HasChildren, string Name, string? Description,
    DateOnly Start, DateOnly End, int DurationDays, bool IsMilestone, int Percent, Guid? AssigneeUserId, ProjectPhase? Phase,
    IReadOnlyList<GanttPredecessorDto> Predecessors, int ConflictDays, bool IsOverdue,
    DateOnly? BaselineStart, DateOnly? BaselineEnd, int? StartVarianceDays, int? EndVarianceDays,
    bool CanEdit, bool CanUpdateProgress, string RowVersion);

public sealed record GanttMilestoneDto(GanttMilestoneKind Kind, DateOnly Date, string Label, string? Status);

public sealed record GanttBaselineDto(Guid Id, int Number, string Name, string? Note, int TaskCount, string CreatedBy, DateTimeOffset CreatedAtUtc);

public sealed record GanttActionsDto(bool CanManage, bool CanSeedPhases);

/// <param name="Today">台灣日期，供畫「今天」線與判斷逾期。</param>
/// <param name="RangeStart">涵蓋所有任務、里程碑與基準線的最早日期（空＝尚無資料）。</param>
/// <param name="SelectedBaselineId">本次對照的基準線（省略查詢參數時預設最新一份；無基準線為空）。</param>
/// <param name="ProjectEndVarianceDays">整體結束日相對基準線的偏差天數（正數＝延後）。</param>
public sealed record GanttDto(
    Guid ProjectId, string ProjectCode, string ProjectName, DateOnly Today, DateOnly? RangeStart, DateOnly? RangeEnd,
    IReadOnlyList<GanttTaskDto> Tasks, IReadOnlyList<GanttMilestoneDto> Milestones, IReadOnlyList<GanttBaselineDto> Baselines,
    Guid? SelectedBaselineId, int? ProjectEndVarianceDays, GanttActionsDto Actions);

/// <param name="BeforeTaskId">排在此任務之前（同層）；空＝排到最後。</param>
public sealed record CreateGanttTaskRequest(
    string Name, string? Description, Guid? ParentId, DateOnly Start, DateOnly End, bool IsMilestone,
    Guid? AssigneeUserId, ProjectPhase? Phase, Guid? BeforeTaskId = null);

/// <summary>摘要任務（有子任務）的日期與進度由子任務決定，請求中的 <c>Start</c>、<c>End</c>、<c>Percent</c> 會被忽略。</summary>
/// <param name="Cascade">日期變動後是否連動順延後繼任務。</param>
public sealed record UpdateGanttTaskRequest(
    string Name, string? Description, DateOnly Start, DateOnly End, bool IsMilestone, Guid? AssigneeUserId, ProjectPhase? Phase,
    int Percent, string RowVersion, bool Cascade = true);

/// <summary>圖上拖曳：移動或縮放。里程碑只能移動；摘要任務只能整段移動（期間長度不可變）。</summary>
public sealed record ScheduleGanttTaskRequest(DateOnly Start, DateOnly End, bool Cascade, string RowVersion);

public sealed record UpdateTaskProgressRequest(int Percent, string RowVersion);

/// <summary>調整階層與順序：搬到 <paramref name="ParentId"/> 之下（空＝頂層），排在 <paramref name="BeforeTaskId"/> 之前（空＝最後）。</summary>
public sealed record MoveGanttTaskRequest(Guid? ParentId, Guid? BeforeTaskId);

public sealed record AddGanttDependencyRequest(Guid PredecessorId, Guid SuccessorId, int LagDays = 0, bool Cascade = true);

public sealed record CreateBaselineRequest(string Name, string? Note);

/// <summary>以六個 SSDLC 階段為頂層任務建立預設計畫（僅在專案尚無任務時），階段間以完成後開始相依串接。</summary>
public sealed record SeedGanttPhasesRequest(DateOnly StartDate, int WeeksPerPhase = 2);

/// <param name="TaskId">新建或被操作的任務識別碼。</param>
/// <param name="ShiftedCount">連動順延的任務數。</param>
public sealed record GanttMutationResult(Guid? TaskId, int ShiftedCount);
