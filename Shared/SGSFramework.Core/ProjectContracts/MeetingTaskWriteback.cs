namespace SGSFramework.Core.ProjectContracts;

/// <summary>要回寫到專案的一筆會議待辦。</summary>
/// <param name="ActionItemId">冪等鍵：同一筆待辦只會在專案裡建立一個任務。</param>
public sealed record MeetingTaskItem(Guid ActionItemId, string Description, string? Owner, DateOnly? DueDate, string? Note);

/// <summary>會議待辦回寫專案的請求（會議助理 MTG → 專案管理 PMS）。</summary>
/// <param name="UserId">執行回寫的使用者；PMS 以此身分套用專案權限（須為專案 PM 或 Tech Lead）。</param>
/// <param name="Actor">寫入稽核欄位的操作者名稱。</param>
public sealed record MeetingTaskRequest(
    Guid ProjectId,
    Guid MeetingId,
    string MeetingTitle,
    DateOnly MeetingDate,
    Guid UserId,
    string Actor,
    IReadOnlyList<MeetingTaskItem> Items);

/// <summary>單筆回寫結果：成功時有 <see cref="TaskId"/>；<see cref="AlreadyExisted"/> 表示專案裡早已有對應任務（重送不會重複建立）。</summary>
public sealed record MeetingTaskResult(Guid ActionItemId, Guid? TaskId, bool AlreadyExisted, string? Error)
{
    public bool Succeeded => TaskId is not null;
}

/// <summary>
/// 會議待辦 → 專案 WBS 任務（Core 契約）：由 SGS.Modules.PMS 外掛實作，會議助理模組只依賴此介面，不直接參照 PMS。
/// 實作必須沿用 PMS 的專案權限與排程規則（只有專案 PM／Tech Lead 可建立任務、專案須進行中），並以待辦識別碼做冪等。
/// PMS 未安裝時由 <see cref="NullMeetingTaskWriteback"/> 接管，每一筆都回報失敗原因。
/// </summary>
public interface IMeetingTaskWriteback
{
    bool IsAvailable { get; }

    /// <summary>為每筆待辦建立（或找回已建立的）專案任務；回傳與請求等長、順序相同的結果。</summary>
    Task<IReadOnlyList<MeetingTaskResult>> CreateTasksAsync(MeetingTaskRequest request, CancellationToken cancellationToken = default);
}

/// <summary>未載入 SGS.Modules.PMS 時的預設實作：不可回寫。</summary>
public sealed class NullMeetingTaskWriteback : IMeetingTaskWriteback
{
    public bool IsAvailable => false;

    public Task<IReadOnlyList<MeetingTaskResult>> CreateTasksAsync(MeetingTaskRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MeetingTaskResult>>(
            request.Items.Select(i => new MeetingTaskResult(i.ActionItemId, null, false, "專案管理模組（PMS）未安裝，無法回寫待辦。")).ToList());
}
