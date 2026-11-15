using System.Text.Json.Serialization;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

/// <summary>附件一交付項目的甲方簽認進度（依最近一次送審回合判定）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SignoffState
{
    /// <summary>此項目不需甲方簽認。</summary>
    NotRequired = 0,

    /// <summary>需簽認，尚未送審。</summary>
    NotSubmitted = 1,

    /// <summary>已送審，等待甲方回覆。</summary>
    InReview = 2,

    /// <summary>已送審，超過甲方回覆期限仍未回覆。</summary>
    Overdue = 3,

    /// <summary>甲方提出修改意見，待修正後再送審。</summary>
    RevisionRequested = 4,

    /// <summary>甲方簽認通過。</summary>
    Approved = 5,

    /// <summary>逾期未回覆，依合約擬制通過。</summary>
    DeemedApproved = 6
}

/// <summary>附件一甲方簽認：進度判定與代登錄（簽認方式）驗證。</summary>
public static class ContractSignoffRules
{
    public static readonly IReadOnlyList<string> Channels = ["書面簽回", "Email", "會議簽認", "線上簽核", "其他"];

    public static SignoffState StateOf(bool requiresSignoff, ContractReviewRound? latest, DateOnly today)
    {
        if (!requiresSignoff) return SignoffState.NotRequired;
        if (latest is null) return SignoffState.NotSubmitted;

        return latest.Outcome switch
        {
            ReviewOutcome.Approved => SignoffState.Approved,
            ReviewOutcome.DeemedApproved => SignoffState.DeemedApproved,
            ReviewOutcome.RevisionRequested => SignoffState.RevisionRequested,
            _ => latest.ResponseDue is { } due && today > due ? SignoffState.Overdue : SignoffState.InReview
        };
    }

    public static string Describe(SignoffState state) => state switch
    {
        SignoffState.NotRequired => "不需簽認",
        SignoffState.NotSubmitted => "待送審",
        SignoffState.InReview => "審查中",
        SignoffState.Overdue => "回覆逾期",
        SignoffState.RevisionRequested => "待修正再送",
        SignoffState.Approved => "已簽認",
        _ => "擬制通過"
    };

    /// <summary>需簽認的文件在簽認完成（簽認通過或擬制通過）前交付，屬提醒事項（不阻擋）。</summary>
    public static bool DeliveredBeforeSignoff(SignoffState state) =>
        state is SignoffState.NotSubmitted or SignoffState.InReview or SignoffState.Overdue or SignoffState.RevisionRequested;

    /// <summary>代甲方登錄回覆須註明簽認方式（書面簽回、Email、會議簽認等），作為佐證。</summary>
    public static string? ValidateChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return "請選擇甲方簽認（回覆）的方式，例如書面簽回、Email、會議簽認。";
        if (channel.Trim().Length > 50) return "簽認方式不得超過 50 字。";
        return null;
    }
}

/// <summary>附件一交付項目的交付狀態（依指定交付日與實際交付日判定）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeliverableState
{
    /// <summary>未約定指定交付日。</summary>
    NoDate = 0,

    /// <summary>尚未到期，進度正常。</summary>
    OnTrack = 1,

    /// <summary>未交付，距指定交付日不足 3 個工作日（含當日）。</summary>
    DueSoon = 2,

    /// <summary>未交付，已超過指定交付日。</summary>
    Overdue = 3,

    /// <summary>已交付（準時，或未約定交付日）。</summary>
    Delivered = 4,

    /// <summary>已交付，但晚於指定交付日。</summary>
    DeliveredLate = 5
}

/// <summary>附件一交付項目：指定交付日、實際交付記錄與到期提醒的規則（純函式）。</summary>
public static class ContractDeliverableRules
{
    public const int WarnWorkdays = 3;

