using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>甲方協力事項類型（規範第二章 階段三：遲延提供測試資料、環境、API 文件、逾期未簽認 SA/SD）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CoopKind
{
    DocSignoff = 0,
    Environment = 1,
    ApiDoc = 2,
    TestData = 3,
    Other = 9
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CoopStatus
{
    Pending = 0,
    Completed = 1,

    /// <summary>甲方免除或乙方決定不需要（保留紀錄）。</summary>
    Waived = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NoticeMode
{
    /// <summary>系統代寄 Email（附正式 PDF）。</summary>
    Email = 0,

    /// <summary>PM 已自行寄出（正式公文等），僅記錄。</summary>
    RecordOnly = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewOutcome
{
    /// <summary>已送審，等待甲方回覆。</summary>
    Pending = 0,

    Approved = 1,

    /// <summary>甲方提出修改意見（算一次彙整修改）。</summary>
    RevisionRequested = 2,

    /// <summary>逾期未回覆，依合約視為通過（系統自動標記）。</summary>
    DeemedApproved = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AcceptanceStatus
{
    NotDelivered = 0,

    /// <summary>已交付，等待甲方在擬制驗收期限內回覆。</summary>
    Delivered = 1,

    /// <summary>甲方於期限內提出具體瑕疵清單，轉瑕疵處理。</summary>
    DefectsReported = 2,

    /// <summary>雙方簽認驗收完成（UAT 報告用印）。</summary>
    Accepted = 3,

    /// <summary>期限內未提瑕疵清單，依合約視為驗收通過（系統自動標記）。</summary>
    DeemedAccepted = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AmendmentSource
{
    ChangeRequest = 0,
    Manual = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentTrigger
{
    /// <summary>簽約後即可請款（頭期款）。</summary>
    Signing = 0,

    /// <summary>PM 於里程碑達成後手動放行。</summary>
    Milestone = 1,

    /// <summary>驗收完成（含擬制驗收）後才可請款（尾款依據）。</summary>
    Acceptance = 2,

    /// <summary>保固結束後（PM 手動放行）。</summary>
    WarrantyEnd = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus
{
    Pending = 0,
    Invoiceable = 1,
    Invoiced = 2,
    Paid = 3
}

/// <summary>甲方協力事項：甲方應提供的資料／環境／簽認，遲延時乙方須於 3 個工作日內書面通知，工期自動順延。</summary>
[DisplayName("合約甲方協力事項")]
[Comment("甲方協力事項：應完成日、實際完成日與遲延追蹤")]
public class ContractCoopItem : IAuditable
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }

    [Comment("協力事項")]
    public string Title { get; set; } = string.Empty;

    public CoopKind Kind { get; set; } = CoopKind.Other;

    [Comment("詳細說明（例：待簽認文件名稱、環境規格）")]
    public string? Description { get; set; }

    [Comment("甲方應完成日（合約或專案時程約定）")]
    public DateOnly DueDate { get; set; }

    [Comment("甲方實際完成日")]
    public DateOnly? CompletedDate { get; set; }

    [Comment("此事項的書面通知期限（遲延後幾個工作日內）；null＝沿用合約約定")]
    public int? NoticeWorkdays { get; set; }

    public CoopStatus Status { get; set; } = CoopStatus.Pending;

    [Comment("最近一次內部提醒時間（每個台灣日最多一次）")]
    public DateTimeOffset? LastReminderAt { get; set; }

    public bool IsDeleted { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
    public List<ContractExtensionNotice> Notices { get; set; } = [];
}

/// <summary>協力遲延與工期展延通知書（寄給甲方的書面證據；寄出版 PDF 存 FMS）。</summary>
[DisplayName("合約展延通知書")]
[Comment("協力遲延與工期展延通知書：寄送對象、方式與展延工作日")]
public class ContractExtensionNotice
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid CoopItemId { get; set; }

    [Comment("合約內流水號")]
    public int Number { get; set; }

    [Comment("寄出當下的累計遲延工作日")]
    public int DelayWorkdays { get; set; }

    [Comment("通知甲方順延的工作日（登記為工期展延）")]
    public int ExtensionWorkdays { get; set; }

    [Comment("寄出時的主旨（可由 PM 編輯）")]
    public string? Subject { get; set; }

    [Comment("寄出時的內文（可由 PM 編輯；與 PDF／Email 內容一致，供稽核回溯）")]
    public string? BodyText { get; set; }

    [Comment("寄出時的結語敬語（例：順頌商祺；可由 PM 編輯）")]
    public string? Closing { get; set; }

    [Comment("寄出時的署名（公司、職稱、姓名；可由 PM 編輯）")]
    public string? Signature { get; set; }

    [Comment("請甲方提供的期限")]
    public DateOnly RequestedBy { get; set; }

    public NoticeMode Mode { get; set; }

    [Comment("收件人與副本摘要（僅 Email 模式）")]
    public string? SentTo { get; set; }

    public string? Note { get; set; }

    public Guid? PdfFileId { get; set; }
    public string? PdfFileName { get; set; }

    public Guid SentBy { get; set; }
    public string SentByName { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }

    public Contract? Contract { get; set; }
    public ContractCoopItem? CoopItem { get; set; }
}

/// <summary>文件審查回合：針對附件一中需甲方簽認的文件，記送審日、回覆期限與結果（7 個工作日、彙整修改 2 次）。</summary>
[DisplayName("合約文件審查回合")]
[Comment("文件審查回合：送審日、回覆期限、結果；逾期可擬制通過")]
public class ContractReviewRound : IAuditable
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid DeliverableItemId { get; set; }

    [Comment("第幾次送審（同一文件）")]
    public int RoundNo { get; set; }

    public DateOnly SubmittedDate { get; set; }

    [Comment("甲方回覆期限；合約未約定期限時為 null")]
    public DateOnly? ResponseDue { get; set; }

    public ReviewOutcome Outcome { get; set; } = ReviewOutcome.Pending;

    public DateOnly? RespondedDate { get; set; }

    [Comment("甲方意見摘要或備註")]
    public string? Note { get; set; }

    [Comment("彙整修改次數超過合約約定（超出部分須走 CR 另行計費）")]
    public bool ExceedsRoundLimit { get; set; }

    public DateTimeOffset? LastReminderAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
    public ContractDeliverableItem? DeliverableItem { get; set; }
}

/// <summary>驗收與擬制驗收（每份合約一筆）：交付日起算 N 個工作日，期限內未提具體瑕疵清單即視為通過。</summary>
[DisplayName("合約驗收")]
[Comment("驗收進度：交付日、擬制驗收期限、瑕疵清單與驗收完成日")]
public class ContractAcceptance : IAuditable
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }

    public AcceptanceStatus Status { get; set; } = AcceptanceStatus.NotDelivered;

    public DateOnly? DeliveredDate { get; set; }

    [Comment("擬制驗收期限：交付日起算合約約定工作日；合約無擬制驗收條款時為 null")]
    public DateOnly? DeemedDue { get; set; }

    public DateOnly? DefectReportedDate { get; set; }

    [Comment("甲方具體瑕疵清單摘要")]
    public string? DefectNote { get; set; }

    [Comment("驗收完成日（雙方簽認日或擬制驗收日）")]
    public DateOnly? AcceptedDate { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset? LastReminderAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
}

/// <summary>合約變更：CR 核准自動掛帳或手動登錄（追加金額、順延交付日、保固延長）。簽約後的條款異動只經此留痕。</summary>
[DisplayName("合約變更")]
[Comment("合約變更：追加金額、新交付日與保固延長")]
public class ContractAmendment : IAuditable
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }

    public AmendmentSource Source { get; set; }

    [Comment("來源 CR（自動掛帳時；同一張 CR 只掛帳一次）")]
    public Guid? ChangeRequestId { get; set; }

    [Comment("來源 CR 單號")]
    public string? CrNo { get; set; }

    public string Title { get; set; } = string.Empty;

    [Comment("追加（正）或減少（負）金額，新台幣未稅")]
    public decimal AmountDelta { get; set; }

    [Comment("變更後的新交付／驗收日（無變更為 null）")]
    public DateOnly? NewDeliveryDate { get; set; }

    [Comment("保固延長月數（可為負）")]
    public int WarrantyMonthsDelta { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public string? Note { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
}

/// <summary>付款期程：期別、比例、觸發條件與請款狀態（尾款僅在驗收完成後可請款）。</summary>
[DisplayName("合約付款期程")]
[Comment("付款期程：比例、觸發條件、請款與收款狀態")]
public class ContractPaymentTerm : IAuditable
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public int SortOrder { get; set; }

    public string Name { get; set; } = string.Empty;

    [Comment("占合約現行金額的比例（%）")]
    public decimal Percent { get; set; }

    public PaymentTrigger Trigger { get; set; }

    [Comment("預計請款日")]
    public DateOnly? PlannedDate { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public string? InvoiceNo { get; set; }
    public DateOnly? InvoicedDate { get; set; }
    public DateOnly? PaidDate { get; set; }

    [Comment("請款時的金額（當時現行金額 × 比例），作為開立依據留存")]
    public decimal? InvoicedAmount { get; set; }

    public DateTimeOffset? LastReminderAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
}
