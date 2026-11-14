using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Contracts;

// ===== 結果與錯誤 =====

/// <summary>PMS 業務錯誤（對應 API 規格 1.5 的 code 與 HTTP 狀態）。</summary>
public sealed record PmsError(string Code, int Status, string Title, string Detail)
{
    public static PmsError Validation(string detail, string code = "PMS.VALIDATION_FAILED") => new(code, 400, "Invalid Request", detail);
    /// <summary>找不到或無權限可見：一律 404，避免洩漏資源是否存在（防 IDOR）。</summary>
    public static PmsError NotFound(string detail = "資源不存在。") => new("PMS.NOT_FOUND", 404, "Resource Not Found", detail);
    public static PmsError Forbidden(string detail) => new("PMS.FORBIDDEN", 403, "Forbidden", detail);
    public static PmsError Conflict(string code, string detail) => new(code, 409, "Conflict", detail);
    public static PmsError Unprocessable(string code, string detail) => new(code, 422, "Unprocessable Entity", detail);
}

/// <summary>操作結果（Result Pattern）。</summary>
public sealed class PmsResult<T>
{
    private PmsResult(T? value, PmsError? error) { Value = value; Error = error; }

    public T? Value { get; }
    public PmsError? Error { get; }
    public bool IsSuccess => Error is null;

    public static PmsResult<T> Success(T value) => new(value, null);
    public static PmsResult<T> Failure(PmsError error) => new(default, error);

    public static implicit operator PmsResult<T>(PmsError error) => Failure(error);
}

// ===== 專案 =====

public sealed record ProjectSearchFilter
{
    public ProjectStatus? Status { get; init; }
    public ProjectStage? Stage { get; init; }
    public ProjectPhase? Phase { get; init; }
    public bool MineOnly { get; init; }
}

public sealed record ProjectSearchRequest : PagedQueryParameters<ProjectSearchFilter>;

public sealed record ProjectListDto(
    Guid Id, string Code, string Name, ProjectPhase Phase, ProjectStage Stage, ProjectStatus Status,
    Guid PmUserId, Guid TechLeadUserId, DateOnly? FreezeLineDate);

public sealed record ProjectMemberDto(Guid MemberId, Guid UserId, RaciRole RaciRole);

public sealed record PmsModuleDto(Guid Id, string Code, string Name);

public sealed record ProjectGateDto(GateKind Gate, GateStatus Status, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? EvidenceNote);

public sealed record ProjectDetailDto(
    Guid Id, string Code, string Name, ProjectPhase Phase, ProjectStage Stage, ProjectStatus Status,
    Guid PmUserId, Guid TechLeadUserId, DateOnly? FreezeLineDate, string? ContractSlaNote,
    IReadOnlyList<ProjectMemberDto> Members, IReadOnlyList<PmsModuleDto> Modules, IReadOnlyList<ProjectGateDto> Gates,
    string RowVersion);

public sealed record CreateProjectRequest(
    string Code, string Name, ProjectStage Stage, Guid PmUserId, Guid TechLeadUserId,
    DateOnly? FreezeLineDate, string? ContractSlaNote);

public sealed record UpdateProjectRequest(
    string Name, ProjectStage Stage, DateOnly? FreezeLineDate, string? ContractSlaNote, string RowVersion,
    /// <summary>若帶入且與現值不同，回 422 PMS.PROJECT_CODE_IMMUTABLE。</summary>
    string? Code = null);

public sealed record AdvancePhaseRequest(ProjectPhase TargetPhase, string RowVersion);

public sealed record AddMemberRequest(Guid UserId, RaciRole RaciRole);

public sealed record AddModuleRequest(string Code, string Name);

public sealed record DecideGateRequest(GateStatus Result, string? EvidenceNote);

public sealed record ProjectCodeCheckDto(string Code, bool Available, string? Reason);

public sealed record ProjectCodeSuggestionDto(IReadOnlyList<string> Suggestions);

// ===== 交付物 =====

public sealed record NamingCheckRequest(NamingKind Kind, string FileName);

public sealed record NamingCheckDto(bool Valid, IReadOnlyList<string> Violations);

/// <summary>上傳交付物的中繼資料（檔案內容另以串流傳入）。檔名由系統依 SOP 5.5 產生。</summary>
public sealed record UploadDeliverableMeta(
    ProjectPhase Stage, string DocType, Guid? ModuleId, string Version, string? DocName, string? ExpectedSha256);

public sealed record DeliverableDto(
    Guid Id, ProjectPhase Stage, string DocType, string? DocName, string Version,
    string FileName, string? OriginalFileName, long? FileSize, string? ContentType,
    bool HasFile, bool CanPreview, bool IsLatest,
    Guid? ModuleId, string? ModuleCode, string FolderPath, string CreatedBy, DateTimeOffset CreatedAtUtc);

public sealed record DeliverableSearchFilter
{
    public ProjectPhase? Stage { get; init; }
    public string? DocType { get; init; }
    public Guid? ModuleId { get; init; }

    /// <summary>是否包含歷史版本；預設僅列各文件（模組＋類型）的最新版。</summary>
    public bool IncludeHistory { get; init; }
}

public sealed record DeliverableSearchRequest : PagedQueryParameters<DeliverableSearchFilter>;