    public static DeliverableState StateOf(DateOnly? deliveryDate, DateOnly? deliveredDate, DateOnly today, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        if (deliveryDate is not { } due) return deliveredDate is not null ? DeliverableState.Delivered : DeliverableState.NoDate;
        if (deliveredDate is { } done) return done <= due ? DeliverableState.Delivered : DeliverableState.DeliveredLate;
        if (today > due) return DeliverableState.Overdue;
        return ContractFulfillmentRules.WorkdaysBetween(today, due, calendar) <= WarnWorkdays ? DeliverableState.DueSoon : DeliverableState.OnTrack;
    }

    /// <summary>記錄實際交付日：不得晚於今天、不得早於合約起日；空值代表取消交付記錄。</summary>
    public static string? ValidateDelivered(DateOnly? deliveredDate, string? note, DateOnly today, DateOnly? contractStart, string? channel = null)
    {
        if (note is { Length: > 200 }) return "交付備註不得超過 200 字。";
        if (channel is { Length: > 50 }) return "交付方式不得超過 50 字。";
        if (deliveredDate is not { } date) return null;
        if (string.IsNullOrWhiteSpace(channel)) return "請選擇交付方式（Email、系統上線、紙本面交、雲端連結等）。";
        if (date.Year < 2000) return "交付日不正確。";
        if (date > today) return "交付日不得晚於今天。";
        if (contractStart is { } start && date < start) return $"交付日不得早於合約起日 {start:yyyy-MM-dd}。";
        return null;
    }

    /// <summary>指定交付日（簽約後協議調整）：年份合理、不得早於合約起日；空值代表取消約定的交付日。</summary>
    public static string? ValidateDeliveryDate(string itemName, DateOnly? date, DateOnly? contractStart)
    {
        if (date is not { } d) return null;
        if (d.Year is < 2000 or > 2100) return $"「{itemName}」的指定交付日不正確。";
        if (contractStart is { } start && d < start) return $"「{itemName}」的指定交付日不得早於合約起日 {start:yyyy-MM-dd}。";
        return null;
    }

    /// <summary>逾期的工作日數說明：跨週末或假日時工作日可能為 0，僅在大於 0 時附註。</summary>
    private static string OverdueWorkdays(DateOnly dueDate, DateOnly today, WorkCalendar calendar)
    {
        var workdays = ContractFulfillmentRules.WorkdaysBetween(dueDate, today, calendar);
        return workdays > 0 ? $"，含 {workdays} 個工作日" : string.Empty;
    }

    public static string ReminderMessage(string name, DateOnly dueDate, DeliverableState state, DateOnly today, WorkCalendar calendar) => state switch
    {
        DeliverableState.Overdue =>
            $"交付項目「{name}」已逾指定交付日 {dueDate:yyyy-MM-dd}（逾 {today.DayNumber - dueDate.DayNumber} 天{OverdueWorkdays(dueDate, today, calendar)}）仍未記錄交付，請盡速交付並在「附件一」記錄，或與甲方協議調整交付日。",
        _ =>
            $"交付項目「{name}」將於 {dueDate:yyyy-MM-dd} 到期（剩 {ContractFulfillmentRules.WorkdaysBetween(today, dueDate, calendar)} 個工作日），尚未記錄交付。"
    };
}

/// <summary>加班時段的類別：標準服務時間內不計加班。</summary>
public enum OvertimeKind
{
    Standard = 0,
    Weekday = 1,
    NightRest = 2,
    Holiday = 3
}

/// <summary>加班時段分析的一個區段：<see cref="Multiplier"/> 為 1.0 且 <see cref="Kind"/> 為 Standard 代表標準服務時間內（不計加班）。</summary>
public sealed record OvertimeSegment(OvertimeKind Kind, decimal Multiplier, decimal Hours);

/// <summary>加班時段分析結果。</summary>
public sealed record OvertimeBreakdown(decimal TotalHours, decimal OvertimeHours, decimal WeightedHours, IReadOnlyList<OvertimeSegment> Segments);

