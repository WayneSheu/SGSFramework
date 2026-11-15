using System.Security.Cryptography;
using System.Text;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ContractFlagSeverity
{
    Medium = 1,
    High = 2,
    Critical = 3
}

/// <summary>審約自動比對乙方底線的結果：哪個審閱項目、多嚴重、為什麼。</summary>
public sealed record ContractFlag(string ItemCode, ContractFlagSeverity Severity, string Message);

/// <summary>審閱檢核表的一個固定項目（規範第二章 階段二的七項）：甲方常見陷阱與乙方談判底線。</summary>
public sealed record ReviewItemSpec(string Code, string Name, string ClientTrap, string Defense);

/// <summary>預設附件一／附件二的種子列。</summary>
public sealed record DeliverableSeed(string Stage, string Name, string Format, string AcceptMethod, bool RequiresCustomerSignoff, DateOnly? DeliveryDate = null);

/// <param name="DueHours">處理期限（時鐘小時）；與 <paramref name="DueWorkdays"/> 擇一，皆空＝不覆寫系統 SLA。</param>
public sealed record SlaLevelSeed(int Level, string Name, string Definition, decimal ResponseHours, string Target, decimal? DueHours = null, int? DueWorkdays = null);

/// <summary>
/// 專案合約管理規則（乙方標準版）：條款驗證、審約自動比對、核決步驟與職責分離、狀態轉移、合約編號與預設附件。全為純函式，不碰資料庫。
/// 簽約前（報價／審約／談判）可編修條款；送審後鎖定（含 SHA-256 快照），退回才可修改；簽約後條款鎖定，變更走合約變更（第二期）。
/// </summary>
public static class ContractRules
{
    public const int MaxMembersPerRole = 5;
    public const int MaxDeliverableItems = 40;
    public const int MaxProjectsPerContract = 20;
    public const int MaxAttachmentsPerContract = 100;
    public const decimal MaxAmount = 100_000_000_000m;

    // ---------- 審閱項目目錄（規範第二章 階段二表格） ----------

    public static IReadOnlyList<ReviewItemSpec> ReviewCatalog { get; } =
    [
        new("SCOPE", "需求範疇", "出現「包含但不限於甲方日後提出之合理需求」等模糊字眼。", "刪除「不限於」等字眼，明訂超出附件規格書者一律走變更程序（CR）另行計費。"),
        new("DOC_REVIEW", "文件審查", "審查無期限、無次數限制，或藉審查名義新增需求。", "限定甲方於【7】個工作日內回覆，彙整修改意見以【2】次為限，逾期視為通過。"),
        new("IPR", "智慧財產權", "要求系統「所有程式碼」著作權全數歸甲方所有。", "區分「既有核心框架／模組（歸乙方，僅授權使用）」與「本次客製化程式碼（尾款付清後移轉甲方）」。"),
        new("VULN_SCAN", "弱點掃描", "要求所有弱點清零（含低風險），或因第三方套件漏洞拒收。", "鎖定單一掃描工具與標準；僅限修復客製化程式之「高／中風險」弱點，低風險與第三方套件限制不影響驗收。"),
        new("ACCEPTANCE", "驗收與尾款", "甲方遲遲不測，或因微幅排版瑕疵扣留全部尾款。", "訂定擬制驗收條款（交付後【14】個工作日未提出具體瑕疵清單即視為通過）；一般瑕疵列保固修復，不影響驗收請款。"),
        new("LIABILITY", "賠償責任", "損害賠償無金額上限，且包含營業損失或商譽損失。", "排除間接與所失利益；總賠償責任上限以「合約總價金（或已收價金）」為限。"),
        new("SECURITY", "資安與個資", "1 小時內正式通報、隨時無預警實地稽核且費用乙方出。", "改為「確認重大資安事件後 24 小時內正式通報」；實地稽核須於 14 日前通知、每年 1 次為限且費用由甲方自理。")
    ];

