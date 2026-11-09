using Microsoft.EntityFrameworkCore;
using SGS.Modules.FMS.Infrastructure.Dbcontexts;
using SGS.Modules.FMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.FMS.Application.Auditing;

/// <summary>檔案存取稽核查詢條件</summary>
public record FileAccessLogFilter
{
    public Guid? FileId { get; init; }
    public string? Category { get; init; }

    /// <summary>動作（精確比對，例如 Download / View / IntegrityFailure）</summary>
    public string? Action { get; init; }

    public bool? IsSuccess { get; init; }

    /// <summary>僅系統帳號（true）/ 僅人員（false）/ 全部（null）</summary>
    public bool? IsSystemAccount { get; init; }

    /// <summary>使用者識別碼或名稱（包含比對）</summary>
    public string? User { get; init; }

    public string? RemoteIp { get; init; }
    public DateTimeOffset? StartTimestamp { get; init; }
    public DateTimeOffset? EndTimestamp { get; init; }
}

/// <summary>分頁查詢（POST search 慣例，SearchTerm 比對補充說明）</summary>
public record FileAccessLogQueryRequest : PagedQueryParameters<FileAccessLogFilter>;

public sealed record FileAccessLogDto(
    Guid Id,
    Guid? FileId,
    string? Category,
    string Action,
    bool IsSuccess,
    string? UserId,
    string UserName,
    bool IsSystemAccount,
    string? ClientId,
    string? RemoteIp,
    string? ForwardedFor,
    string? UserAgent,
    string? TraceId,
    string? Detail,
    DateTimeOffset OccurredAt);

public interface IFileAccessLogQueryService
{
    Task<PagedResult<FileAccessLogDto>> SearchAsync(FileAccessLogQueryRequest request, CancellationToken cancellationToken = default);
}

public sealed class FileAccessLogQueryService(FMSDbContext dbContext) : IFileAccessLogQueryService
{
    public async Task<PagedResult<FileAccessLogDto>> SearchAsync(FileAccessLogQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.FileAccessLogs.AsNoTracking();
        var filter = request.Filter ?? new FileAccessLogFilter();

        if (filter.FileId.HasValue) query = query.Where(x => x.FileId == filter.FileId);
        if (!string.IsNullOrWhiteSpace(filter.Category)) query = query.Where(x => x.Category == filter.Category);
        if (!string.IsNullOrWhiteSpace(filter.Action)) query = query.Where(x => x.Action == filter.Action);
        if (filter.IsSuccess.HasValue) query = query.Where(x => x.IsSuccess == filter.IsSuccess);
        if (filter.IsSystemAccount.HasValue) query = query.Where(x => x.IsSystemAccount == filter.IsSystemAccount);
        if (!string.IsNullOrWhiteSpace(filter.User))
            query = query.Where(x => x.UserName.Contains(filter.User) || (x.UserId != null && x.UserId.Contains(filter.User)) || (x.ClientId != null && x.ClientId.Contains(filter.User)));
        if (!string.IsNullOrWhiteSpace(filter.RemoteIp)) query = query.Where(x => x.RemoteIp == filter.RemoteIp);
        if (filter.StartTimestamp.HasValue) query = query.Where(x => x.OccurredAt >= filter.StartTimestamp.Value);
        if (filter.EndTimestamp.HasValue) query = query.Where(x => x.OccurredAt <= filter.EndTimestamp.Value);
        if (!string.IsNullOrWhiteSpace(request.SearchTerm)) query = query.Where(x => x.Detail != null && x.Detail.Contains(request.SearchTerm));

        var sortContext = new SortStrategyContext<FileAccessLog>()
            .Register("OccurredAt", new ExpressionSortStrategy<FileAccessLog>(x => x.OccurredAt))
            .Register("Action", new ExpressionSortStrategy<FileAccessLog>(x => x.Action))
            .Register("UserName", new ExpressionSortStrategy<FileAccessLog>(x => x.UserName))
            .Register("Id", new ExpressionSortStrategy<FileAccessLog>(x => x.Id));

        var parameters = new PagedQueryParameters<object>
        {
            Type = request.Type,
            PageIndex = request.PageIndex,
            PageSize = Math.Clamp(request.PageSize, 1, 200),
            Sorts = request.Sorts?.Count > 0
                ? request.Sorts
                : [new SortCriteria { SortBy = "OccurredAt", Direction = SortDirection.Descending }],
            LastSortValue = request.LastSortValue,
            LastIdValue = request.LastIdValue
        };

        var paged = await query.ToPagedResultAsync(parameters, sortContext, x => x.Id, cancellationToken).ConfigureAwait(false);
        var items = paged.Items.Select(x => new FileAccessLogDto(
            x.Id, x.FileId, x.Category, x.Action, x.IsSuccess, x.UserId, x.UserName, x.IsSystemAccount, x.ClientId,
            x.RemoteIp, x.ForwardedFor, x.UserAgent, x.TraceId, x.Detail, x.OccurredAt)).ToList();

        return new PagedResult<FileAccessLogDto>(items, paged.TotalCount, paged.PageIndex, paged.PageSize);
    }
}
