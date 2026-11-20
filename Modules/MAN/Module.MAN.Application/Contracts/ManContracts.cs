using SGS.Modules.MAN.Infrastructure.Entities;

namespace SGS.Modules.MAN.Application.Contracts;

/// <summary>MAN 業務錯誤（對應 HTTP 狀態與 code）。</summary>
public sealed record ManError(string Code, int Status, string Title, string Detail)
{
    public static ManError Validation(string detail, string code = "MAN.VALIDATION_FAILED") => new(code, 400, "Invalid Request", detail);

    public static ManError NotFound(string detail = "資源不存在。") => new("MAN.NOT_FOUND", 404, "Resource Not Found", detail);

    public static ManError Conflict(string code, string detail) => new(code, 409, "Conflict", detail);
}

/// <summary>操作結果（Result Pattern）。</summary>
public sealed class ManResult<T>
{
    private ManResult(T? value, ManError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public ManError? Error { get; }

    public bool IsSuccess => Error is null;

    public static ManResult<T> Success(T value) => new(value, null);

    public static ManResult<T> Failure(ManError error) => new(default, error);

    public static implicit operator ManResult<T>(ManError error) => Failure(error);
}

/// <summary>分頁查詢（POST search，Request Body 傳遞）。</summary>
public sealed record ManualTopicQueryRequest(
    string? Keyword = null,
    ManualTopicStatus? Status = null,
    string? ModuleName = null,
    int PageIndex = 1,
    int PageSize = 20);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageIndex, int PageSize);

public sealed record ManualTopicDto(
    Guid Id,
    string FeatureKey,
    string Title,
    string? ModuleName,
    string Tooltip,
    string StepsMarkdown,
    string Precautions,
    int SortOrder,
    ManualTopicStatus Status,
    ManualContentSource Source,
    DateTimeOffset? PublishedAtUtc,
    string? PublishedBy,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>建立／更新主題（FeatureKey 建立後不可變更，避免前端綁定失效）。</summary>
public sealed record SaveManualTopicRequest(
    string FeatureKey,
    string Title,
    string? ModuleName,
    string Tooltip,
    string StepsMarkdown,
    string Precautions,
    int SortOrder = 0);

/// <summary>內嵌提示字典（Key＝FeatureKey）與版本戳（供前端快取比對）。</summary>
public sealed record TooltipDictionaryDto(IReadOnlyDictionary<string, string> Items, string Version);

// ===== AI 生成 =====

public sealed record GenerationJobQueryRequest(
    GenerationJobStatus? Status = null,
    int PageIndex = 1,
    int PageSize = 20);

public sealed record GenerationJobDto(
    Guid Id,
    string FeatureKey,
    string Title,
    GenerationJobStatus Status,
    string Model,
    bool Overwrite,
    Guid? TopicId,
    string? Message,
    long? DurationMs,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? FinishedAtUtc);

/// <summary>AI 生成服務狀態：是否啟用、Ollama 是否可連線、視覺模型是否已安裝。</summary>
public sealed record GenerationStatusDto(
    bool Enabled,
    bool OllamaReachable,
    string VisionModel,
    bool VisionModelInstalled,
    int QueueLength,
    string? Message);

/// <summary>送出生成作業（截圖以 multipart 另行傳入，不在此物件中）。</summary>
public sealed record GenerateTopicRequest(
    string FeatureKey,
    string Title,
    string? ModuleName,
    string? ApiKeyword,
    bool Overwrite);