    public static bool IsKnownReviewCode(string? code) => ReviewCatalog.Any(r => string.Equals(r.Code, code, StringComparison.Ordinal));

    // ---------- 預設附件（規範第四章） ----------

    public static IReadOnlyList<DeliverableSeed> DefaultDeliverables { get; } =
    [
        new("規劃", "專案管理計畫書（PMP）", "電子檔 (PDF)", "甲方書面簽認", true),
        new("分析", "系統需求規格書（SRS）", "電子檔 (PDF)", "甲方書面簽認（開發基準）", true),
        new("設計", "系統分析與設計書（SA/SD）／UI 設計稿", "電子檔／線上連結", "甲方書面簽認（限 2 次彙整修改）", true),
        new("測試", "系統測試報告（SIT）與弱點掃描報告（SAST）", "電子檔 (PDF)", "隨系統交付備查（無高／中風險）", false),
        new("訓練", "系統操作手冊與系統管理員維運手冊", "電子檔 (PDF)", "甲方書面簽認", true),
        new("結案", "使用者驗收測試報告（UAT）", "紙本用印／電子簽章", "雙方用印（請領尾款依據）", true),
        new("結案", "客製化系統原始碼（不含乙方既有底層框架）", "Git／儲存媒體", "尾款付清後交付", false)
    ];

    public static IReadOnlyList<SlaLevelSeed> DefaultSlaLevels { get; } =
    [
        new(1, "Level 1 緊急", "系統完全無法運作、核心交易中斷，且無替代方案。", 2m, "立即排查，於 4 小時內提出暫時性替代方案（Workaround）恢復運作。", DueHours: 4m),
        new(2, "Level 2 一般", "部分非核心功能異常，但不影響系統整體運作與主要業務。", 4m, "安排工程師檢修，預計於 2 個工作日內提供修正版本。", DueWorkdays: 2),
        new(3, "Level 3 輕微", "畫面排版微調、文字顯示異常或操作方式諮詢。", 8m, "彙整於下次定期更新修正，或於 3 個工作日內回覆說明。", DueWorkdays: 3)
    ];

    // ---------- 狀態與編修權 ----------

    private static readonly Dictionary<ContractStatus, ContractStatus[]> Transitions = new()
    {
        [ContractStatus.Quoting] = [ContractStatus.Reviewing, ContractStatus.Lost, ContractStatus.Cancelled],
        [ContractStatus.Reviewing] = [ContractStatus.Quoting, ContractStatus.Negotiating, ContractStatus.Lost, ContractStatus.Cancelled],
        [ContractStatus.Negotiating] = [ContractStatus.Reviewing, ContractStatus.Lost, ContractStatus.Cancelled],
        // 待核決只能經核決流程離開（退回→談判、簽約→履約中），不開放手動轉換
        [ContractStatus.PendingApproval] = [],
        [ContractStatus.Active] = [ContractStatus.Accepted, ContractStatus.Terminated],
        [ContractStatus.Accepted] = [ContractStatus.Warranty, ContractStatus.Closed, ContractStatus.Terminated],
        [ContractStatus.Warranty] = [ContractStatus.Closed, ContractStatus.Terminated],
        [ContractStatus.Closed] = [],
        [ContractStatus.Lost] = [],
        [ContractStatus.Cancelled] = [],
        [ContractStatus.Terminated] = []
    };

    /// <summary>手動變更狀態是否合法（送審、核決、簽約走各自流程）。</summary>
    public static bool CanTransition(ContractStatus from, ContractStatus to) =>
        Transitions.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>條款可編修：報價評估、審約、談判三個簽約前階段。</summary>
    public static bool CanEditTerms(ContractStatus status) =>
        status is ContractStatus.Quoting or ContractStatus.Reviewing or ContractStatus.Negotiating;

    public static bool IsPreSigning(ContractStatus status) => CanEditTerms(status) || status == ContractStatus.PendingApproval;

    public static bool CanBindProjects(ContractStatus status) =>
        status is ContractStatus.Active or ContractStatus.Accepted or ContractStatus.Warranty;