/// <summary>
/// 非服務時間支援的合約參數：提前申請工作日、單次窗口上限與三種加班倍率。倍率隨法規或合約約定調整，故由合約參數化，預設取規範值。
/// </summary>
public sealed record OvertimeTerms(int AdvanceWorkdays, int MaxWindowDays, decimal WeekdayRate, decimal NightRestRate, decimal HolidayRate, bool RequiresApproval = true)
{
    public static OvertimeTerms Default { get; } = new(5, 7, 1.5m, 2.0m, 2.5m);

    public const int MaxAdvanceWorkdays = 30;
    public const int MaxWindowDaysLimit = 31;
    public const decimal MinRate = 1.0m;
    public const decimal MaxRate = 5.0m;

    public decimal RateOf(OvertimeKind kind) => kind switch
    {
        OvertimeKind.Weekday => WeekdayRate,
        OvertimeKind.NightRest => NightRestRate,
        OvertimeKind.Holiday => HolidayRate,
        _ => 1.0m
    };

    /// <summary>參數驗證：提前 0~30 個工作日、窗口 1~31 天、倍率 1.0~5.0（至多兩位小數）、倍率不得隨類別反向（平日 ≤ 深夜與例假日 ≤ 國定假日）。</summary>
    public string? Validate()
    {
        if (AdvanceWorkdays is < 0 or > MaxAdvanceWorkdays) return $"提前申請工作日須為 0~{MaxAdvanceWorkdays}。";
        if (MaxWindowDays is < 1 or > MaxWindowDaysLimit) return $"單次支援窗口上限須為 1~{MaxWindowDaysLimit} 天。";

        foreach (var rate in new[] { WeekdayRate, NightRestRate, HolidayRate })
        {
            if (rate is < MinRate or > MaxRate) return $"加班倍率須介於 {MinRate:0.0} 與 {MaxRate:0.0} 之間。";
            if (decimal.Round(rate, 2) != rate) return "加班倍率最多兩位小數。";
        }

        if (WeekdayRate > NightRestRate || NightRestRate > HolidayRate) return "加班倍率須符合：平日加班 ≤ 深夜與例假日 ≤ 國定假日。";
        return null;
    }
}

/// <summary>
/// 非服務時間支援與加班計費規則（規範第二章 階段四）：標準服務時間為週一至週五 09:00–18:00（不含國定假日）；
/// 非服務時間須提前申請，並依平日加班、深夜（22:00–06:00）與例假日、國定假日三種倍率計費（提前日數、窗口上限與倍率皆為合約參數，預設 5 日、7 天、1.5／2.0／2.5）。
/// 時間以台灣時間（UTC+8）判定，15 分鐘為最小單位。
/// </summary>
public static class OvertimeRules
{
    public const int SliceMinutes = 15;

    private static readonly TimeOnly StandardStart = new(9, 0);
    private static readonly TimeOnly StandardEnd = new(18, 0);
    private static readonly TimeOnly NightStart = new(22, 0);
    private static readonly TimeOnly NightEnd = new(6, 0);

    /// <summary>某個台灣時間點所屬的時段類別。</summary>
    public static OvertimeKind KindOf(DateTime local, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var date = DateOnly.FromDateTime(local);
        if (calendar.IsHoliday(date)) return OvertimeKind.Holiday;
        if (!calendar.IsWorkday(date)) return OvertimeKind.NightRest;

        var time = TimeOnly.FromDateTime(local);
        if (time >= StandardStart && time < StandardEnd) return OvertimeKind.Standard;
        if (time >= NightStart || time < NightEnd) return OvertimeKind.NightRest;
        return OvertimeKind.Weekday;
    }

    /// <summary>某個台灣時間點適用的倍率：1.0＝標準服務時間內；其餘為合約參數的加班倍率。</summary>
    public static decimal Multiplier(DateTime local, WorkCalendar calendar, OvertimeTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        return terms.RateOf(KindOf(local, calendar));
    }

    /// <summary>分析 [start, end) 區間（含時區）的各類別時數；無法被 15 分鐘整除的尾數不計。</summary>
    public static OvertimeBreakdown Analyze(DateTimeOffset start, DateTimeOffset end, WorkCalendar calendar, OvertimeTerms terms)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(terms);

