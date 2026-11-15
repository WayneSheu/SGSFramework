using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

/// <summary>協力事項目前的追蹤狀態（供畫面燈號）。</summary>
public enum CoopState
{
    /// <summary>未到甲方應完成日。</summary>
    OnTrack = 0,

    /// <summary>已完成（或免除）。</summary>
    Done = 1,

    /// <summary>已過應完成日，尚在 3 個工作日通知期限內：須盡快通知。</summary>
    LateNoticeDue = 2,

    /// <summary>已逾期且通知期限已過，仍未寄出書面通知。</summary>
    LateNoticeOverdue = 3,

    /// <summary>已逾期且已寄出書面通知。</summary>
    LateNotified = 4
}

/// <summary>付款期程的檢查結果。</summary>
public readonly record struct PaymentCheck(bool Ok, string? Error);

/// <summary>
/// 專案合約履約規則（乙方標準版第二期）：協力遲延與 3 個工作日通知期限、工期展延與現行交期、文件審查回合與擬制通過、
/// 驗收與擬制驗收、付款期程與請款條件。全為純函式，工作日一律以 <see cref="WorkCalendar"/> 計算（週末與國定假日不計）。
/// </summary>
public static class ContractFulfillmentRules
{
    /// <summary>協力遲延後，乙方須發出書面通知的工作日數（規範：發生當下 3 個工作日內）。</summary>
    public const int NoticeWorkdays = 3;

    /// <summary>通知期限工作日數的上下限（可由合約或各協力事項調整）。</summary>
    public const int MaxNoticeWorkdays = 30;

    public const int MaxSubjectLength = 200;
    public const int MaxBodyLength = 3000;
    public const int MaxClosingLength = 100;
    public const int MaxSignatureLength = 200;

    public const int MaxCoopItems = 100;
    public const int MaxPaymentTerms = 12;
    public const int MaxExtensionWorkdays = 365;

    // ---------- 協力遲延與通知 ----------

    /// <summary>
    /// 累計遲延工作日：甲方應完成日之後（不含當日）到「完成日（或今天）」之間的工作日數；未逾期為 0。
    /// </summary>
    public static int DelayWorkdays(DateOnly due, DateOnly? completed, DateOnly today, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var end = completed ?? today;
        if (end <= due) return 0;
        return WorkdaysBetween(due, end, calendar);
    }