    // ---------- 成員與授權 ----------

    public static bool IsMember(Guid userId, IReadOnlyList<(Guid UserId, ContractMemberRole Role)> members) =>
        members.Any(m => m.UserId == userId);

    public static bool HasRole(Guid userId, ContractMemberRole role, IReadOnlyList<(Guid UserId, ContractMemberRole Role)> members) =>
        members.Any(m => m.UserId == userId && m.Role == role);

    /// <summary>業務與合約 PM 可維護條款、附件、送審與簽約登錄。</summary>
    public static bool CanManage(Guid userId, IReadOnlyList<(Guid UserId, ContractMemberRole Role)> members) =>
        HasRole(userId, ContractMemberRole.Sales, members) || HasRole(userId, ContractMemberRole.Pm, members);

    /// <summary>可填寫審閱項目的結果：合約 PM 與法務。</summary>
    public static bool CanReview(Guid userId, IReadOnlyList<(Guid UserId, ContractMemberRole Role)> members) =>
        HasRole(userId, ContractMemberRole.Pm, members) || HasRole(userId, ContractMemberRole.Legal, members);

    public static ContractMemberRole RoleOfStep(ContractApprovalStep step) => step switch
    {
        ContractApprovalStep.Pm => ContractMemberRole.Pm,
        ContractApprovalStep.Legal => ContractMemberRole.Legal,
        _ => ContractMemberRole.Executive
    };

    // ---------- 條款驗證 ----------

    /// <summary>驗證條款欄位；回傳第一個錯誤訊息，通過回傳 null。</summary>
    public static string? ValidateTerms(Contract c)
    {
        ArgumentNullException.ThrowIfNull(c);

        if (string.IsNullOrWhiteSpace(c.Title)) return "請輸入合約名稱。";
        if (c.Title.Trim().Length > 200) return "合約名稱不得超過 200 字。";
        if (string.IsNullOrWhiteSpace(c.CustomerName)) return "請輸入客戶名稱。";
        if (c.CustomerName.Trim().Length > 200) return "客戶名稱不得超過 200 字。";
        if (c.CustomerContactName is { Length: > 100 }) return "甲方窗口姓名不得超過 100 字。";
        if (!string.IsNullOrWhiteSpace(c.CustomerContactEmail) && !IsPlausibleEmail(c.CustomerContactEmail))
            return "甲方窗口 Email 格式不正確。";
        if (c.TotalAmount < 0 || c.TotalAmount > MaxAmount) return "合約總價金須介於 0 與 1,000 億之間。";
        if (c.StartDate is { } start && c.DeliveryDate is { } delivery && delivery < start) return "原定交付日不得早於合約起日。";
        if (c.ReviewDeadlineWorkdays is < 1 or > 90) return "文件審查期限須為 1~90 個工作日。";
        if (c.MaxReviewRounds is < 1 or > 20) return "彙整修改次數須為 1~20 次。";
        if (c.DeemedAcceptanceWorkdays is < 1 or > 180) return "擬制驗收期限須為 1~180 個工作日。";
        if (c.WarrantyMonths is < 0 or > 120) return "保固月數須為 0~120 個月。";
        if (c.CoopNoticeWorkdays is < 1 or > ContractFulfillmentRules.MaxNoticeWorkdays)
            return $"甲方協力遲延的通知期限須為 1~{ContractFulfillmentRules.MaxNoticeWorkdays} 個工作日。";
        if (c.IncidentNotifyHours is < 1 or > 720) return "資安事件通報時限須為 1~720 小時。";
        if (c.AuditNoticeDays is < 0 or > 365) return "稽核事前通知天數須為 0~365 天。";
        if (c.AuditPerYear is < 0 or > 52) return "每年稽核次數須為 0~52 次。";
        if (c.LiabilityCap == LiabilityCapType.FixedAmount && (c.LiabilityCapAmount is null or <= 0))
            return "賠償上限為「固定金額」時須填寫大於 0 的金額。";
        if (c.LiabilityCapAmount is < 0 or > MaxAmount) return "賠償上限金額不正確。";
        if (string.IsNullOrWhiteSpace(c.ServiceHours) || c.ServiceHours.Length > 200) return "標準服務時間必填且不得超過 200 字。";
        if (c.SupportChannel is { Length: > 200 }) return "報修管道不得超過 200 字。";
        if (c.Notes is { Length: > 2000 }) return "備註不得超過 2000 字。";
        return null;
    }