        var totals = new SortedDictionary<OvertimeKind, decimal>();
        var slice = TimeSpan.FromMinutes(SliceMinutes);
        var cursor = start.ToOffset(SlaCalculator.Offset).DateTime;
        var last = end.ToOffset(SlaCalculator.Offset).DateTime;

        while (cursor + slice <= last)
        {
            var kind = KindOf(cursor, calendar);
            totals[kind] = totals.GetValueOrDefault(kind) + SliceMinutes / 60m;
            cursor += slice;
        }

        var segments = totals.Select(kv => new OvertimeSegment(kv.Key, terms.RateOf(kv.Key), kv.Value)).ToList();
        var overtime = segments.Where(s => s.Kind != OvertimeKind.Standard).ToList();
        return new OvertimeBreakdown(
            segments.Sum(s => s.Hours), overtime.Sum(s => s.Hours), overtime.Sum(s => s.Hours * s.Multiplier), segments);
    }

    /// <summary>
    /// 申請驗證：事由必填、窗口合理（15 分鐘為單位、不超過合約的窗口上限）、須提前合約約定的工作日數（以申請日與支援起日的工作日數計）。
    /// </summary>
    public static string? ValidateRequest(string? title, DateTimeOffset start, DateTimeOffset end, DateOnly today, WorkCalendar calendar, string? note, OvertimeTerms terms)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(terms);

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) return "支援事由必填且不得超過 200 字。";
        if (note is { Length: > 1000 }) return "備註不得超過 1000 字。";

        var s = start.ToOffset(SlaCalculator.Offset);
        var e = end.ToOffset(SlaCalculator.Offset);
        if (e <= s) return "支援結束時間須晚於開始時間。";
        if (e - s > TimeSpan.FromDays(terms.MaxWindowDays)) return $"單次支援窗口不得超過 {terms.MaxWindowDays} 天（合約約定）。";
        if (s.Minute % SliceMinutes != 0 || e.Minute % SliceMinutes != 0 || s.Second != 0 || e.Second != 0)
            return $"起訖時間請以 {SliceMinutes} 分鐘為單位。";

        var startDate = DateOnly.FromDateTime(s.DateTime);
        var earliest = calendar.AddWorkdays(today, terms.AdvanceWorkdays);
        if (startDate < earliest)
            return $"非服務時間支援須提前 {terms.AdvanceWorkdays} 個工作日申請（最早可支援日期為 {earliest:yyyy-MM-dd}）。";

        var breakdown = Analyze(start, end, calendar, terms);
        if (breakdown.OvertimeHours <= 0) return "這段時間全部落在標準服務時間內，不需要申請非服務時間支援。";
        return null;
    }

    public const int MaxClientChannelLength = 50;
    public const int MaxClientRefLength = 500;
    public const int MaxClientRequestAgeDays = 365;

    /// <summary>
    /// 甲方提出需求的佐證：提出日期（不得晚於今天、不得晚於支援開始日、不早於 365 天前）、提出方式必填，佐證說明選填。
    /// 規範要求甲方須提前申請，此欄位留下「確實由甲方提出」的紀錄。
    /// </summary>
    public static string? ValidateClientRequest(DateOnly? requestedDate, string? channel, string? reference, DateOnly today, DateOnly supportStartDate)
    {
        if (requestedDate is not { } date) return "請填寫甲方提出需求的日期。";
        if (date > today) return "甲方提出日期不得晚於今天。";
        if (date > supportStartDate) return "甲方提出日期不得晚於支援開始日。";
        if (date < today.AddDays(-MaxClientRequestAgeDays)) return $"甲方提出日期不得早於 {MaxClientRequestAgeDays} 天前。";
        if (string.IsNullOrWhiteSpace(channel)) return "請選擇甲方提出的方式（Email、電話、會議等）。";
        if (channel.Trim().Length > MaxClientChannelLength) return $"提出方式不得超過 {MaxClientChannelLength} 字。";
        if (reference is { Length: > MaxClientRefLength }) return $"佐證說明不得超過 {MaxClientRefLength} 字。";
        return null;
    }

    public static string? ValidateDecision(SupportStatus status, bool approve, string? note, Guid userId, Guid requestedBy, bool isExecutive)
    {
        if (status != SupportStatus.Pending) return "這張申請已處理完畢。";
        if (!isExecutive) return "僅合約的核決主管可核准或駁回非服務時間支援。";
        if (userId == requestedBy) return "不得核准自己提出的支援申請（職責分離）。";
        if (!approve && string.IsNullOrWhiteSpace(note)) return "駁回請填寫原因。";
        if (note is { Length: > 1000 }) return "意見不得超過 1000 字。";
        return null;
    }

    public static string? ValidateComplete(SupportStatus status, DateTimeOffset actualStart, DateTimeOffset actualEnd, DateTimeOffset now, string? note, OvertimeTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        if (status != SupportStatus.Approved) return "核准後才能記錄實際支援。";
        if (actualEnd <= actualStart) return "實際結束時間須晚於開始時間。";
        if (actualEnd > now) return "實際結束時間不得晚於現在。";
        if (actualEnd - actualStart > TimeSpan.FromDays(terms.MaxWindowDays)) return $"實際支援時間不得超過 {terms.MaxWindowDays} 天（合約約定）。";
        if (actualStart.ToOffset(SlaCalculator.Offset).Minute % SliceMinutes != 0 || actualEnd.ToOffset(SlaCalculator.Offset).Minute % SliceMinutes != 0)
            return $"起訖時間請以 {SliceMinutes} 分鐘為單位。";
        if (note is { Length: > 1000 }) return "備註不得超過 1000 字。";
        return null;
    }

    public static string? ValidateCancel(SupportStatus status) =>
        status is SupportStatus.Pending or SupportStatus.Approved ? null : "只有待核准或已核准（尚未執行）的申請可以取消。";

    public static string KindName(OvertimeKind kind, decimal rate) => kind switch
    {
        OvertimeKind.Standard => "標準服務時間內",
        OvertimeKind.Weekday => $"平日加班 {rate:0.##} 倍",
        OvertimeKind.NightRest => $"深夜／例假日 {rate:0.##} 倍",
        _ => $"國定假日 {rate:0.##} 倍"
    };
}