    /// <summary>通知期限：甲方應完成日起第 N 個工作日（含補班、不含假日）；N 預設 3，可由合約或各協力事項調整。</summary>
    public static DateOnly NoticeDeadline(DateOnly due, WorkCalendar calendar, int noticeWorkdays = NoticeWorkdays)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return calendar.AddWorkdays(due, Math.Clamp(noticeWorkdays, 1, MaxNoticeWorkdays));
    }

    /// <summary>此協力事項適用的通知期限工作日：事項自訂優先，否則沿用合約約定。</summary>
    public static int EffectiveNoticeWorkdays(int? itemNoticeWorkdays, int contractNoticeWorkdays) =>
        Math.Clamp(itemNoticeWorkdays ?? contractNoticeWorkdays, 1, MaxNoticeWorkdays);

    /// <summary>協力事項的追蹤狀態。</summary>
    public static CoopState State(ContractCoopItem item, bool hasNotice, DateOnly today, WorkCalendar calendar, int noticeWorkdays = NoticeWorkdays)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(calendar);

        if (item.Status != CoopStatus.Pending) return CoopState.Done;
        if (today <= item.DueDate) return CoopState.OnTrack;
        if (hasNotice) return CoopState.LateNotified;
        return today > NoticeDeadline(item.DueDate, calendar, noticeWorkdays) ? CoopState.LateNoticeOverdue : CoopState.LateNoticeDue;
    }

    /// <summary>
    /// 已逾期仍未完成、需要寄通知（或補寄更新版通知）的事項：未完成且逾期，且尚無通知或遲延已超過最近一次通知的展延工作日。
    /// </summary>
    public static bool NeedsNotice(ContractCoopItem item, int delayWorkdays, int latestNoticeExtension)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Status == CoopStatus.Pending && delayWorkdays > 0 && delayWorkdays > latestNoticeExtension;
    }

    // ---------- 工期展延與現行交期 ----------

    /// <summary>累計展延工作日：每個協力事項取最近（最大）一次通知的展延工作日，加總。</summary>
    public static int TotalExtensionWorkdays(IEnumerable<(Guid CoopItemId, int ExtensionWorkdays)> notices)
    {
        ArgumentNullException.ThrowIfNull(notices);
        return notices.GroupBy(n => n.CoopItemId).Sum(g => g.Max(n => n.ExtensionWorkdays));
    }

    /// <summary>
    /// 現行交付日：以最近一次合約變更的新交付日為基準（無則為合約原定交付日），再加上累計展延工作日（工作日曆）。
    /// 合約未約定交付日時回傳 null。
    /// </summary>
    public static DateOnly? EffectiveDeliveryDate(DateOnly? originalDelivery, IEnumerable<ContractAmendment> amendments, int extensionWorkdays, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(amendments);
        ArgumentNullException.ThrowIfNull(calendar);

        var amended = AmendedDeliveryDate(originalDelivery, amendments);
        if (amended is not { } baseDate) return null;
        return extensionWorkdays > 0 ? calendar.AddWorkdays(baseDate, extensionWorkdays) : baseDate;
    }

    /// <summary>合約變更後的交付日（尚未計入協力展延）：生效日最晚（同日取建立較晚）的新交付日。</summary>
    public static DateOnly? AmendedDeliveryDate(DateOnly? originalDelivery, IEnumerable<ContractAmendment> amendments)
    {
        ArgumentNullException.ThrowIfNull(amendments);

        var latest = amendments.Where(a => a.NewDeliveryDate is not null)
            .OrderByDescending(a => a.EffectiveDate).ThenByDescending(a => a.CreatedAtUtc).FirstOrDefault();
        return latest?.NewDeliveryDate ?? originalDelivery;
    }

    /// <summary>現行合約金額＝原價＋累計變更。</summary>
    public static decimal CurrentAmount(decimal original, IEnumerable<ContractAmendment> amendments)
    {
        ArgumentNullException.ThrowIfNull(amendments);
        return original + amendments.Sum(a => a.AmountDelta);
    }

    /// <summary>現行保固月數＝合約約定＋累計延長。</summary>
    public static int CurrentWarrantyMonths(int original, IEnumerable<ContractAmendment> amendments)
    {
        ArgumentNullException.ThrowIfNull(amendments);
        return Math.Max(0, original + amendments.Sum(a => a.WarrantyMonthsDelta));
    }

    // ---------- 文件審查回合 ----------

    /// <summary>回覆期限：送審日起算合約約定工作日；合約未約定期限回傳 null。</summary>
    public static DateOnly? ReviewResponseDue(DateOnly submitted, int? deadlineWorkdays, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return deadlineWorkdays is { } n ? calendar.AddWorkdays(submitted, n) : null;
    }

    /// <summary>
    /// 能否對某文件新增下一回合送審：尚無回合可送；上一回合須為「甲方提出修改意見」（待回覆或已通過者不可再送）。
    /// </summary>
    public static string? ValidateNewRound(IReadOnlyList<ContractReviewRound> existingRoundsOfItem, bool requiresCustomerSignoff)
    {
        ArgumentNullException.ThrowIfNull(existingRoundsOfItem);

        if (!requiresCustomerSignoff) return "這個交付項目不需甲方書面簽認，不需要送審計時。";
        var last = existingRoundsOfItem.OrderByDescending(r => r.RoundNo).FirstOrDefault();
        if (last is null) return null;

        return last.Outcome switch
        {
            ReviewOutcome.Pending => "上一回合仍在等待甲方回覆，請先記錄回覆結果。",
            ReviewOutcome.Approved or ReviewOutcome.DeemedApproved => "這份文件已通過簽認，不需要再送審。",
            _ => null
        };
    }

    /// <summary>
    /// 是否超出合約約定的彙整修改次數：回合序號大於「約定次數＋1」。
    /// 例：約定彙整修改 2 次 → 第 1 次送審、至多 2 次修改後再送審（共 3 個回合）以內不超出；第 4 個回合起須走 CR。
    /// </summary>
    public static bool ExceedsRoundLimit(int roundNo, int? maxReviewRounds) =>
        maxReviewRounds is { } max && roundNo > max + 1;

    /// <summary>可記錄的甲方回覆結果（擬制通過由系統自動標記，不可手動選）。</summary>
    public static string? ValidateRespond(ReviewOutcome current, ReviewOutcome outcome, DateOnly submitted, DateOnly responded, DateOnly today)
    {
        if (current != ReviewOutcome.Pending) return "這個回合已有結果，不能重複記錄。";
        if (outcome is not (ReviewOutcome.Approved or ReviewOutcome.RevisionRequested)) return "回覆結果只能是「通過」或「提出修改意見」。";
        if (responded < submitted) return "回覆日不得早於送審日。";
        if (responded > today) return "回覆日不得晚於今天。";
        return null;
    }

    /// <summary>逾期未回覆是否應自動擬制通過：回合仍待回覆、合約約定逾期視為通過、且今天已超過回覆期限。</summary>
    public static bool ShouldDeemApproved(ContractReviewRound round, bool deemedPassOnTimeout, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(round);
        return round.Outcome == ReviewOutcome.Pending && deemedPassOnTimeout && round.ResponseDue is { } due && today > due;
    }

    // ---------- 驗收與擬制驗收 ----------

    /// <summary>擬制驗收期限：交付日起算約定工作日；合約無擬制驗收條款回傳 null。</summary>
    public static DateOnly? DeemedDue(DateOnly delivered, int? deemedWorkdays, WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return deemedWorkdays is { } n ? calendar.AddWorkdays(delivered, n) : null;
    }

    public static string? ValidateDeliver(ContractStatus contractStatus, AcceptanceStatus current, DateOnly delivered, DateOnly today)
    {
        if (contractStatus is not (ContractStatus.Active or ContractStatus.Accepted)) return "合約須在履約中才能登錄交付。";
        if (contractStatus == ContractStatus.Accepted) return "合約已驗收完成，不能再登錄交付。";
        if (current is AcceptanceStatus.Accepted or AcceptanceStatus.DeemedAccepted) return "已驗收完成，不能再登錄交付。";
        if (current == AcceptanceStatus.Delivered) return "已登錄交付，正在等待甲方回覆；若甲方提出瑕疵清單請改記錄瑕疵。";
        if (delivered > today) return "交付日不得晚於今天。";
        return null;
    }

    public static string? ValidateDefects(AcceptanceStatus current, DateOnly? deemedDue, DateOnly delivered, DateOnly reported, DateOnly today, string? note)
    {
        if (current != AcceptanceStatus.Delivered) return "須在已交付、等待甲方回覆期間才能記錄瑕疵清單。";
        if (string.IsNullOrWhiteSpace(note)) return "請填寫甲方提出的具體瑕疵清單；沒有具體瑕疵清單不影響擬制驗收。";
        if (note.Length > 2000) return "瑕疵清單不得超過 2000 字。";
        if (reported < delivered) return "提出日不得早於交付日。";
        if (reported > today) return "提出日不得晚於今天。";
        if (deemedDue is { } due && reported > due) return "甲方提出瑕疵清單的日期已超過擬制驗收期限，依合約視為已通過驗收。";
        return null;
    }

    public static string? ValidateConfirm(AcceptanceStatus current, DateOnly? delivered, DateOnly accepted, DateOnly today)
    {
        if (current is AcceptanceStatus.Accepted or AcceptanceStatus.DeemedAccepted) return "已驗收完成。";
        if (current == AcceptanceStatus.NotDelivered || delivered is null) return "須先登錄交付，才能記錄驗收完成。";
        if (accepted < delivered) return "驗收日不得早於交付日。";
        if (accepted > today) return "驗收日不得晚於今天。";
        return null;
    }

    /// <summary>是否應自動擬制驗收：已交付且合約有擬制驗收條款、今天已超過期限（瑕疵處理中不自動）。</summary>
    public static bool ShouldDeemAccepted(ContractAcceptance acceptance, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        return acceptance.Status == AcceptanceStatus.Delivered && acceptance.DeemedDue is { } due && today > due;
    }

    /// <summary>驗收是否已完成（雙方簽認或擬制）。</summary>
    public static bool IsAccepted(AcceptanceStatus status) => status is AcceptanceStatus.Accepted or AcceptanceStatus.DeemedAccepted;

    // ---------- 付款期程 ----------

    /// <summary>整組付款期程驗證：比例合計須 100%、至少一期、每期名稱與比例有效；已請款或已收款後不可整組取代。</summary>
    public static PaymentCheck ValidateSchedule(IReadOnlyList<(string Name, decimal Percent, PaymentTrigger Trigger)> terms, bool anyInvoiced)
    {
        ArgumentNullException.ThrowIfNull(terms);

        if (anyInvoiced) return new(false, "已有期別請款或收款，付款期程不可整組修改。");
        if (terms.Count == 0) return new(false, "付款期程至少需要一期。");
        if (terms.Count > MaxPaymentTerms) return new(false, $"付款期程最多 {MaxPaymentTerms} 期。");

        foreach (var (name, percent, trigger) in terms)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) return new(false, "每期名稱必填且不得超過 100 字。");
            if (percent is <= 0 or > 100) return new(false, "每期比例須大於 0 且不超過 100%。");
            if (!Enum.IsDefined(trigger)) return new(false, "付款觸發條件不正確。");
        }

        var sum = terms.Sum(t => t.Percent);
        if (sum != 100m) return new(false, $"各期比例合計須為 100%，目前為 {sum:0.##}%。");
        if (terms.Count(t => t.Trigger == PaymentTrigger.Acceptance) > 1) return new(false, "驗收後請款（尾款）只能有一期。");

        return new(true, null);
    }

    /// <summary>
    /// 付款期程應處於的狀態（自動放行）：簽約觸發＝合約已簽約即可請款；驗收觸發＝驗收完成才可請款；
    /// 里程碑與保固結束由 PM 手動放行（不自動）。已請款以後的狀態不回退。
    /// </summary>
    public static PaymentStatus AutoStatus(ContractPaymentTerm term, ContractStatus contractStatus, bool accepted)
    {
        ArgumentNullException.ThrowIfNull(term);

        if (term.Status != PaymentStatus.Pending) return term.Status;

        var signed = contractStatus is ContractStatus.Active or ContractStatus.Accepted or ContractStatus.Warranty or ContractStatus.Closed;
        return term.Trigger switch
        {
            PaymentTrigger.Signing when signed => PaymentStatus.Invoiceable,
            PaymentTrigger.Acceptance when accepted => PaymentStatus.Invoiceable,
            _ => PaymentStatus.Pending
        };
    }

    /// <summary>狀態變更檢查：Pending→Invoiceable（僅里程碑／保固結束可手動放行，驗收觸發須已驗收）、Invoiceable→Invoiced（須填發票號與日期）、Invoiced→Paid。</summary>
    public static string? ValidateMove(
        ContractPaymentTerm term, PaymentStatus to, ContractStatus contractStatus, bool accepted, string? invoiceNo, DateOnly? date, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(term);

        var signed = contractStatus is ContractStatus.Active or ContractStatus.Accepted or ContractStatus.Warranty or ContractStatus.Closed;
        if (!signed) return "合約簽約後才能操作付款。";

        switch (term.Status, to)
        {
            case (PaymentStatus.Pending, PaymentStatus.Invoiceable):
                return term.Trigger switch
                {
                    PaymentTrigger.Milestone or PaymentTrigger.WarrantyEnd => null,
                    PaymentTrigger.Acceptance => accepted ? null : "驗收完成後才可請款（尾款依據為雙方簽認的驗收）。",
                    _ => "此期別的觸發條件已自動放行，不需手動操作。"
                };
            case (PaymentStatus.Invoiceable, PaymentStatus.Invoiced):
                if (string.IsNullOrWhiteSpace(invoiceNo) || invoiceNo.Trim().Length > 50) return "請填寫發票號碼（50 字以內）。";
                if (date is not { } d) return "請填寫開立日期。";
                if (d > today) return "開立日期不得晚於今天。";
                if (term.Trigger == PaymentTrigger.Acceptance && !accepted) return "驗收完成後才可請款。";
                return null;
            case (PaymentStatus.Invoiced, PaymentStatus.Paid):
                if (date is not { } paid) return "請填寫收款日期。";
                if (paid > today) return "收款日期不得晚於今天。";
                if (term.InvoicedDate is { } invoiced && paid < invoiced) return "收款日不得早於開立日。";
                return null;
            default:
                return $"付款狀態不能從「{StatusName(term.Status)}」變更為「{StatusName(to)}」。";
        }
    }

    /// <summary>某期金額：現行合約金額 × 比例（四捨五入到元）；最後一期補齊尾差，使各期合計等於現行金額。</summary>
    public static IReadOnlyList<decimal> Amounts(decimal currentAmount, IReadOnlyList<decimal> percents)
    {
        ArgumentNullException.ThrowIfNull(percents);

        var result = new List<decimal>(percents.Count);
        decimal running = 0;
        for (var i = 0; i < percents.Count; i++)
        {
            var amount = i == percents.Count - 1 && percents.Sum() == 100m
                ? currentAmount - running
                : Math.Round(currentAmount * percents[i] / 100m, 0, MidpointRounding.AwayFromZero);
            result.Add(amount);
            running += amount;
        }

        return result;
    }

    // ---------- 合約變更 ----------

    public static string? ValidateAmendment(string? title, decimal amountDelta, DateOnly? newDeliveryDate, int warrantyMonthsDelta, decimal currentAmount, string? note)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) return "變更標題必填且不得超過 200 字。";
        if (note is { Length: > 1000 }) return "備註不得超過 1000 字。";
        if (Math.Abs(amountDelta) > ContractRules.MaxAmount) return "變更金額不正確。";
        if (currentAmount + amountDelta < 0) return "變更後合約金額不得為負。";
        if (warrantyMonthsDelta is < -120 or > 120) return "保固延長月數須介於 -120 與 120。";
        if (amountDelta == 0 && newDeliveryDate is null && warrantyMonthsDelta == 0) return "請至少填寫一項變更（金額、新交付日或保固延長）。";
        return null;
    }

    // ---------- 協力事項與通知書驗證 ----------

    public static string? ValidateCoopItem(string? title, string? description, DateOnly dueDate, int? noticeWorkdays = null)
    {
        if (noticeWorkdays is < 1 or > MaxNoticeWorkdays) return $"通知期限須為 1~{MaxNoticeWorkdays} 個工作日（留空＝沿用合約約定）。";
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) return "協力事項必填且不得超過 200 字。";
        if (description is { Length: > 1000 }) return "說明不得超過 1000 字。";
        if (dueDate < new DateOnly(2000, 1, 1) || dueDate > new DateOnly(2100, 12, 31)) return "甲方應完成日不正確。";
        return null;
    }

    /// <summary>通知書主旨與內文（可編輯）的驗證：主旨必填、不得換行；內文必填。</summary>
    public static string? ValidateNoticeText(string? subject, string? body)
    {
        if (string.IsNullOrWhiteSpace(subject)) return "請填寫通知書主旨。";
        if (subject.Trim().Length > MaxSubjectLength) return $"主旨不得超過 {MaxSubjectLength} 字。";
        if (subject.Contains('\r') || subject.Contains('\n')) return "主旨不得換行。";
        if (string.IsNullOrWhiteSpace(body)) return "請填寫通知書內文。";
        if (body.Trim().Length > MaxBodyLength) return $"內文不得超過 {MaxBodyLength} 字。";
        return null;
    }

    /// <summary>結語敬語與署名（皆選填，空白＝預設）：長度限制、不得換行。</summary>
    public static string? ValidateNoticeSignoff(string? closing, string? signature)
    {
        if (!string.IsNullOrWhiteSpace(closing))
        {
            if (closing.Trim().Length > MaxClosingLength) return $"結語不得超過 {MaxClosingLength} 字。";
            if (closing.Contains('\r') || closing.Contains('\n')) return "結語不得換行。";
        }

        if (!string.IsNullOrWhiteSpace(signature))
        {
            if (signature.Trim().Length > MaxSignatureLength) return $"署名不得超過 {MaxSignatureLength} 字。";
            if (signature.Contains('\r') || signature.Contains('\n')) return "署名不得換行。";
        }

        return null;
    }

    public static string? ValidateNotice(int delayWorkdays, int extensionWorkdays, DateOnly requestedBy, DateOnly today, string? note, string? message)
    {
        if (delayWorkdays <= 0) return "這項協力事項尚未逾期，不需要寄遲延通知。";
        if (extensionWorkdays < 1 || extensionWorkdays > MaxExtensionWorkdays) return $"順延工作日須為 1~{MaxExtensionWorkdays}。";
        if (extensionWorkdays > delayWorkdays + NoticeWorkdays) return "順延工作日不應超過目前累計遲延工作日（可再加上預期的後續遲延，上限為遲延日數加 3）。";
        if (requestedBy < today) return "請甲方提供的期限不得早於今天。";
        if (note is { Length: > 500 }) return "備註不得超過 500 字。";
        if (message is { Length: > 2000 }) return "補充說明不得超過 2000 字。";
        return null;
    }

    // ---------- 內部 ----------

    /// <summary>(from, to] 之間的工作日數（不含起日、含迄日）。</summary>
    public static int WorkdaysBetween(DateOnly from, DateOnly to, WorkCalendar calendar)
    {
        var count = 0;
        for (var d = from.AddDays(1); d <= to; d = d.AddDays(1))
            if (calendar.IsWorkday(d)) count++;
        return count;
    }

    public static string StatusName(PaymentStatus s) => s switch
    {
        PaymentStatus.Pending => "待觸發",
        PaymentStatus.Invoiceable => "可請款",
        PaymentStatus.Invoiced => "已請款",
        _ => "已收款"
    };
}