    private static bool IsPlausibleEmail(string value)
    {
        var v = value.Trim();
        var at = v.IndexOf('@', StringComparison.Ordinal);
        return v.Length <= 256 && at > 0 && at == v.LastIndexOf('@') && v.IndexOf('.', at) > at + 1 && !v.Contains(' ', StringComparison.Ordinal);
    }

    // ---------- 審約：自動比對乙方底線 ----------

    /// <summary>依條款參數自動標示偏離乙方底線之處（規範第二章 階段二表格）。</summary>
    public static IReadOnlyList<ContractFlag> AutoFlags(Contract c)
    {
        ArgumentNullException.ThrowIfNull(c);

        var flags = new List<ContractFlag>();

        if (c.OpenEndedScope)
            flags.Add(new("SCOPE", ContractFlagSeverity.High, "條款含「包含但不限於」等開放式範疇字眼，須刪除，超出附件規格者走 CR。"));

        if (c.ReviewDeadlineWorkdays is null)
            flags.Add(new("DOC_REVIEW", ContractFlagSeverity.High, "文件審查無期限，乙方底線為 7 個工作日內回覆。"));
        if (c.MaxReviewRounds is null)
            flags.Add(new("DOC_REVIEW", ContractFlagSeverity.High, "文件審查未限制彙整修改次數，乙方底線為 2 次。"));
        if (!c.DeemedPassOnTimeout)
            flags.Add(new("DOC_REVIEW", ContractFlagSeverity.Medium, "逾期未回覆未約定視為通過。"));

        if (c.IpRule == IpRule.AllToCustomer)
            flags.Add(new("IPR", ContractFlagSeverity.High, "所有程式碼歸甲方，應區分既有核心框架（乙方）與客製化程式碼（尾款付清後移轉）。"));
        else if (c.IpRule == IpRule.Unspecified)
            flags.Add(new("IPR", ContractFlagSeverity.Medium, "智慧財產權歸屬未約定。"));

        if (c.ScanRule == ScanRule.AllIncludingLow)
            flags.Add(new("VULN_SCAN", ContractFlagSeverity.High, "要求弱點全數清零（含低風險、第三方套件），乙方底線為客製化程式之高／中風險。"));
        else if (c.ScanRule == ScanRule.Unspecified)
            flags.Add(new("VULN_SCAN", ContractFlagSeverity.Medium, "弱點掃描工具與標準未約定。"));

        if (c.DeemedAcceptanceWorkdays is null)
            flags.Add(new("ACCEPTANCE", ContractFlagSeverity.High, "無擬制驗收條款，乙方底線為交付後 14 個工作日未提瑕疵清單即視為通過。"));
        if (c.FinalPaymentHeldForMinorDefects)
            flags.Add(new("ACCEPTANCE", ContractFlagSeverity.High, "允許因微幅瑕疵扣留全部尾款，一般瑕疵應列保固修復、不影響請款。"));

        if (c.LiabilityCap == LiabilityCapType.None)
            flags.Add(new("LIABILITY", ContractFlagSeverity.Critical, "賠償責任無金額上限，乙方底線為以合約總價金（或已收價金）為限。"));
        else if (c.LiabilityCap == LiabilityCapType.FixedAmount && c.LiabilityCapAmount is { } cap && cap > c.TotalAmount && c.TotalAmount > 0)
            flags.Add(new("LIABILITY", ContractFlagSeverity.Medium, "賠償上限高於合約總價金。"));
        if (!c.ExcludesIndirectLoss)
            flags.Add(new("LIABILITY", ContractFlagSeverity.High, "未排除間接損害與所失利益（營業損失、商譽損失）。"));

        if (c.IncidentNotifyHours is null or < 24)
            flags.Add(new("SECURITY", ContractFlagSeverity.High, "資安事件通報時限短於乙方底線（確認重大事件後 24 小時內正式通報）或未約定。"));
        if (c.AuditNoticeDays is null or < 14)
            flags.Add(new("SECURITY", ContractFlagSeverity.High, "實地稽核事前通知少於 14 日或未約定。"));
        if (c.AuditPerYear is null or > 1)
            flags.Add(new("SECURITY", ContractFlagSeverity.Medium, "實地稽核次數未限制每年 1 次。"));
        if (c.AuditCostOnVendor)
            flags.Add(new("SECURITY", ContractFlagSeverity.Medium, "稽核費用由乙方負擔，底線為由甲方自理。"));

        return flags;
    }

