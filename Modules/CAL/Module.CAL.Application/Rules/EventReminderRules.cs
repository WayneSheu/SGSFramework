namespace SGS.Modules.CAL.Application.Rules;

/// <summary>活動提醒與與會者的驗證與時間規則（純函式）。</summary>
public static class EventReminderRules
{
    public const int MaxReminders = 5;
    public const int MinMinutes = 1;
    public const int MaxMinutes = 20160; // 14 天
    public const int MaxAttendees = 50;

    /// <summary>驗證提醒設定：最多 5 組、每組 1 分鐘～14 天、不得重複。</summary>
    public static string? ValidateReminders(IReadOnlyList<int>? minutes)
    {
        if (minutes is null) return null;
        if (minutes.Count > MaxReminders) return $"每個活動最多設定 {MaxReminders} 組提醒。";
        if (minutes.Any(m => m < MinMinutes || m > MaxMinutes)) return $"提醒時間須為活動前 {MinMinutes} 分鐘～14 天。";
        if (minutes.Distinct().Count() != minutes.Count) return "提醒時間不可重複。";
        return null;
    }

    public static string? ValidateAttendees(IReadOnlyList<Guid>? userIds)
    {
        if (userIds is null) return null;
        if (userIds.Count > MaxAttendees) return $"與會者最多 {MaxAttendees} 人。";
        if (userIds.Any(u => u == Guid.Empty)) return "與會者資料不正確。";
        return null;
    }

    /// <summary>該組提醒應寄出的時間。</summary>
    public static DateTimeOffset DueAt(DateTimeOffset eventStart, int minutesBefore) => eventStart.AddMinutes(-minutesBefore);

    /// <summary>是否該寄：尚未寄出、已到提醒時間、且活動尚未開始（開始後再提醒沒有意義）。</summary>
    public static bool ShouldSend(DateTimeOffset eventStart, int minutesBefore, DateTimeOffset? sentAt, DateTimeOffset now) =>
        sentAt is null && now >= DueAt(eventStart, minutesBefore) && now < eventStart;

    /// <summary>活動改期（開始時間改變）時，已寄出的提醒需重設，才會依新時間再提醒一次。</summary>
    public static bool ShouldResetOnReschedule(DateTimeOffset oldStart, DateTimeOffset newStart) => oldStart != newStart;

    /// <summary>「15 分鐘前」「2 小時前」「1 天前」。</summary>
    public static string Describe(int minutes) => minutes switch
    {
        _ when minutes % 1440 == 0 => $"{minutes / 1440} 天前",
        _ when minutes % 60 == 0 => $"{minutes / 60} 小時前",
        _ => $"{minutes} 分鐘前"
    };
}