/// <summary>保固規則：保固期自驗收完成日起算、現行保固月數（含合約變更延長）；到期前 60／30／7 天與到期後各提醒一次。</summary>
public static class ContractWarrantyRules
{
    public const int Stage60 = 1;
    public const int Stage30 = 2;
    public const int Stage7 = 3;
    public const int StageExpired = 4;

    public static DateOnly? WarrantyEnd(DateOnly? acceptedDate, int months) =>
        acceptedDate is { } d && months > 0 ? d.AddMonths(months) : null;

    /// <summary>距保固到期的天數（含當日）；今天已超過到期日為負數。</summary>
    public static int DaysLeft(DateOnly warrantyEnd, DateOnly today) => warrantyEnd.DayNumber - today.DayNumber;

    /// <summary>依剩餘天數應處於的提醒階段：&gt;60 天為 0；≤60→1；≤30→2；≤7→3；已過期→4。</summary>
    public static int StageFor(int daysLeft) => daysLeft switch
    {
        < 0 => StageExpired,
        <= 7 => Stage7,
        <= 30 => Stage30,
        <= 60 => Stage60,
        _ => 0
    };

    /// <summary>是否需要提醒：目前階段高於已提醒階段（每個階段只提醒一次）。</summary>
    public static bool ShouldRemind(int currentStage, int remindedStage) => currentStage > remindedStage;