    public static ContractFlagSeverity? WorstSeverity(IEnumerable<ContractFlag> flags) =>
        flags.Select(f => (ContractFlagSeverity?)f.Severity).DefaultIfEmpty(null).Max();

    // ---------- 送審 ----------

    /// <summary>送審前檢查；回傳所有阻擋原因（空＝可送審）。</summary>
    public static IReadOnlyList<string> ValidateSubmit(
        Contract c, IReadOnlyList<ContractReviewItem> items, IReadOnlyList<(Guid UserId, ContractMemberRole Role)> members,
        IReadOnlyList<ContractComplianceItem>? compliance = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(items);

        var problems = new List<string>();

        var terms = ValidateTerms(c);
        if (terms is not null) problems.Add(terms);
        if (c.TotalAmount <= 0) problems.Add("請填寫合約總價金。");
        if (c.DeliveryDate is null) problems.Add("請填寫原定交付日。");

        var flags = AutoFlags(c);
        foreach (var spec in ReviewCatalog)
        {
            var item = items.FirstOrDefault(i => i.Code == spec.Code);
            if (item is null || item.Result == ReviewResult.Pending)
            {
                problems.Add($"審閱項目「{spec.Name}」尚未填寫審閱結果。");
                continue;
            }

            if (item.Result == ReviewResult.Compliant && flags.Any(f => f.ItemCode == spec.Code))
                problems.Add($"審閱項目「{spec.Name}」的條款參數偏離乙方底線，結果不可標為「符合」，請改為「偏離」或「折衷」，或調整條款。");
            if (item.Result is ReviewResult.Deviation or ReviewResult.Compromised && string.IsNullOrWhiteSpace(item.Note))
                problems.Add($"審閱項目「{spec.Name}」為偏離或折衷，請填寫說明或折衷方案。");
        }

        foreach (var role in new[] { ContractMemberRole.Sales, ContractMemberRole.Pm, ContractMemberRole.Executive })
            if (!members.Any(m => m.Role == role))
                problems.Add($"請指派{RoleName(role)}。");

        var required = RequiredSteps(items, flags);
        if (required.Contains(ContractApprovalStep.Legal) && !members.Any(m => m.Role == ContractMemberRole.Legal))
            problems.Add("本合約有偏離或折衷條款，須指派法務審核。");

        // 金融、醫療、製造須逐項確認產業合規項目（已納入或不適用並說明）
        if (c.Industry != ContractIndustry.General)
            problems.AddRange(ContractComplianceCatalog.ValidateForSubmit(c.Industry, compliance ?? []));

        return problems;
    }

    /// <summary>核決步驟：PM 初審、有偏離／折衷／自動標示者加法務、最後高階主管核決。</summary>
    public static IReadOnlyList<ContractApprovalStep> RequiredSteps(IEnumerable<ContractReviewItem> items, IEnumerable<ContractFlag> flags)
    {
        var needsLegal = items.Any(i => i.Result is ReviewResult.Deviation or ReviewResult.Compromised) || flags.Any();
        return needsLegal
            ? [ContractApprovalStep.Pm, ContractApprovalStep.Legal, ContractApprovalStep.Executive]
            : [ContractApprovalStep.Pm, ContractApprovalStep.Executive];
    }

