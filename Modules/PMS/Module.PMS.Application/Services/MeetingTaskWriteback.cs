using Microsoft.EntityFrameworkCore;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGSFramework.Core.ProjectContracts;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// Core <see cref="IMeetingTaskWriteback"/> 的 PMS 實作（會議助理 MTG → 專案 WBS 任務）。
/// <para>
/// 一律經由 <see cref="GanttService.CreateTaskAsync"/> 建立，所以沿用甘特圖的全部規則：只有專案 PM 或 Tech Lead 可建立、專案須進行中、
/// 非成員一律當作專案不存在（防 IDOR）、任務數與階層上限。所有回寫的任務放在專案頂層的「會議待辦」群組之下。
/// </para>
/// <para>
/// 冪等：每個任務的說明帶 <c>[MTG:待辦識別碼]</c> 標記，重送時先找標記，找到就回傳既有任務而不重複建立
/// （即使 MTG 端建立後沒來得及記錄，也不會產生重複任務）。
/// 負責人不會自動指派：PMS 的專案成員只有使用者識別碼，伺服器端沒有可靠的「姓名 → 使用者」對應；
/// 紀要裡的負責人姓名寫在任務說明，由 PM 在甘特圖上指派。
/// </para>
/// </summary>
public sealed class MeetingTaskWriteback(GanttService gantt, PMSDbContext db) : IMeetingTaskWriteback
{
    public const string GroupName = "會議待辦";
    private const int DefaultDays = 7;

    public bool IsAvailable => true;

    public static string Marker(Guid actionItemId) => $"[MTG:{actionItemId:N}]";

    public async Task<IReadOnlyList<MeetingTaskResult>> CreateTasksAsync(MeetingTaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Items.Count == 0) return [];

        var results = new Dictionary<Guid, MeetingTaskResult>();

        // 先找出已回寫過的（冪等）；用標記比對而不是依賴 MTG 的紀錄
        var markers = request.Items.ToDictionary(i => i.ActionItemId, i => Marker(i.ActionItemId));
        var existing = await db.ProjectTasks.AsNoTracking()
            .Where(t => t.ProjectId == request.ProjectId && !t.IsDeleted && t.Description != null && t.Description.Contains("[MTG:"))
            .Select(t => new { t.Id, t.Description })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in request.Items)
        {
            var found = existing.FirstOrDefault(t => t.Description!.Contains(markers[item.ActionItemId], StringComparison.Ordinal));
            if (found is not null) results[item.ActionItemId] = new MeetingTaskResult(item.ActionItemId, found.Id, true, null);
        }

        var pending = request.Items.Where(i => !results.ContainsKey(i.ActionItemId)).ToList();
        if (pending.Count > 0)
        {
            var group = await EnsureGroupAsync(request, cancellationToken).ConfigureAwait(false);
            if (group.Error is { } groupError)
            {
                foreach (var item in pending) results[item.ActionItemId] = new MeetingTaskResult(item.ActionItemId, null, false, groupError);
            }
            else
            {
                foreach (var item in pending)
                    results[item.ActionItemId] = await CreateOneAsync(request, group.GroupId!.Value, item, cancellationToken).ConfigureAwait(false);
            }
        }

        return request.Items.Select(i => results[i.ActionItemId]).ToList();
    }

    private async Task<(Guid? GroupId, string? Error)> EnsureGroupAsync(MeetingTaskRequest request, CancellationToken ct)
    {
        var groupId = await db.ProjectTasks.AsNoTracking()
            .Where(t => t.ProjectId == request.ProjectId && !t.IsDeleted && t.ParentId == null && t.Name == GroupName)
            .Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (groupId is not null) return (groupId, null);

        var created = await gantt.CreateTaskAsync(request.UserId, request.Actor, request.ProjectId,
            new CreateGanttTaskRequest(GroupName, "由會議助理（MTG）回寫的會議待辦。各任務的說明記載來源會議與紀要中的負責人。", null,
                request.MeetingDate, request.MeetingDate.AddDays(DefaultDays), false, null, null), ct).ConfigureAwait(false);

        return created.IsSuccess ? (created.Value!.TaskId, null) : (null, created.Error!.Detail);
    }

    private async Task<MeetingTaskResult> CreateOneAsync(MeetingTaskRequest request, Guid groupId, MeetingTaskItem item, CancellationToken ct)
    {
        var (start, end) = Schedule(request.MeetingDate, item.DueDate);
        var name = item.Description.Trim();
        if (name.Length > 200) name = name[..199] + "…";

        var created = await gantt.CreateTaskAsync(request.UserId, request.Actor, request.ProjectId,
            new CreateGanttTaskRequest(name, BuildDescription(request, item), groupId, start, end, false, null, null), ct).ConfigureAwait(false);

        return created.IsSuccess
            ? new MeetingTaskResult(item.ActionItemId, created.Value!.TaskId, false, null)
            : new MeetingTaskResult(item.ActionItemId, null, false, created.Error!.Detail);
    }

    /// <summary>起日＝會議日；結束日＝待辦期限，沒有期限就是會議日起 7 天。期限早於會議日時整個任務落在期限當天；期間超過 PMS 上限時只保留期限當天。</summary>
    public static (DateOnly Start, DateOnly End) Schedule(DateOnly meetingDate, DateOnly? due)
    {
        var end = due ?? meetingDate.AddDays(DefaultDays);
        var start = meetingDate <= end ? meetingDate : end;
        if (end.DayNumber - start.DayNumber + 1 > GanttRules.MaxSpanDays) start = end;
        return (start, end);
    }

    public static string BuildDescription(MeetingTaskRequest request, MeetingTaskItem item)
    {
        var lines = new List<string> { item.Description.Trim() };
        lines.Add($"來源會議：{request.MeetingTitle}（{request.MeetingDate:yyyy-MM-dd}）");
        if (!string.IsNullOrWhiteSpace(item.Owner)) lines.Add($"紀要中的負責人：{item.Owner} （尚未指派，請在甘特圖上指派專案成員）");
        if (!string.IsNullOrWhiteSpace(item.Note)) lines.Add($"備註：{item.Note}");
        lines.Add(Marker(item.ActionItemId));

        var text = string.Join('\n', lines);
        if (text.Length <= 1000) return text;

        // 任務說明上限 1000 字：標記必須保留（冪等依賴它），所以截斷前面的內容
        var marker = Marker(item.ActionItemId);
        return text[..(1000 - marker.Length - 2)] + "…\n" + marker;
    }
}
