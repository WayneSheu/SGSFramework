using Microsoft.EntityFrameworkCore;
using SGS.Modules.CAL.Application.Contracts;
using SGS.Modules.CAL.Application.Rules;
using SGS.Modules.CAL.Infrastructure.Dbcontexts;
using SGS.Modules.CAL.Infrastructure.Entities;

namespace SGS.Modules.CAL.Application.Services;

/// <summary>
/// 日曆檢視與活動維護（含與會者與提醒設定）。第一期只有全公司日曆：任何具檢視權限者可讀；活動的新增、修改、刪除由控制器的
/// CAL.EVENT.MANAGE 把關（全公司日曆僅開放特定管理員編輯，一般員工唯讀）。部門與專案日曆的成員授權於第二期加入。
/// </summary>
public sealed class CalendarService(CALDbContext db, TimeProvider clock)
{
    /// <summary>某段期間的月曆資料：可見日曆、期間內活動與假日／補班日。</summary>
    public async Task<CalResult<CalendarViewDto>> ViewAsync(CalendarViewRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invalid = CalendarEventRules.ValidateViewRange(request.From, request.To);
        if (invalid is not null) return CalError.Validation(invalid);

        var calendars = await db.Calendars.AsNoTracking().Where(c => !c.IsArchived && c.Type == CalendarType.Company).OrderBy(c => c.Name)
            .Select(c => new CalendarDto(c.Id, c.Code, c.Name, c.Type, c.Color, c.IsArchived)).ToListAsync(ct).ConfigureAwait(false);
        var visibleIds = calendars.Select(c => c.Id).ToList();
        if (request.CalendarIds is { Count: > 0 }) visibleIds = visibleIds.Intersect(request.CalendarIds).ToList();

        var (start, endExclusive) = CalendarEventRules.ToRange(request.From, request.To);
        var events = await db.CalendarEvents.AsNoTracking().Include(e => e.Attendees).Include(e => e.Reminders)
            .Where(e => visibleIds.Contains(e.CalendarId) && e.StartAt < endExclusive && e.EndAt > start)
            .OrderBy(e => e.StartAt).ThenBy(e => e.Title)
            .AsSplitQuery()
            .ToListAsync(ct).ConfigureAwait(false);

        var holidayList = await db.Holidays.AsNoTracking().Where(h => h.HolidayDate >= request.From && h.HolidayDate <= request.To)
            .OrderBy(h => h.HolidayDate).Select(h => new HolidayDto(h.HolidayDate, h.Name, h.IsWorkday, h.Source)).ToListAsync(ct).ConfigureAwait(false);

        return CalResult<CalendarViewDto>.Success(new CalendarViewDto(calendars, events.Select(ToDto).ToList(), holidayList));
    }

    public async Task<CalResult<CalendarEventDto>> CreateAsync(Guid userId, string actor, UpsertCalendarEventRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var checkedCalendar = await CheckAsync(request, ct).ConfigureAwait(false);
        if (checkedCalendar is not null) return checkedCalendar;

        var now = clock.GetUtcNow();
        var entity = new CalendarEvent { Id = Guid.NewGuid(), CreatedBy = actor, CreatedByUserId = userId == Guid.Empty ? null : userId, CreatedAtUtc = now };
        Apply(entity, request);
        db.CalendarEvents.Add(entity);
        ReplaceAttendees(entity, request.AttendeeUserIds ?? []);
        ReplaceReminders(entity, request.ReminderMinutes ?? [], resetAll: true);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return CalResult<CalendarEventDto>.Success(ToDto(entity));
    }

    public async Task<CalResult<CalendarEventDto>> UpdateAsync(string actor, Guid id, UpsertCalendarEventRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await db.CalendarEvents.Include(e => e.Attendees).Include(e => e.Reminders).AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false);
        if (entity is null) return CalError.NotFound("活動不存在。");
        if (entity.ExternalSource is not null) return CalError.Conflict("CAL.EXTERNAL_READONLY", $"此活動由「{entity.ExternalSource}」同步，請在來源系統修改。");

        var checkedCalendar = await CheckAsync(request, ct).ConfigureAwait(false);
        if (checkedCalendar is not null) return checkedCalendar;

        var rescheduled = EventReminderRules.ShouldResetOnReschedule(entity.StartAt, request.StartAt);
        Apply(entity, request);
        entity.UpdatedBy = actor;
        entity.UpdatedAtUtc = clock.GetUtcNow();