    /// <summary>下一個待決的核決步驟（依序）；全部核准回傳 null。</summary>
    public static ContractApprovalStep? NextStep(IReadOnlyList<ContractApprovalStep> required, IEnumerable<ContractApproval> currentCycle)
    {
        var approved = currentCycle.Where(a => a.Decision == ApprovalDecision.Approved).Select(a => a.Step).ToHashSet();
        foreach (var step in required)
            if (!approved.Contains(step)) return step;
        return null;
    }

    /// <summary>核決檢查：步驟輪到、身分符合、職責分離（不得核決自己送審的案，且同一人不得決定同週期的兩個步驟）。</summary>
    public static string? ValidateDecision(
        ContractApprovalStep step, ApprovalDecision decision, string? comment, Guid userId, Guid? submittedBy,
        ContractApprovalStep? next, IReadOnlyList<(Guid UserId, ContractMemberRole Role)> members, IEnumerable<ContractApproval> currentCycle)
    {
        if (next is null) return "所有核決步驟已完成。";
        if (next != step) return $"目前輪到「{StepName(next.Value)}」，尚不能進行「{StepName(step)}」。";
        if (!HasRole(userId, RoleOfStep(step), members)) return $"僅合約的{RoleName(RoleOfStep(step))}可進行此核決。";
        if (submittedBy == userId) return "不得核決自己送審的合約（職責分離）。";
        if (currentCycle.Any(a => a.DecidedBy == userId && a.Decision == ApprovalDecision.Approved))
            return "同一人不得核決同一送審週期的兩個步驟（職責分離）。";
        if (decision != ApprovalDecision.Approved && string.IsNullOrWhiteSpace(comment)) return "退回或否決時請填寫意見。";
        if (comment is { Length: > 1000 }) return "意見不得超過 1000 字。";
        return null;
    }

    public static string? ValidateSign(Contract c, ContractApprovalStep? nextStep, string? currentHash)
    {
        ArgumentNullException.ThrowIfNull(c);

        if (c.Status != ContractStatus.PendingApproval) return "僅待核決的合約可登錄簽約。";
        if (nextStep is not null) return "尚有核決步驟未完成，不能登錄簽約。";
        if (c.SnapshotHash is null || !string.Equals(c.SnapshotHash, currentHash, StringComparison.OrdinalIgnoreCase))
            return "條款或審閱結果在送審後有異動，請退回重新送審。";
        return null;
    }

    // ---------- 快照雜湊 ----------

    /// <summary>送審快照：條款參數與審閱結果的規範化字串 SHA-256（十六進位小寫）。</summary>
    public static string ComputeSnapshotHash(Contract c, IEnumerable<ContractReviewItem> items)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(items);

        var sb = new StringBuilder();
        void Add(object? value) => sb.Append(value switch
        {
            null => string.Empty,
            decimal d => d.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            bool b => b ? "1" : "0",
            Enum e => e.ToString(),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
        }).Append('\u001f');

        Add(c.Title?.Trim()); Add(c.CustomerName?.Trim()); Add(c.CustomerContactName?.Trim()); Add(c.CustomerContactEmail?.Trim());
        Add(c.Type); Add(c.Industry); Add(c.TotalAmount); Add(c.QuoteDate); Add(c.StartDate); Add(c.DeliveryDate);
        Add(c.OpenEndedScope); Add(c.ReviewDeadlineWorkdays); Add(c.MaxReviewRounds); Add(c.DeemedPassOnTimeout);
        Add(c.IpRule); Add(c.ScanRule); Add(c.DeemedAcceptanceWorkdays); Add(c.FinalPaymentHeldForMinorDefects);
        Add(c.LiabilityCap); Add(c.LiabilityCapAmount); Add(c.ExcludesIndirectLoss); Add(c.WarrantyMonths);
        Add(c.IncidentNotifyHours); Add(c.AuditNoticeDays); Add(c.AuditPerYear); Add(c.AuditCostOnVendor);
        Add(c.ServiceHours?.Trim()); Add(c.SupportChannel?.Trim());

