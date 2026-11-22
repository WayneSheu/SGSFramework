using SGS.Modules.MTG.Infrastructure.Entities;

namespace SGS.Modules.MTG.Application.Contracts;

/// <summary>業務錯誤（映射為 ProblemDetails）。</summary>
public sealed record MtgError(int Status, string Code, string Title, string Detail);

/// <summary>服務結果：成功帶值，失敗帶 <see cref="MtgError"/>。</summary>
public sealed record MtgResult<T>(bool IsSuccess, T? Value, MtgError? Error)
{
    public static MtgResult<T> Ok(T value) => new(true, value, null);
    public static MtgResult<T> Fail(MtgError error) => new(false, default, error);
}

public static class MtgErrors
{
    public static MtgError NotFound(string what) => new(404, "MTG_NOT_FOUND", "找不到資料", $"找不到{what}。");
    public static MtgError Validation(string detail) => new(400, "MTG_VALIDATION", "資料驗證失敗", detail);
    public static MtgError Conflict(string detail) => new(409, "MTG_CONFLICT", "狀態衝突", detail);
    public static MtgError Disabled() => new(503, "MTG_DISABLED", "AI 紀要未啟用", "尚未啟用會議助理的 AI 紀要功能（Meeting:Enabled）。");
    public static MtgError QueueFull() => new(429, "MTG_QUEUE_FULL", "處理佇列已滿", "目前待處理的紀要作業過多，請稍後再試。");
    public static MtgError WhisperDisabled() => new(503, "MTG_WHISPER_DISABLED", "音檔轉錄未啟用", "尚未啟用語音轉錄（Meeting:Whisper:Enabled）。");
    public static MtgError TranscriptionQueueFull() => new(429, "MTG_QUEUE_FULL", "轉錄佇列已滿", "目前待轉錄的音檔過多，請稍後再試。");
    public static MtgError MailUnavailable() => new(503, "MTG_MAIL_UNAVAILABLE", "郵件服務未啟用", "尚未設定郵件（SMTP）或郵件功能已停用，無法寄送通知。");
    public static MtgError Gone(string detail) => new(410, "MTG_GONE", "資源已不存在", detail);
    public static MtgError FileRejected(int status, string detail) => new(status, "MTG_FILE_REJECTED", "音檔被拒絕", detail);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageIndex, int PageSize);

// ---- 會議 ----

public sealed record MeetingQueryRequest(
    string? Keyword = null,
    MeetingStatus? Status = null,
    string? ScenarioCode = null,
    Guid? ProjectId = null,
    int PageIndex = 1,
    int PageSize = 20);

public sealed record SaveMeetingRequest(
    string Title,
    string? ScenarioCode,
    DateTimeOffset? MeetingTimeUtc,
    Guid? ProjectId);

public sealed record MeetingDto(
    Guid Id,
    string Title,
    string ScenarioCode,
    DateTimeOffset MeetingTimeUtc,
    Guid? ProjectId,
    MeetingStatus Status,
    string? StatusMessage,
    string Organizer,
    int SegmentCount,
    int MinutesVersionCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    string? ProjectName = null);

// ---- 逐字稿 ----

/// <summary>貼上逐字稿；Replace=true 時覆蓋既有全部片段，否則附加在最後。</summary>
public sealed record PasteTranscriptRequest(string Text, bool Replace = false);

public sealed record TranscriptSegmentDto(
    int Seq,
    string? Speaker,
    double? StartSeconds,
    double? EndSeconds,
    string Text,
    string? CorrectedText);

public sealed record PasteTranscriptResult(int AddedSegments, int TotalSegments, int CorrectedSegments);

// ---- 音檔 ----

public sealed record MeetingAudioDto(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSize,
    AudioStatus Status,
    string? StatusMessage,
    double? DurationSeconds,
    string? Language,
    int SegmentCount,
    bool AutoSummarize,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? TranscribedAtUtc,
    DateTimeOffset? ExpiredAtUtc = null);

// ---- 桌面端同步 ----

/// <summary>桌面端上傳的一個片段；<see cref="ClientSegmentId"/> 由桌面端產生，重送時用來避免重複寫入。</summary>
public sealed record ClientSegmentInput(string ClientSegmentId, string? Speaker, double? StartSeconds, double? EndSeconds, string Text);

public sealed record UploadSegmentsRequest(IReadOnlyList<ClientSegmentInput> Segments);

public sealed record UploadSegmentsResult(int Added, int Skipped, int TotalSegments);

/// <summary>結束會議：<see cref="GenerateMinutes"/> 為 true 時接著要求產生紀要（場景空白＝沿用會議場景）。</summary>
public sealed record FinalizeMeetingRequest(string? ScenarioCode, bool GenerateMinutes = true);

