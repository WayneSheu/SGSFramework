using SGS.Modules.ORG.Infrastructure.Entities.Org;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Command
{
    /// <summary>
    /// 實驗室支援的共用規則：單筆指派 (AssignUserLabCommand) 與批次指派 (BatchAssignLabSupportCommand) 共用同一份檢核，
    /// 避免兩條路徑規則不一致。
    /// </summary>
    internal static class LabSupportRules
    {
        // 台灣時區定義（相容跨平台 Windows / Linux）
        private static readonly TimeZoneInfo TaiwanTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Taipei Standard Time" : "Asia/Taipei");

        /// <summary>日期（只取日期部分）於台灣時區當天 00:00 對應的 UTC 時間</summary>
        public static DateTime ToUtcStartOfDay(DateTime date)
        {
            var unspecified = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, TaiwanTimeZone);
        }

        /// <summary>日期於台灣時區當天結束（23:59:59.9999999）對應的 UTC 時間：支援期間的「迄日（含）」涵蓋整天</summary>
        public static DateTime ToUtcEndOfDay(DateTime date) => ToUtcStartOfDay(date).AddDays(1).AddTicks(-1);

        /// <summary>現在時間於台灣時區的日期</summary>
        public static DateTime TodayInTaiwan(TimeProvider clock) =>
            TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TaiwanTimeZone).Date;

        /// <summary>
        /// 檢核人員主要實驗室與支援實驗室：須有主要實驗室、不可相同、且所屬實驗室別（上層組織）須相同。
        /// 通過回傳 null；不通過回傳（錯誤碼, 訊息）。
        /// </summary>
        /// <param name="homeLabId">人員啟用中的主要實驗室 Id；無則為 null</param>
        /// <param name="targetLabId">支援實驗室 Id</param>
        /// <param name="organizations">含 Parent 的組織字典，至少包含上述兩個 Id</param>
        public static (string Code, string Message)? CheckCategory(
            int? homeLabId, int targetLabId, IReadOnlyDictionary<int, Organization> organizations)
        {
            if (homeLabId is null)
            {
                return ("USERLAB_SUPPORT_NO_PRIMARY", "該人員尚無啟用中的主要實驗室，無法指派支援。");
            }

            if (homeLabId.Value == targetLabId)
            {
                return ("USERLAB_SUPPORT_SAME_AS_PRIMARY", "支援實驗室不可與人員的主要實驗室相同。");
            }

            if (!organizations.TryGetValue(targetLabId, out var target))
            {
                return ("USERLAB_SUPPORT_LAB_NOT_FOUND", "找不到指定的支援實驗室。");
            }

            organizations.TryGetValue(homeLabId.Value, out var home);
            if (home?.ParentId is null || target.ParentId is null || home.ParentId != target.ParentId)
            {
                var homeCategory = home?.Parent?.Name ?? "（未知）";
                var targetCategory = target.Parent?.Name ?? "（未知）";
                return ("USERLAB_SUPPORT_CATEGORY_MISMATCH",
                    $"人員主要實驗室所屬實驗室別為「{homeCategory}」，與支援實驗室「{target.Name}」所屬的實驗室別「{targetCategory}」不同，無法指派支援。");
            }

            return null;
        }
    }
}
