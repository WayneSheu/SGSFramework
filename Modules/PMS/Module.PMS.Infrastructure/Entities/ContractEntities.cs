using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>專案類型（合約管理規範 第二章 階段一：不同類型的風險盤點重點不同）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractType
{
    /// <summary>客製化網站／App。</summary>
    CustomApp = 0,

    /// <summary>ERP 導入。</summary>
    Erp = 1,

    /// <summary>政府資訊標案。</summary>
    Government = 2,

    /// <summary>大型企業委外。</summary>
    Enterprise = 3,

    Other = 9
}

/// <summary>產業別（金融／醫療／製造另有專屬合規清單，第三期啟用）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractIndustry
{
    General = 0,
    Finance = 1,
    Medical = 2,
    Manufacturing = 3
}

/// <summary>合約狀態：報價評估→審約→談判→待核決→履約中→已驗收→保固中→已結案；另有流標、取消、終止。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractStatus
{
    Quoting = 0,
    Reviewing = 1,
    Negotiating = 2,
    PendingApproval = 3,
    Active = 4,
    Accepted = 5,
    Warranty = 6,
    Closed = 7,
    Lost = 8,
    Cancelled = 9,
    Terminated = 10
}

/// <summary>合約成員角色（資料列授權；非成員一律 404）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractMemberRole
{
    Sales = 0,
    Pm = 1,
    Legal = 2,
    Executive = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LiabilityCapType
{
    /// <summary>無上限（乙方底線之外）。</summary>
    None = 0,

    /// <summary>以合約總價金為上限。</summary>
    TotalPrice = 1,

    /// <summary>以已收價金為上限。</summary>
    PaidAmount = 2,

    /// <summary>其他約定金額。</summary>
    FixedAmount = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IpRule
{
    /// <summary>未約定。</summary>
    Unspecified = 0,

    /// <summary>既有核心框架歸乙方（授權使用）、客製化程式碼尾款付清後移轉甲方（乙方標準）。</summary>
    SplitFramework = 1,

    /// <summary>所有程式碼歸甲方。</summary>
    AllToCustomer = 2,

    /// <summary>所有程式碼歸乙方。</summary>
    AllToVendor = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScanRule
{
    Unspecified = 0,

    /// <summary>鎖定單一工具與標準，僅修復客製化程式之高／中風險（乙方標準）。</summary>
    HighMediumOnly = 1,

    /// <summary>要求所有弱點清零（含低風險、第三方套件）。</summary>
    AllIncludingLow = 2
}

/// <summary>乙方對甲方條款的立場。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewStance
{
    Accept = 0,
    Compromise = 1,
    Reject = 2
}

/// <summary>審閱結果：符合乙方底線／偏離需核決／折衷採用。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewResult
{
    Pending = 0,
    Compliant = 1,
    Deviation = 2,
    Compromised = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractApprovalStep
{
    Pm = 0,
    Legal = 1,
    Executive = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApprovalDecision
{
    Approved = 0,
    Rejected = 1,
    Returned = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractEventType
{
    Created = 0,
    Updated = 1,
    StatusChanged = 2,
    ReviewItemUpdated = 3,
    Submitted = 4,
    Approved = 5,
    Rejected = 6,
    Returned = 7,
    Signed = 8,
    MembersChanged = 9,
    ProjectBound = 10,
    ProjectUnbound = 11,
    AttachmentAdded = 12,
    AttachmentRemoved = 13,
    AppendixUpdated = 14,
    Withdrawn = 15,
    CoopItemAdded = 16,
    NoticeSent = 17,
    ReviewRound = 18,
    Acceptance = 19,
    Amendment = 20,
    Payment = 21,
    Support = 22,
    Compliance = 23,
    Warranty = 24
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractAttachmentKind
{
    /// <summary>甲方合約草稿／往返版本。</summary>
    Draft = 0,

    /// <summary>用印正本（掃描檔）。</summary>
    Signed = 1,

    Other = 9
}

/// <summary>
/// 專案合約主檔（乙方標準版合約管理規範）。合約先於專案建立（報價評估→審約→簽約），簽約後綁定 1~N 個 PMS 專案。
/// 條款參數供審約自動比對乙方底線；簽約後條款鎖定。僅合約成員可見，客戶 Stakeholder 一律不可見。
/// </summary>
[DisplayName("專案合約")]
[Comment("專案合約主檔：條款參數、狀態、核決快照；僅合約成員可見")]
public class Contract : IAuditable
{
    public Guid Id { get; set; }

    [Comment("合約編號 CT-YYYY-NNN，核發後不可變")]
    public string ContractNo { get; set; } = string.Empty;

    [Comment("合約／專案名稱")]
    public string Title { get; set; } = string.Empty;

    [Comment("客戶（甲方）名稱")]
    public string CustomerName { get; set; } = string.Empty;

    [Comment("甲方窗口姓名")]
    public string? CustomerContactName { get; set; }

    [Comment("甲方窗口 Email（正式通知用，個資）")]
    public string? CustomerContactEmail { get; set; }

    public ContractType Type { get; set; } = ContractType.CustomApp;
    public ContractIndustry Industry { get; set; } = ContractIndustry.General;
    public ContractStatus Status { get; set; } = ContractStatus.Quoting;

    [Comment("合約總價金（新台幣，未稅）")]
    public decimal TotalAmount { get; set; }

    [Comment("報價日")]
    public DateOnly? QuoteDate { get; set; }

    [Comment("簽約日（用印完成日）")]
    public DateOnly? SignedDate { get; set; }

    [Comment("合約起日")]
    public DateOnly? StartDate { get; set; }

    [Comment("原定交付／驗收日（合約約定，變更與展延另計）")]
    public DateOnly? DeliveryDate { get; set; }

    // ---- 條款參數（供審約比對乙方底線） ----

    [Comment("條款含「包含但不限於」等開放式範疇字眼")]
    public bool OpenEndedScope { get; set; }

    [Comment("甲方文件審查期限（工作日）；null＝合約未約定期限")]
    public int? ReviewDeadlineWorkdays { get; set; } = 7;

    [Comment("彙整修改次數上限；null＝未約定")]
    public int? MaxReviewRounds { get; set; } = 2;

    [Comment("逾期未回覆視為通過")]
    public bool DeemedPassOnTimeout { get; set; } = true;

    public IpRule IpRule { get; set; } = IpRule.SplitFramework;
    public ScanRule ScanRule { get; set; } = ScanRule.HighMediumOnly;

    [Comment("擬制驗收期限（交付後工作日）；null＝無擬制驗收條款")]
    public int? DeemedAcceptanceWorkdays { get; set; } = 14;

    [Comment("允許因微幅瑕疵扣留全部尾款")]
    public bool FinalPaymentHeldForMinorDefects { get; set; }

    public LiabilityCapType LiabilityCap { get; set; } = LiabilityCapType.TotalPrice;

    [Comment("賠償上限金額（上限類型為 FixedAmount 時使用）")]
    public decimal? LiabilityCapAmount { get; set; }

    [Comment("排除間接損害與所失利益")]
    public bool ExcludesIndirectLoss { get; set; } = true;

    [Comment("免費保固月數")]
    public int WarrantyMonths { get; set; } = 12;

    [Comment("重大資安事件正式通報時限（小時）；null＝未約定")]
    public int? IncidentNotifyHours { get; set; } = 24;

    [Comment("實地稽核須事前通知天數；null＝未約定")]
    public int? AuditNoticeDays { get; set; } = 14;

    [Comment("每年實地稽核次數上限；null＝未約定")]
    public int? AuditPerYear { get; set; } = 1;

    [Comment("稽核費用由乙方負擔")]
    public bool AuditCostOnVendor { get; set; }

    [Comment("甲方協力遲延後，乙方須發出書面通知的工作日數（預設 3；各協力事項可個別覆寫）")]
    public int CoopNoticeWorkdays { get; set; } = 3;

    [Comment("非服務時間支援須提前申請的工作日數（預設 5）")]
    public int SupportAdvanceWorkdays { get; set; } = 5;

    [Comment("非服務時間支援是否須乙方高階主管內部核准（預設是；否則申請即視為已核准）")]
    public bool SupportRequiresApproval { get; set; } = true;

    [Comment("最近一次調整非服務時間支援參數的原因")]
    public string? SupportSettingsReason { get; set; }

    [Comment("最近一次調整非服務時間支援參數的人")]
    public string? SupportSettingsUpdatedBy { get; set; }

    [Comment("最近一次調整非服務時間支援參數的時間")]
    public DateTimeOffset? SupportSettingsUpdatedAt { get; set; }

    [Comment("非服務時間支援單次窗口上限（天，預設 7）")]
    public int SupportMaxWindowDays { get; set; } = 7;

    [Comment("平日加班倍率（預設 1.5）")]
    public decimal OvertimeWeekdayRate { get; set; } = 1.5m;

    [Comment("深夜與例假日加班倍率（預設 2.0）")]
    public decimal OvertimeNightRestRate { get; set; } = 2.0m;

    [Comment("國定假日加班倍率（預設 2.5）")]
    public decimal OvertimeHolidayRate { get; set; } = 2.5m;

    [Comment("標準服務時間")]
    public string ServiceHours { get; set; } = "週一至週五 09:00–18:00（不含國定假日）";

    [Comment("唯一正式報修管道（客服信箱或系統）")]
    public string? SupportChannel { get; set; }

    [Comment("備註")]
    public string? Notes { get; set; }

    // ---- 核決 ----

    [Comment("送審週期（每次送審加一；退回後再送審為新週期）")]
    public int ApprovalCycle { get; set; }

    [Comment("送審時鎖定的條款與審閱結果 SHA-256；核決與簽約前比對")]
    public string? SnapshotHash { get; set; }

    public Guid? SubmittedBy { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? SignedBy { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public List<ContractMember> Members { get; set; } = [];
    public List<ContractReviewItem> ReviewItems { get; set; } = [];
    public List<ContractApproval> Approvals { get; set; } = [];
    public List<ContractDeliverableItem> DeliverableItems { get; set; } = [];
    public List<ContractSlaLevel> SlaLevels { get; set; } = [];
    public List<ContractProject> Projects { get; set; } = [];
}

[DisplayName("合約成員")]
[Comment("合約成員與角色（業務／PM／法務／核決人）；資料列授權依據")]
public class ContractMember : IAuditable
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid UserId { get; set; }
    public ContractMemberRole Role { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
}

/// <summary>乙方合約重點審閱檢核表的一個項目（七項固定，產業別項目第三期加入）。</summary>
[DisplayName("合約審閱項目")]
[Comment("審約檢核表：甲方原條款、乙方立場與審閱結果")]
public class ContractReviewItem : IAuditable
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }

    [Comment("項目代碼（SCOPE、DOC_REVIEW、IPR、VULN_SCAN、ACCEPTANCE、LIABILITY、SECURITY）")]
    public string Code { get; set; } = string.Empty;

    [Comment("甲方原條款（摘錄）")]
    public string? ClientClause { get; set; }

    public ReviewStance Stance { get; set; } = ReviewStance.Accept;
    public ReviewResult Result { get; set; } = ReviewResult.Pending;

    [Comment("審閱說明或折衷方案")]
    public string? Note { get; set; }

    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
}

/// <summary>核決紀錄：每個送審週期，各步驟（PM、法務、高階主管）的決定。</summary>
[DisplayName("合約核決")]
[Comment("合約核決紀錄（PM 初審、法務審、高階主管核決）")]
public class ContractApproval
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public int Cycle { get; set; }
    public ContractApprovalStep Step { get; set; }
    public ApprovalDecision Decision { get; set; }
    public string? Comment { get; set; }
    public Guid DecidedBy { get; set; }
    public string DecidedByName { get; set; } = string.Empty;
    public DateTimeOffset DecidedAt { get; set; }

    public Contract? Contract { get; set; }
}

/// <summary>附件一《交付項目清單》的一列。</summary>
[DisplayName("合約交付項目")]
[Comment("附件一交付項目清單：正面表列的交付物與確認方式")]
public class ContractDeliverableItem : IAuditable
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public int SortOrder { get; set; }

    [Comment("階段（規劃、分析、設計、測試、訓練、結案…）")]
    public string Stage { get; set; } = string.Empty;

    [Comment("交付項目名稱")]
    public string Name { get; set; } = string.Empty;

    [Comment("交付形式")]
    public string Format { get; set; } = string.Empty;

    [Comment("確認／驗收方式")]
    public string AcceptMethod { get; set; } = string.Empty;

    [Comment("需甲方書面簽認（第二期：文件審查計時依據）")]
    public bool RequiresCustomerSignoff { get; set; }

    [Comment("指定交付日（合約約定該項目交付給甲方的日期；未約定為空）")]
    public DateOnly? DeliveryDate { get; set; }

    [Comment("實際交付日（由業務／合約 PM 記錄；空表示尚未交付）")]
    public DateOnly? DeliveredDate { get; set; }

    [Comment("交付方式（Email、系統上線、紙本面交、雲端連結等）")]
    public string? DeliveredChannel { get; set; }

    [Comment("交付備註（對象、網址、版本等）")]
    public string? DeliveredNote { get; set; }

    [Comment("最近一次到期提醒時間（每個台灣日最多一次）")]
    public DateTimeOffset? LastReminderAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
}

/// <summary>附件二《保固期 SLA 異常分級與回應標準》的一列。</summary>
[DisplayName("合約 SLA 分級")]
[Comment("附件二 SLA 分級：定義、回應時間與處理目標")]
public class ContractSlaLevel : IAuditable
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }

    [Comment("等級 1~3")]
    public int Level { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;

    [Comment("回應時間（標準服務時間內，小時）")]
    public decimal ResponseHours { get; set; }

    [Comment("處理期限（時鐘小時，例：Level 1 於 4 小時內提出替代方案）；與 DueWorkdays 擇一，皆空＝不覆寫系統 SLA")]
    public decimal? DueHours { get; set; }

    [Comment("處理期限（工作日，例：Level 2 於 2 個工作日內提供修正）；與 DueHours 擇一")]
    public int? DueWorkdays { get; set; }

    [Comment("處理目標說明")]
    public string Target { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
}

/// <summary>合約與 PMS 專案的綁定（一個專案至多屬於一份合約）。</summary>
[DisplayName("合約專案")]
[Comment("合約綁定的 PMS 專案")]
public class ContractProject : IAuditable
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid ProjectId { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
    public Project? Project { get; set; }
}

/// <summary>合約事件（稽核軌跡）。</summary>
[DisplayName("合約事件")]
[Comment("合約事件軌跡：狀態轉移、核決、簽約與異動")]
public class ContractEvent
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public ContractEventType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTimeOffset At { get; set; }

    public Contract? Contract { get; set; }
}

/// <summary>合約附件（甲方草稿、用印正本等），內容存 FMS（分類 pms-contract，加密、不落地）。</summary>
[DisplayName("合約附件")]
[Comment("合約附件中繼資料；檔案內容存放於檔案管理（FMS）")]
public class ContractAttachment : IAuditable
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public string? Sha256 { get; set; }
    public ContractAttachmentKind Kind { get; set; } = ContractAttachmentKind.Other;
    public bool IsDeleted { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
}
