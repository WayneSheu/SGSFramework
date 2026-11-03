// ==========================================
// 檔案路徑: src/SGSFramework.Core/Helpers/DateTimeHelper.cs
// 架構層級: Core / Helpers (.NET 10 跨平台支援)
// ==========================================

#nullable enable
using System;
using System.Diagnostics;

namespace SGSFramework.Core.Helpers;

/// <summary>
/// 跨平台時區與時間處理輔助工具 (同時相容 Windows「Taipei Standard Time」與 Linux/Docker「Asia/Taipei」)
/// </summary>
public static class DateTimeHelper
{
    private static readonly TimeZoneInfo TaiwanTimeZone = ResolveTaiwanTimeZone();

    /// <summary>
    /// 自動尋找作業系統對應的台灣時區 ID，若皆不存在則降級採用固定 +08:00 偏移量
    /// </summary>
    private static TimeZoneInfo ResolveTaiwanTimeZone()
    {
        string[] candidateIds = ["Taipei Standard Time", "Asia/Taipei"];
        foreach (var id in candidateIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // 目的作業系統環境未註冊此時區 ID，記錄偵錯訊息並安全嘗試下一個候選 ID
                Debug.WriteLine($"[DateTimeHelper] 找不到時區 ID: {id}，將嘗試下一個候選設定。");
            }
            catch (InvalidTimeZoneException)
            {
                // 時區登錄資料結構異常，記錄偵錯訊息並安全嘗試下一個候選 ID
                Debug.WriteLine($"[DateTimeHelper] 時區 ID 格式無效: {id}，將嘗試下一個候選設定。");
            }
        }

        // 降級防護：建立固定 +8 小時時區
        return TimeZoneInfo.CreateCustomTimeZone(
            "Taiwan Standard Time",
            TimeSpan.FromHours(8),
            "Taiwan Standard Time",
            "Taiwan Standard Time");
    }

    /// <summary>
    /// 取得當前台灣時區 (UTC+8) 之 DateTimeOffset
    /// </summary>
    public static DateTimeOffset GetTaiwanTime()
    {
        try
        {
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TaiwanTimeZone);
        }
        catch
        {
            return DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8));
        }
    }

    /// <summary>
    /// 將任意 DateTimeOffset 轉換為台灣時區 (UTC+8)
    /// </summary>
    public static DateTimeOffset ConvertToTaiwanTime(DateTimeOffset dateTimeOffset)
    {
        try
        {
            return TimeZoneInfo.ConvertTime(dateTimeOffset, TaiwanTimeZone);
        }
        catch
        {
            return dateTimeOffset.ToOffset(TimeSpan.FromHours(8));
        }
    }
}