    public static string Message(int stage, DateOnly end, int daysLeft) => stage switch
    {
        StageExpired => $"保固已於 {end:yyyy-MM-dd} 到期（逾 {-daysLeft} 天）。請確認是否結案，或與客戶討論維運合約續約；到期後的修復屬保固除外，須另行報價。",
        Stage7 => $"保固將於 {end:yyyy-MM-dd} 到期（剩 {daysLeft} 天）。請確認是否有未完成的保固修復，並提醒客戶評估維運合約。",
        Stage30 => $"保固將於 {end:yyyy-MM-dd} 到期（剩 {daysLeft} 天）。建議開始與客戶討論維運合約續約。",
        _ => $"保固將於 {end:yyyy-MM-dd} 到期（剩 {daysLeft} 天）。"
    };

    /// <summary>保固除外條款（規範第二章 階段四）：不在免費保固範圍內，須另行報價。</summary>
    public static IReadOnlyList<string> Exclusions { get; } =
    [
        "人為操作不當所造成的問題",
        "甲方環境變更（主機、網路、資料庫、作業系統等）所造成的問題",
        "第三方 API 或外部服務改版所造成的問題",
        "新增功能或規格外的需求（須走變更程序另行報價）"
    ];
}

/// <summary>合約附件二 SLA 分級覆寫：把問題單優先級 P1／P2／P3 對應到合約 Level 1／2／3，並以其處理期限取代系統預設 SLA。</summary>
public static class ContractSlaRules
{
    /// <summary>問題單優先級對應的合約 SLA 等級（P1→1、P2→2、P3→3）。</summary>
    public static int LevelFor(TicketPriority priority) => priority switch
    {
        TicketPriority.P1 => 1,
        TicketPriority.P2 => 2,
        _ => 3
    };

    /// <summary>
    /// 合約約定的應解決期限；等級未設定處理期限（皆空）時回傳 null，由呼叫端沿用系統預設 SLA。
    /// 時鐘小時＝受理時間加上小時；工作日＝受理當日起算第 N 個工作日、時分保持受理當下。
    /// </summary>
    public static DateTimeOffset? OverrideDue(DateTimeOffset receivedAt, decimal? dueHours, int? dueWorkdays, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        if (dueHours is { } hours and > 0) return receivedAt.AddHours((double)hours);
        if (dueWorkdays is { } days and > 0) return SlaCalculator.AddWorkdays(receivedAt, days, calendar);
        return null;
    }

    private static readonly TimeSpan ServiceStart = new(9, 0, 0);
    private static readonly TimeSpan ServiceEnd = new(18, 0, 0);

    /// <summary>
    /// 首次回應期限：自受理時間起，只累計標準服務時間（工作日 09:00–18:00，國定假日不計、補班日計）的小時數。
    /// 受理時間在服務時間外時，從下一個服務時段開始起算；以 15 分鐘為單位向上取整（合約約定的小時數不會被少算）。
    /// </summary>
    public static DateTimeOffset ResponseDue(DateTimeOffset receivedAt, decimal responseHours, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var local = receivedAt.ToOffset(SlaCalculator.Offset).DateTime;
        var remaining = TimeSpan.FromMinutes((double)Math.Ceiling(responseHours * 4m) * 15);
        var cursor = local;

        // 最多往後找一年，避免曆表異常造成無窮迴圈
        for (var guard = 0; guard < 366 && remaining > TimeSpan.Zero; guard++)
        {
            var date = DateOnly.FromDateTime(cursor);
            if (calendar.IsWorkday(date))
            {
                var windowStart = date.ToDateTime(TimeOnly.FromTimeSpan(ServiceStart));
                var windowEnd = date.ToDateTime(TimeOnly.FromTimeSpan(ServiceEnd));
                var from = cursor > windowStart ? cursor : windowStart;
                if (from < windowEnd)
                {
                    var available = windowEnd - from;
                    if (available >= remaining) return new DateTimeOffset(from + remaining, SlaCalculator.Offset);

                    remaining -= available;
                }
            }

            cursor = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        }

        return new DateTimeOffset(cursor, SlaCalculator.Offset);
    }

    /// <summary>首次回應是否逾期：已回應時間晚於期限（無期限者不判定）。</summary>
    public static bool IsResponseLate(DateTimeOffset? due, DateTimeOffset? respondedAt) =>
        due is { } d && respondedAt is { } r && r > d;