        foreach (var item in items.OrderBy(i => i.Code, StringComparer.Ordinal))
        {
            sb.Append('|');
            Add(item.Code); Add(item.ClientClause?.Trim()); Add(item.Stance); Add(item.Result); Add(item.Note?.Trim());
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    // ---------- 合約編號 ----------

    /// <summary>下一個合約編號：同年度最大流水號＋1，格式 CT-YYYY-NNN（三位以上）。</summary>
    public static string NextContractNo(int year, IEnumerable<string> existingNumbers)
    {
        ArgumentNullException.ThrowIfNull(existingNumbers);

        var prefix = $"CT-{year}-";
        var max = 0;
        foreach (var no in existingNumbers)
        {
            if (no is null || !no.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (int.TryParse(no.AsSpan(prefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n > max)
                max = n;
        }

        return $"{prefix}{max + 1:000}";
    }

    // ---------- 附件一／二 驗證 ----------

    public static string? ValidateDeliverableItems(IReadOnlyList<DeliverableSeed> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0) return "交付項目清單至少需要一列。";
        if (items.Count > MaxDeliverableItems) return $"交付項目最多 {MaxDeliverableItems} 列。";
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Stage) || item.Stage.Trim().Length > 50) return "階段必填且不得超過 50 字。";
            if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Trim().Length > 200) return "交付項目名稱必填且不得超過 200 字。";
            if (string.IsNullOrWhiteSpace(item.Format) || item.Format.Trim().Length > 100) return "交付形式必填且不得超過 100 字。";
            if (string.IsNullOrWhiteSpace(item.AcceptMethod) || item.AcceptMethod.Trim().Length > 200) return "確認／驗收方式必填且不得超過 200 字。";
            if (item.DeliveryDate is { } date && (date.Year < 2000 || date.Year > 2100)) return $"「{item.Name.Trim()}」的指定交付日不正確。";
        }

        return null;
    }

    public static string? ValidateSlaLevels(IReadOnlyList<SlaLevelSeed> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);

        if (levels.Count is < 1 or > 5) return "SLA 分級須為 1~5 級。";
        if (levels.Select(l => l.Level).Distinct().Count() != levels.Count) return "SLA 等級不可重複。";
        foreach (var l in levels)
        {
            if (l.Level is < 1 or > 5) return "SLA 等級須介於 1~5。";
            if (string.IsNullOrWhiteSpace(l.Name) || l.Name.Trim().Length > 50) return "SLA 等級名稱必填且不得超過 50 字。";
            if (string.IsNullOrWhiteSpace(l.Definition) || l.Definition.Trim().Length > 500) return "SLA 定義必填且不得超過 500 字。";
            if (string.IsNullOrWhiteSpace(l.Target) || l.Target.Trim().Length > 500) return "SLA 處理目標必填且不得超過 500 字。";
            if (l.ResponseHours is <= 0 or > 240m) return "回應時間須大於 0 且不超過 240 小時。";
            var due = ContractSlaRules.ValidateDue(l.DueHours, l.DueWorkdays);
            if (due is not null) return due;
        }

        return null;
    }

    // ---------- 顯示輔助 ----------

    public static string RoleName(ContractMemberRole role) => role switch
    {
        ContractMemberRole.Sales => "業務",
        ContractMemberRole.Pm => "合約 PM",
        ContractMemberRole.Legal => "法務",
        _ => "核決主管"
    };

    public static string StepName(ContractApprovalStep step) => step switch
    {
        ContractApprovalStep.Pm => "PM 初審",
        ContractApprovalStep.Legal => "法務審核",
        _ => "高階主管核決"
    };
}
