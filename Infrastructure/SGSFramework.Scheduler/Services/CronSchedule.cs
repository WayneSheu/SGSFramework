using Cronos;
using SGSFramework.Scheduler.Hosting;

namespace SGSFramework.Scheduler.Services;

/// <summary>
/// Cron 驗證與預覽：使用與 Hangfire 相同的 Cronos 解析規則（5 碼，不含秒）。
/// </summary>
public static class CronSchedule
{
    /// <summary>使用者自訂週期的最短間隔，避免誤設為每分鐘執行（備份、清除等作業負載較重）</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(5);

    /// <summary>驗證 Cron 與時區；成功時回傳接下來的執行時間（UTC）</summary>
    public static (bool Valid, string? Error, IReadOnlyList<DateTimeOffset> NextOccurrences) Validate(
        string? cron, string? timeZoneId, DateTimeOffset now, int count = 5)
    {
        if (string.IsNullOrWhiteSpace(cron)) return (false, "請輸入週期 (Cron)。", []);

        var trimmed = cron.Trim();
        if (trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length != 5)
        {
            return (false, "Cron 須為 5 碼：分 時 日 月 星期（例如 0 2 * * * 表示每天 02:00）。", []);
        }

        if (!string.IsNullOrWhiteSpace(timeZoneId) && !TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _))
        {
            return (false, $"找不到時區：{timeZoneId}", []);
        }

        CronExpression expression;
        try
        {
            expression = CronExpression.Parse(trimmed, CronFormat.Standard);
        }
        catch (CronFormatException ex)
        {
            return (false, $"Cron 格式錯誤：{ex.Message}", []);
        }

        var zone = SchedulerTimeZones.Resolve(timeZoneId);
        var occurrences = expression.GetOccurrences(now, now.AddYears(5), zone, fromInclusive: false)
            .Take(Math.Max(count, 2))
            .ToList();
        if (occurrences.Count == 0) return (false, "此週期在未來 5 年內不會執行，請確認日期設定。", []);

        for (int i = 1; i < occurrences.Count; i++)
        {
            if (occurrences[i] - occurrences[i - 1] < MinimumInterval)
            {
                return (false, $"執行間隔不可短於 {MinimumInterval.TotalMinutes:0} 分鐘。", []);
            }
        }

        return (true, null, occurrences.Take(count).ToList());
    }
}