    public const int MaxAgreementChannelLength = 50;
    public const int MaxAgreementRefLength = 500;

    /// <summary>簽約後調整附件二須有「雙方協議」佐證：協議日期（不得晚於今天、不早於 365 天前）、協議方式必填，佐證說明選填。</summary>
    public static string? ValidateAgreement(DateOnly? agreedDate, string? channel, string? reference, DateOnly today)
    {
        if (agreedDate is not { } date) return "請填寫雙方協議日期。";
        if (date > today) return "協議日期不得晚於今天。";
        if (date < today.AddDays(-365)) return "協議日期不得早於 365 天前。";
        if (string.IsNullOrWhiteSpace(channel)) return "請選擇雙方協議的方式（書面補充協議、Email、會議紀錄等）。";
        if (channel.Trim().Length > MaxAgreementChannelLength) return $"協議方式不得超過 {MaxAgreementChannelLength} 字。";
        if (reference is { Length: > MaxAgreementRefLength }) return $"佐證說明不得超過 {MaxAgreementRefLength} 字。";
        return null;
    }

    /// <summary>處理期限（時鐘小時與工作日）至多設一種，且需在合理範圍。</summary>
    public static string? ValidateDue(decimal? dueHours, int? dueWorkdays)
    {
        if (dueHours is not null && dueWorkdays is not null) return "處理期限只能設定「時鐘小時」或「工作日」其中一種。";
        if (dueHours is <= 0 or > 720m) return "處理期限（小時）須大於 0 且不超過 720。";
        if (dueWorkdays is < 1 or > 90) return "處理期限（工作日）須為 1~90。";
        return null;
    }

    public static string Describe(decimal? dueHours, int? dueWorkdays) =>
        dueHours is { } h ? $"{h:0.##} 小時內" : dueWorkdays is { } d ? $"{d} 個工作日內" : "沿用系統預設";
}

/// <summary>產業合規項目的規範內容。</summary>
public sealed record ComplianceSpec(string Code, ContractIndustry Industry, string Name, string Risk, string Defense);

