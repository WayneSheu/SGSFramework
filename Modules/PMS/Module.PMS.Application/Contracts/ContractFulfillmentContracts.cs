using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Contracts;

/// <summary>協力事項（含遲延與通知期限的計算結果）。</summary>
public sealed record CoopItemDto(
    Guid Id, string Title, CoopKind Kind, string? Description, DateOnly DueDate, DateOnly? CompletedDate, CoopStatus Status,
    CoopState State, int DelayWorkdays, DateOnly NoticeDeadline, int NoticeCount, int LatestExtensionWorkdays, bool NeedsNotice, string RowVersion,
    int NoticeWorkdays = 3, int? NoticeWorkdaysOverride = null);

public sealed record ExtensionNoticeDto(
    Guid Id, int Number, Guid CoopItemId, string CoopTitle, int DelayWorkdays, int ExtensionWorkdays, DateOnly RequestedBy, NoticeMode Mode,
    string? SentTo, string? Note, bool HasPdf, string SentByName, DateTimeOffset SentAt, string? Subject = null, string? BodyText = null,
    string? Closing = null, string? Signature = null);

public sealed record ReviewRoundDto(
    Guid Id, Guid DeliverableItemId, string DeliverableName, int RoundNo, DateOnly SubmittedDate, DateOnly? ResponseDue, ReviewOutcome Outcome,
    DateOnly? RespondedDate, string? Note, bool ExceedsRoundLimit, bool IsOverdue);

/// <summary>需甲方簽認的交付項目與其審查回合彙總（畫面逐項顯示、可送審）。</summary>
public sealed record ReviewTrackDto(
    Guid DeliverableItemId, string Name, string Stage, int RoundsUsed, int? MaxRounds, ReviewOutcome? LastOutcome, bool CanSubmitNext);

public sealed record AcceptanceDto(
    AcceptanceStatus Status, DateOnly? DeliveredDate, DateOnly? DeemedDue, DateOnly? DefectReportedDate, string? DefectNote, DateOnly? AcceptedDate,
    string? Note, int? DaysLeft, string RowVersion);

public sealed record AmendmentDto(
    Guid Id, AmendmentSource Source, string? CrNo, string Title, decimal AmountDelta, DateOnly? NewDeliveryDate, int WarrantyMonthsDelta,
    DateOnly EffectiveDate, string? Note, string CreatedBy, DateTimeOffset CreatedAtUtc);

public sealed record PaymentTermDto(
    Guid Id, int SortOrder, string Name, decimal Percent, decimal Amount, PaymentTrigger Trigger, DateOnly? PlannedDate, PaymentStatus Status,
    string? InvoiceNo, DateOnly? InvoicedDate, DateOnly? PaidDate, decimal? InvoicedAmount, bool CanRelease, bool CanInvoice, bool CanMarkPaid);

/// <summary>金額與交期總覽：原價＋變更＝現行金額；原交期→變更後→含協力展延的現行交期。</summary>
public sealed record FulfillmentSummaryDto(
    decimal OriginalAmount, decimal AmendmentTotal, decimal CurrentAmount, decimal InvoicedAmount, decimal PaidAmount,
    DateOnly? OriginalDelivery, DateOnly? AmendedDelivery, int ExtensionWorkdays, DateOnly? EffectiveDelivery,
    int OriginalWarrantyMonths, int CurrentWarrantyMonths, DateOnly? WarrantyEnd);

public sealed record FulfillmentActionsDto(bool CanManage, bool CanDeliver, bool CanReportDefects, bool CanConfirm, bool CanEditPayments, bool Active);

public sealed record FulfillmentDto(
    FulfillmentSummaryDto Summary, IReadOnlyList<CoopItemDto> CoopItems, IReadOnlyList<ExtensionNoticeDto> Notices,
    IReadOnlyList<ReviewTrackDto> ReviewTracks, IReadOnlyList<ReviewRoundDto> ReviewRounds, AcceptanceDto Acceptance,
    IReadOnlyList<AmendmentDto> Amendments, IReadOnlyList<PaymentTermDto> Payments, FulfillmentActionsDto Actions);

/// <param name="NoticeWorkdays">此事項的書面通知期限（遲延後幾個工作日內，1~30）；null＝沿用合約約定。</param>
public sealed record CoopItemRequest(string Title, CoopKind Kind, string? Description, DateOnly DueDate, string? RowVersion = null, int? NoticeWorkdays = null);

public sealed record CompleteCoopRequest(DateOnly CompletedDate, string RowVersion);

/// <param name="Mode">Email＝系統代寄（附正式 PDF）；RecordOnly＝PM 已自行寄出，僅記錄。</param>
/// <param name="Subject">通知書主旨（可編輯；空白＝預設）。</param>
/// <param name="BodyText">通知書內文（可編輯，段落以空白行分隔；空白＝預設）。</param>
public sealed record SendNoticeRequest(
    NoticeMode Mode, int ExtensionWorkdays, DateOnly RequestedBy, IReadOnlyList<string>? To, IReadOnlyList<string>? Cc, string? Message, string? Note,
    string? Subject = null, string? BodyText = null, string? Closing = null, string? Signature = null);

public sealed record NoticePreviewRequest(
    int? ExtensionWorkdays, DateOnly? RequestedBy, string? Subject = null, string? BodyText = null, string? Closing = null, string? Signature = null);

/// <summary>通知書預設內容（依目前遲延、順延與請甲方提供期限產生，供編輯器帶入）。</summary>
public sealed record NoticeDefaultsDto(
    string Subject, string BodyText, int DelayWorkdays, int ExtensionWorkdays, DateOnly RequestedBy, DateOnly? EffectiveDelivery, DateOnly NoticeDeadline,
    string Closing = "順頌商祺", string Signature = "");

public sealed record SubmitReviewRoundRequest(Guid DeliverableItemId, DateOnly? SubmittedDate);

public sealed record RespondReviewRoundRequest(ReviewOutcome Outcome, DateOnly? RespondedDate, string? Note);

public sealed record DeliverRequest(DateOnly DeliveredDate, string? Note);

public sealed record ReportDefectsRequest(DateOnly ReportedDate, string Note);

public sealed record ConfirmAcceptanceRequest(DateOnly AcceptedDate, string? Note);

public sealed record CreateAmendmentRequest(
    string Title, decimal AmountDelta, DateOnly? NewDeliveryDate, int WarrantyMonthsDelta, DateOnly? EffectiveDate, string? Note);

public sealed record PaymentTermRequest(string Name, decimal Percent, PaymentTrigger Trigger, DateOnly? PlannedDate);

public sealed record SetPaymentsRequest(IReadOnlyList<PaymentTermRequest> Terms);

public sealed record MovePaymentRequest(PaymentStatus Status, string? InvoiceNo, DateOnly? Date);

/// <summary>每日監看結果（排程呼叫）。</summary>
public sealed record ContractWatchResult(
    int CoopReminded, int ReviewsDeemed, int AcceptancesDeemed, int PaymentsPromoted, int PaymentsReminded, int Skipped, string Message);

/// <summary>展延通知書 PDF／下載檔案。</summary>
public sealed record NoticePdfFile(byte[] Content, string FileName);
