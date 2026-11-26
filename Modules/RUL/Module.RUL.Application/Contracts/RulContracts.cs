using SGS.Modules.RUL.Infrastructure.Entities;
using SGSFramework.Core.Rules;

namespace SGS.Modules.RUL.Application.Contracts;

public sealed record RulError(string Code, int Status, string Title, string Detail)
{
    public static RulError Validation(string detail, string code = "RUL.VALIDATION_FAILED") => new(code, 400, "Invalid Request", detail);
    public static RulError NotFound(string detail = "資源不存在。") => new("RUL.NOT_FOUND", 404, "Resource Not Found", detail);
    public static RulError Forbidden(string detail, string code = "RUL.FORBIDDEN") => new(code, 403, "Forbidden", detail);
    public static RulError Conflict(string code, string detail) => new(code, 409, "Conflict", detail);
}

public sealed class RulResult<T>
{
    private RulResult(T? value, RulError? error) { Value = value; Error = error; }
    public bool IsSuccess => Error is null;
    public T? Value { get; }
    public RulError? Error { get; }
    public static RulResult<T> Success(T value) => new(value, null);
    public static RulResult<T> Failure(RulError error) => new(default, error);
}

public sealed record PagedList<T>(IReadOnlyList<T> Items, int Total);

/// <summary>規則內容（提議／顯示共用）。</summary>
public sealed record RuleInput
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public RuleDomain Domain { get; init; }
    public RuleKind Kind { get; init; }
    public string TargetEntity { get; init; } = string.Empty;
    public string? FieldName { get; init; }
    public string RoleName { get; init; } = "GlobalPolicy";
    public string ConditionExpr { get; init; } = string.Empty;
    public string? ActionExpr { get; init; }
    public string? OutputPayload { get; init; }
    public FieldAccessLevel ReadAccess { get; init; }
    public FieldAccessLevel WriteAccess { get; init; }
    public RiskDecision? Risk { get; init; }
    public int Priority { get; init; }
    public DateTimeOffset? EffectiveFromUtc { get; init; }
    public DateTimeOffset? EffectiveToUtc { get; init; }
}

public sealed record RuleDto(long Id, RuleInput Content, RuleStatus Status, int Version, string CreatedBy, DateTimeOffset CreatedAtUtc, string? UpdatedBy, DateTimeOffset? UpdatedAtUtc);

public sealed record RuleSearchRequest
{
    public string? Keyword { get; init; }
    public RuleDomain? Domain { get; init; }
    public RuleStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record SubmitChangeRequest
{
    public long? RuleId { get; init; }
    public RuleChangeType ChangeType { get; init; }
    public RuleInput? Proposed { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record ChangeDto(long Id, long? RuleId, RuleChangeType ChangeType, RuleInput? Proposed, string Reason, RuleChangeStatus Status,
    string RequestedBy, DateTimeOffset RequestedAtUtc, string? ReviewedBy, DateTimeOffset? ReviewedAtUtc, string? ReviewComment);

public sealed record ChangeSearchRequest
{
    public RuleChangeStatus? Status { get; init; }
    public long? RuleId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record ReviewRequest(string? Comment);

public sealed record RuleVersionDto(long Id, long RuleId, int Version, RuleInput Content, string ApprovedBy, DateTimeOffset CreatedAtUtc);

public sealed record RollbackRequest(long RuleId, int Version, string Reason);

public sealed record ValidateExpressionRequest(string TargetEntity, RuleKind Kind, string ConditionExpr, string? ActionExpr);

public sealed record ValidateExpressionResult(bool IsValid, string? Error);

public sealed record DryRunRequest
{
    public RuleInput Rule { get; init; } = new();
    public string? UserJson { get; init; }
    public string TargetJson { get; init; } = "{}";
}

public sealed record TestCaseInput(string Name, string? UserJson, string TargetJson, bool ExpectedMatch);

public sealed record TestCaseDto(long Id, long RuleId, string Name, string UserJson, string TargetJson, bool ExpectedMatch);

public sealed record TestCaseRunDto(long TestCaseId, string Name, bool Passed, bool Matched, bool ExpectedMatch, string? Error);

public sealed record RuleTargetDto(string Name, string Description, IReadOnlyList<string> Fields);
