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
    DateTimeOffset? UpdatedAtUtc);

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
    DateTimeOffset? TranscribedAtUtc);

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