/// <summary>產業合規清單目錄（規範：金融、醫療、製造三大產業合規與合約避雷指南）。</summary>
public static class ContractComplianceCatalog
{
    public static IReadOnlyList<ComplianceSpec> All { get; } =
    [
        new("FIN_AUDIT", ContractIndustry.Finance, "金檢配合範圍",
            "金融機構委外須配合金管會金檢並簽署嚴格保密與資安條款，範圍可能無限擴張至乙方其他客戶資料。",
            "合約明訂乙方僅就「與本專案直接相關」之系統與文件配合主管機關查核。"),
        new("FIN_DR", ContractIndustry.Finance, "營運不中斷與 DR 演練",
            "要求極高可用率（如 99.95% 以上）與 RTO／RPO，並要求無限次、不限時段的災難復原（DR）演練支援。",
            "限定每年免費支援演練次數（例如 1 次）與時段；超出次數或非上班時間演練須另計人力費用。"),
        new("FIN_INCIDENT", ContractIndustry.Finance, "資安事件通報與連帶賠償",
            "要求 30 分鐘或 1 小時內通報，且連帶賠償金管會罰鍰。",
            "改為「發現疑似重大資安異常後 2 小時內先口頭／電話通報，確認事件性質後 24 小時內補送書面報告」，並將賠償上限鎖定在合約總價金內。"),

        new("MED_DEIDENT", ContractIndustry.Medical, "特種個資去識別化與測試資料",
            "開發與測試需要真實病歷結構，涉及個資法第 6 條特種個資（病歷、醫療、健康檢查）。",
            "合約明訂甲方提供的開發／測試資料須先去識別化或使用模擬假資料；未去識別化即交付真實病歷之個資外洩風險概由甲方承擔。"),
        new("MED_SAMD", ContractIndustry.Medical, "醫療器材軟體（SaMD）界線",
            "若系統涉及輔助診斷、疾病預測或治療建議演算法，可能被認定為醫療器材軟體而須申請醫材許可證。",
            "一般行政／流程管理系統應聲明「本系統不具備醫療診斷或治療決策功能，非屬醫療器材軟體」；確實涉及醫材認證者，查驗登記規費與臨床試驗責任歸甲方。"),
        new("MED_24X7", ContractIndustry.Medical, "24x7 維運與 SLA",
            "醫院急診與核心醫療系統要求 24 小時全年無休支援。",
            "區分一般行政系統（平日 09:00–18:00）與急診／核心醫療系統（24x7）；24x7 SLA 的維護費率提高至專案總價的 20%–25%。"),
        new("MED_VPN", ContractIndustry.Medical, "遠端 VPN 連線權限",
            "每次異常都要派工程師到院現場，夜間成本高且反應慢。",
            "合約明訂乙方的遠端 VPN 連線權限與作業規範，避免半夜派工到院。"),

        new("MFG_PROTOCOL", ContractIndustry.Manufacturing, "機台通訊協定與介面責任",
            "MES／自動化系統需讀取機台（CNC、感測器、條碼機）資料，老舊機台無通訊介面或需機台原廠解鎖。",
            "合約明訂甲方須提供機台標準通訊協定（如 OPC UA、Modbus）與開放資料讀取介面；老舊機台無介面或需聯繫原廠解鎖之費用與協調責任歸甲方。"),
        new("MFG_DOWNTIME", ContractIndustry.Manufacturing, "產線停機損失免責",
            "系統上線切換或異常導致產線停擺，動輒求償每小時數十萬的產能損失。",
            "合約絕對要排除「產線停機損失、良率下降損失、交貨遲延違約金及一切間接衍生性損害」。"),
        new("MFG_BACKUP_SOP", ContractIndustry.Manufacturing, "人工紙本備援作業程序",
            "系統異常時若無人工備援，產線完全停擺並全數歸責乙方。",
            "系統上線前要求甲方必須具備「人工紙本備援作業程序（SOP）」，並列為上線條件。"),
        new("MFG_ONSITE", ContractIndustry.Manufacturing, "駐廠支援與工安規範",
            "工程師進入無塵室或廠區須遵守嚴格工安勞安規定，且常因機台未就緒而空等。",
            "明確約定駐廠支援天數上限；因甲方機台尚未就緒導致工程師在廠區空等，該天數仍計入已使用額度。")
    ];

    public static IReadOnlyList<ComplianceSpec> For(ContractIndustry industry) => All.Where(s => s.Industry == industry).ToList();

    public static ComplianceSpec? Find(string? code) => All.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.Ordinal));

    /// <summary>送審前檢查：該產業的合規項目都已處理（已納入或不適用），不適用須填原因。</summary>
    public static IReadOnlyList<string> ValidateForSubmit(ContractIndustry industry, IEnumerable<ContractComplianceItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var list = items.ToList();
        var problems = new List<string>();
        foreach (var spec in For(industry))
        {
            var item = list.FirstOrDefault(i => i.Code == spec.Code);
            if (item is null || item.Status == ComplianceStatus.Pending)
                problems.Add($"產業合規項目「{spec.Name}」尚未確認（請標示已納入合約或不適用）。");
            else if (item.Status == ComplianceStatus.NotApplicable && string.IsNullOrWhiteSpace(item.Note))
                problems.Add($"產業合規項目「{spec.Name}」標為不適用，請填寫原因。");
        }

        return problems;
    }

    public static string? ValidateUpdate(string code, ContractIndustry industry, ComplianceStatus status, string? note)
    {
        var spec = Find(code);
        if (spec is null || spec.Industry != industry) return "合規項目不存在或不屬於此合約的產業別。";
        if (!Enum.IsDefined(status)) return "狀態不正確。";
        if (status == ComplianceStatus.NotApplicable && string.IsNullOrWhiteSpace(note)) return "標為不適用請填寫原因。";
        if (note is { Length: > 1000 }) return "說明不得超過 1000 字。";
        return null;
    }
}