/// <summary>術語（供桌面端快取，於本機做同音字校正）。</summary>
public sealed record GlossaryTermDto(string Term, IReadOnlyList<string> Variants);

// ---- 紀要 ----

/// <summary>要求產生紀要；ScenarioCode 為空時沿用會議的場景。</summary>
public sealed record GenerateMinutesRequest(string? ScenarioCode);

public sealed record MinutesDto(
    int Version,
    string ScenarioCode,
    string Markdown,
    string Model,
    DateTimeOffset GeneratedAtUtc,
    string GeneratedBy);

public sealed record MinutesSummaryDto(int Version, string ScenarioCode, string Model, DateTimeOffset GeneratedAtUtc, string GeneratedBy);

// ---- 場景模板 ----

public sealed record PromptTemplateDto(
    Guid Id,
    string ScenarioCode,
    string DisplayName,
    string MapPrompt,
    string ReducePrompt,
    bool IsActive,
    int Version);

public sealed record SavePromptTemplateRequest(string DisplayName, string MapPrompt, string ReducePrompt, bool IsActive);

// ---- 術語表 ----

public sealed record GlossaryQueryRequest(string? Keyword = null, bool? IsActive = null, int PageIndex = 1, int PageSize = 50);

public sealed record SaveGlossaryEntryRequest(string Term, string? Variants, string? Category, bool IsActive);

public sealed record GlossaryEntryDto(Guid Id, string Term, string? Variants, string? Category, bool IsActive);

// ---- 待辦事項 ----

public sealed record ActionItemDto(
    Guid Id,
    Guid MeetingId,
    int Seq,
    string Description,
    string? Owner,
    string? OwnerEmail,
    DateOnly? DueDate,
    string? DueText,
    ActionItemStatus Status,
    string? Note,
    ActionItemSource Source,
    int? MinutesVersion,
    DateTimeOffset? NotifiedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    Guid? PmsTaskId = null,
    DateTimeOffset? PmsSyncedAtUtc = null);

/// <summary>跨會議的待辦清單列（多帶會議標題）。</summary>
public sealed record ActionItemListDto(
    Guid Id,
    Guid MeetingId,
    string MeetingTitle,
    int Seq,
    string Description,
    string? Owner,
    DateOnly? DueDate,
    string? DueText,
    ActionItemStatus Status,
    bool IsOverdue,
    DateTimeOffset? NotifiedAtUtc);

public sealed record SaveActionItemRequest(
    string Description,
    string? Owner,
    string? OwnerEmail,
    DateOnly? DueDate,
    string? DueText,
    ActionItemStatus? Status,
    string? Note);

/// <summary>跨會議查詢待辦（POST search）。Keyword 比對事項內容與負責人；OverdueOnly 只看逾期且未完成者。</summary>
public sealed record ActionItemQueryRequest(
    string? Keyword = null,
    ActionItemStatus? Status = null,
    bool OverdueOnly = false,
    Guid? MeetingId = null,
    DateOnly? DueFrom = null,
    DateOnly? DueTo = null,
    int PageIndex = 1,
    int PageSize = 20);

public sealed record ExtractActionItemsResult(int Added, int Replaced, int Kept, int Total, string Method);

/// <summary>通知待辦：ItemIds 為空代表所有「未完成且有負責人信箱」者；ExtraRecipients 另外收到一封完整清單（例如主管或會議群組）。</summary>
public sealed record NotifyActionItemsRequest(IReadOnlyList<Guid>? ItemIds, IReadOnlyList<string>? ExtraRecipients);

public sealed record NotifyActionItemsResult(int ItemsNotified, int OwnerMails, int SummaryMails, int SkippedNoEmail, IReadOnlyList<string> Failures);

// ---- PMS 專案關聯 ----

public sealed record ProjectOptionDto(Guid Id, string Code, string Name);

/// <summary>目前使用者在 PMS 的可見範圍：只能關聯自己看得到的專案（成員，或具 PMS.PROJECT.ADMIN）。</summary>
public sealed record ProjectScope(Guid UserId, bool ViewAll);

// ---- M6：音檔過期清除與 PMS 回寫 ----

/// <summary>過期音檔清除結果。<see cref="Candidates"/> 是本次符合條件的錄音數（單次最多處理 200 筆，其餘下次再清）。</summary>
public sealed record AudioPurgeResult(int RetentionDays, int Candidates, int Expired, int Failed, string Message);

/// <summary>回寫 PMS：ItemIds 為空代表所有「尚未回寫、未完成」的待辦。</summary>
public sealed record SyncPmsRequest(IReadOnlyList<Guid>? ItemIds);

public sealed record SyncPmsResult(int Created, int AlreadyLinked, int Failed, IReadOnlyList<string> Failures);