        // 與會者與提醒：未提供（null）代表不變；提供則整組取代。活動改期時，保留的提醒重設為「尚未寄出」，才會依新時間再提醒一次
        if (request.AttendeeUserIds is not null) ReplaceAttendees(entity, request.AttendeeUserIds);
        if (request.ReminderMinutes is not null) ReplaceReminders(entity, request.ReminderMinutes, resetAll: rescheduled);
        else if (rescheduled) foreach (var reminder in entity.Reminders) reminder.SentAtUtc = null;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return CalResult<CalendarEventDto>.Success(ToDto(entity));
    }

    public async Task<CalResult<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false);
        if (entity is null) return CalError.NotFound("活動不存在。");
        if (entity.ExternalSource is not null) return CalError.Conflict("CAL.EXTERNAL_READONLY", $"此活動由「{entity.ExternalSource}」同步，請在來源系統刪除。");

        db.CalendarEvents.Remove(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return CalResult<bool>.Success(true);
    }

    /// <summary>驗證活動欄位、與會者、提醒與目標日曆（第一期只允許全公司日曆，且不得為已封存）；通過回傳 null。</summary>
    private async Task<CalError?> CheckAsync(UpsertCalendarEventRequest request, CancellationToken ct)
    {
        var invalid = CalendarEventRules.Validate(request)
                      ?? EventReminderRules.ValidateAttendees(request.AttendeeUserIds)
                      ?? EventReminderRules.ValidateReminders(request.ReminderMinutes);
        if (invalid is not null) return CalError.Validation(invalid);

        var calendar = await db.Calendars.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CalendarId, ct).ConfigureAwait(false);
        if (calendar is null) return CalError.NotFound("日曆不存在。");
        if (calendar.IsArchived) return CalError.Conflict("CAL.CALENDAR_ARCHIVED", "日曆已封存，不能新增或修改活動。");
        if (calendar.Type != CalendarType.Company) return CalError.Conflict("CAL.CALENDAR_NOT_SUPPORTED", "目前只開放維護全公司日曆。");
        return null;
    }

    private static void Apply(CalendarEvent entity, UpsertCalendarEventRequest request)
    {
        entity.CalendarId = request.CalendarId;
        entity.Title = request.Title.Trim();
        entity.Kind = request.Kind;
        entity.AllDay = request.AllDay;
        entity.StartAt = request.StartAt;
        entity.EndAt = request.EndAt;
        entity.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
    }

    private void ReplaceAttendees(CalendarEvent entity, IReadOnlyList<Guid> userIds)
    {
        var wanted = userIds.Distinct().ToHashSet();
        foreach (var gone in entity.Attendees.Where(a => !wanted.Contains(a.UserId)).ToList())
        {
            entity.Attendees.Remove(gone);
            db.CalendarEventAttendees.Remove(gone);
        }

        foreach (var userId in wanted.Where(u => entity.Attendees.All(a => a.UserId != u)))
        {
            var attendee = new CalendarEventAttendee { Id = Guid.NewGuid(), EventId = entity.Id, UserId = userId };
            entity.Attendees.Add(attendee);
            db.CalendarEventAttendees.Add(attendee);
        }
    }

    /// <summary>整組取代提醒：移除不在清單的、新增缺少的；保留者依 <paramref name="resetAll"/> 決定是否重設為尚未寄出。</summary>
    private void ReplaceReminders(CalendarEvent entity, IReadOnlyList<int> minutes, bool resetAll)
    {
        var wanted = minutes.Distinct().ToHashSet();
        foreach (var gone in entity.Reminders.Where(r => !wanted.Contains(r.MinutesBefore)).ToList())
        {
            entity.Reminders.Remove(gone);
            db.CalendarEventReminders.Remove(gone);
        }

        foreach (var kept in entity.Reminders)
        {
            if (resetAll) kept.SentAtUtc = null;
        }

        foreach (var m in wanted.Where(m => entity.Reminders.All(r => r.MinutesBefore != m)))
        {
            var reminder = new CalendarEventReminder { Id = Guid.NewGuid(), EventId = entity.Id, MinutesBefore = m };
            entity.Reminders.Add(reminder);
            db.CalendarEventReminders.Add(reminder);
        }
    }

    private static CalendarEventDto ToDto(CalendarEvent e) =>
        new(e.Id, e.CalendarId, e.Title, e.Kind, e.AllDay, e.StartAt, e.EndAt, e.Location, e.Description, e.Category, e.CreatedBy,
            CalendarEventRules.IsNamingConventional(e.Title), e.ExternalSource is not null,
            e.Attendees.Select(a => a.UserId).OrderBy(u => u).ToList(), e.Reminders.Select(r => r.MinutesBefore).OrderBy(m => m).ToList());
}
