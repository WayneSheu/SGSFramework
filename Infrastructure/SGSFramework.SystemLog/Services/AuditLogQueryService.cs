#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Paginations;
using SGSFramework.SystemLog.DTOs;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>
    /// 資料日誌（core.AuditLogs）查詢服務介面：供稽核人員檢視資料新 / 舊值異動紀錄
    /// </summary>
    public interface IAuditLogQueryService
    {
        Task<PagedResult<AuditLogListItemDto>> GetAuditLogsAsync(AuditLogQueryRequest request, CancellationToken cancellationToken = default);

        Task<AuditLogDetailDto?> GetAuditLogByIdAsync(long id, string? source, CancellationToken cancellationToken = default);

        /// <summary>取得指定來源資料日誌中出現過的資料表名稱清單（供前端下拉選單）</summary>
        Task<IReadOnlyList<string>> GetTableNamesAsync(string? source, CancellationToken cancellationToken = default);

        /// <summary>列出可查詢的資料日誌來源（主系統與各外掛模組）</summary>
        IReadOnlyList<AuditLogSourceDto> GetSources();
    }

    public class AuditLogQueryService : IAuditLogQueryService
    {
        private readonly IAuditLogSourceResolver _sources;
        private readonly ILogger<AuditLogQueryService> _logger;

        public AuditLogQueryService(IAuditLogSourceResolver sources, ILogger<AuditLogQueryService> logger)
        {
            _sources = sources ?? throw new ArgumentNullException(nameof(sources));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PagedResult<AuditLogListItemDto>> GetAuditLogsAsync(AuditLogQueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var query = GetQueryableOrThrow(request.Filter?.Source);
                var filter = request.Filter;

                if (filter is not null)
                {
                    if (!string.IsNullOrWhiteSpace(filter.TableName))
                    {
                        var tableName = filter.TableName.Trim();
                        query = query.Where(x => x.TableName.Contains(tableName));
                    }

                    if (!string.IsNullOrWhiteSpace(filter.Action))
                    {
                        var action = filter.Action.Trim();
                        query = query.Where(x => x.Action == action);
                    }

                    if (!string.IsNullOrWhiteSpace(filter.UserId))
                    {
                        var userId = filter.UserId.Trim();
                        query = query.Where(x => x.UserId == userId);
                    }

                    if (!string.IsNullOrWhiteSpace(filter.TraceId))
                    {
                        // 寫入時 TraceId 已去除 '-'，查詢時同步正規化；前綴比對可利用索引
                        var traceId = filter.TraceId.Replace("-", string.Empty).Trim();
                        query = query.Where(x => x.TraceId.StartsWith(traceId));
                    }

                    if (!string.IsNullOrWhiteSpace(filter.KeyValue))
                    {
                        var keyValue = filter.KeyValue.Trim();
                        query = query.Where(x => x.KeyValues != null && x.KeyValues.Contains(keyValue));
                    }

                    if (!string.IsNullOrWhiteSpace(filter.ChangedColumn))
                    {
                        var column = filter.ChangedColumn.Trim();
                        query = query.Where(x => x.ChangedColumns != null && x.ChangedColumns.Contains(column));
                    }

                    if (filter.StartTimestamp.HasValue)
                        query = query.Where(x => x.Timestamp >= filter.StartTimestamp.Value);

                    if (filter.EndTimestamp.HasValue)
                        query = query.Where(x => x.Timestamp <= filter.EndTimestamp.Value);
                }

                var sortContext = new SortStrategyContext<AuditLogEntity>()
                    .Register("Timestamp", new ExpressionSortStrategy<AuditLogEntity>(x => x.Timestamp))
                    .Register("TableName", new ExpressionSortStrategy<AuditLogEntity>(x => x.TableName))
                    .Register("Action", new ExpressionSortStrategy<AuditLogEntity>(x => x.Action))
                    .Register("Id", new ExpressionSortStrategy<AuditLogEntity>(x => x.Id));

                var sorts = request.Sorts?.Count > 0
                    ? request.Sorts
                    : new[] { new SortCriteria { SortBy = "Timestamp", Direction = SortDirection.Descending } };

                var parameters = new PagedQueryParameters<object>
                {
                    Type = request.Type,
                    PageIndex = request.PageIndex,
                    PageSize = request.PageSize,
                    Sorts = sorts,
                    LastSortValue = request.LastSortValue,
                    LastIdValue = request.LastIdValue
                };

                var paged = await query.ToPagedResultAsync(
                    parameters: parameters,
                    sortContext: sortContext,
                    idSelector: x => x.Id,
                    cancellationToken: cancellationToken);

                var items = paged.Items.Select(e => new AuditLogListItemDto(
                    e.Id, e.Timestamp, e.Schema, e.TableName, e.Action, e.KeyValues, e.ChangedColumns,
                    e.UserId, e.RemoteIp, e.TraceId, e.IsRepaired)).ToList();

                return new PagedResult<AuditLogListItemDto>(items, paged.TotalCount, paged.PageIndex, paged.PageSize);
            }
            catch (Exception ex) when (ex is not (OperationCanceledException or ArgumentException))
            {
                _logger.LogError(ex, "查詢資料日誌清單時發生未預期的內核錯誤。");
                throw new InvalidOperationException("查詢資料日誌清單失敗。", ex);
            }
        }

        public async Task<AuditLogDetailDto?> GetAuditLogByIdAsync(long id, string? source, CancellationToken cancellationToken = default)
        {
            try
            {
                // 主鍵為 (Id, CreatedAt) 複合鍵，Id 本身為 IDENTITY 唯一，直接以 Id 查詢
                var e = await GetQueryableOrThrow(source).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (e is null) return null;

                return new AuditLogDetailDto(
                    e.Id, e.Timestamp, e.CreatedAt, e.Schema, e.TableName, e.Action, e.KeyValues,
                    e.OldValues, e.NewValues, e.ChangedColumns, e.UserId, e.RemoteIp, e.TraceId,
                    e.PreviousHash, e.StoredHash, e.IsRepaired, e.RepairedAt, e.GapReason);
            }
            catch (Exception ex) when (ex is not (OperationCanceledException or ArgumentException))
            {
                _logger.LogError(ex, "依識別碼 ({Id}) 檢索資料日誌時發生異常。", id);
                throw new InvalidOperationException("檢索資料日誌失敗。", ex);
            }
        }

        public async Task<IReadOnlyList<string>> GetTableNamesAsync(string? source, CancellationToken cancellationToken = default)
        {
            try
            {
                return await GetQueryableOrThrow(source)
                    .Select(x => x.TableName)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not (OperationCanceledException or ArgumentException))
            {
                _logger.LogError(ex, "取得資料日誌資料表清單時發生異常。");
                throw new InvalidOperationException("取得資料表清單失敗。", ex);
            }
        }

        public IReadOnlyList<AuditLogSourceDto> GetSources() => _sources.GetSources();

        private IQueryable<AuditLogEntity> GetQueryableOrThrow(string? source) =>
            _sources.GetQueryable(source)
            ?? throw new ArgumentException($"找不到資料日誌來源：{source}", nameof(source));
    }
}
