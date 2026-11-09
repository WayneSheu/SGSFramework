#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Logging;
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
        /// <summary>多來源合併分頁時，每個來源最多取回的筆數（= 頁碼 × 每頁筆數的上限），避免深頁耗盡記憶體</summary>
        private const int MaxMergeDepth = 5000;

        private readonly IAuditLogSourceResolver _sources;
        private readonly ILogger<AuditLogQueryService> _logger;

        private readonly ILogIdentityResolver? _identityResolver;

        public AuditLogQueryService(IAuditLogSourceResolver sources, ILogger<AuditLogQueryService> logger, ILogIdentityResolver? identityResolver = null)
        {
            _identityResolver = identityResolver;
            _sources = sources ?? throw new ArgumentNullException(nameof(sources));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PagedResult<AuditLogListItemDto>> GetAuditLogsAsync(AuditLogQueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var filter = request.Filter;
                var keys = ResolveSourceKeys(filter);
                var loginNames = await ResolveLoginNamesAsync(filter, cancellationToken);

                if (keys.Count > 1)
                {
                    return await GetMergedAsync(request, keys, loginNames, cancellationToken);
                }

                var sourceKey = keys[0];
                var query = ApplyFilter(GetQueryableOrThrow(sourceKey), filter, loginNames);

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

                var items = await ToListItemsAsync(paged.Items.Select(e => (e, sourceKey)).ToList(), cancellationToken);

                return new PagedResult<AuditLogListItemDto>(items, paged.TotalCount, paged.PageIndex, paged.PageSize);
            }
            catch (Exception ex) when (ex is not (OperationCanceledException or ArgumentException))
            {
                _logger.LogError(ex, "查詢資料日誌清單時發生未預期的內核錯誤。");
                throw new InvalidOperationException("查詢資料日誌清單失敗。", ex);
            }
        }

        /// <summary>
        /// 多來源查詢：各來源可能位於不同資料庫（獨立日誌庫 / 主庫各模組 Schema），無法在單一 SQL 合併，
        /// 因此每個來源各取「前 頁碼×每頁筆數」筆並加總筆數，再於記憶體依時間合併後切出該頁。
        /// 僅支援依 Timestamp 排序（預設新到舊）。
        /// </summary>
        private async Task<PagedResult<AuditLogListItemDto>> GetMergedAsync(
            AuditLogQueryRequest request, IReadOnlyList<string?> keys, IReadOnlyCollection<string> loginNames, CancellationToken cancellationToken)
        {
            var pageIndex = Math.Max(request.PageIndex, 1);
            var pageSize = Math.Clamp(request.PageSize, 1, 500);
            var take = Math.Min(pageIndex * pageSize, MaxMergeDepth);
            if ((pageIndex - 1) * pageSize >= MaxMergeDepth)
            {
                throw new ArgumentException($"多來源合併查詢最多檢視前 {MaxMergeDepth} 筆，請縮小時間區間或增加篩選條件。", nameof(request));
            }

            var ascending = request.Sorts?.FirstOrDefault(s => string.Equals(s.SortBy, "Timestamp", StringComparison.OrdinalIgnoreCase))
                ?.Direction == SortDirection.Ascending;

            var total = 0;
            var merged = new List<(AuditLogEntity Entity, string? Source)>();

            foreach (var key in keys)
            {
                var query = ApplyFilter(GetQueryableOrThrow(key), request.Filter, loginNames);
                total += await query.CountAsync(cancellationToken);

                var ordered = ascending
                    ? query.OrderBy(x => x.Timestamp).ThenBy(x => x.Id)
                    : query.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id);

                foreach (var entity in await ordered.Take(take).ToListAsync(cancellationToken))
                {
                    merged.Add((entity, key));
                }
            }

            var page = (ascending
                    ? merged.OrderBy(m => m.Entity.Timestamp).ThenBy(m => m.Entity.Id)
                    : merged.OrderByDescending(m => m.Entity.Timestamp).ThenByDescending(m => m.Entity.Id))
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = await ToListItemsAsync(page, cancellationToken);
            return new PagedResult<AuditLogListItemDto>(items, total, pageIndex, pageSize);
        }

        /// <summary>來源清單：優先使用多選的 Sources；否則為單一 Source（空值 = 預設來源）。去除重複與空白。</summary>
        private static IReadOnlyList<string?> ResolveSourceKeys(AuditLogFilter? filter)
        {
            var keys = (filter?.Sources ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string?>()
                .ToList();

            return keys.Count > 0 ? keys : [filter?.Source];
        }

        private async Task<IReadOnlyCollection<string>> ResolveLoginNamesAsync(AuditLogFilter? filter, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(filter?.UserName) || _identityResolver is null) return Array.Empty<string>();
            return await _identityResolver.FindLoginNamesAsync(filter.UserName.Trim(), 200, cancellationToken);
        }

        private static IQueryable<AuditLogEntity> ApplyFilter(
            IQueryable<AuditLogEntity> query, AuditLogFilter? filter, IReadOnlyCollection<string> loginNames)
        {
            if (filter is null) return query;

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

            if (!string.IsNullOrWhiteSpace(filter.UserName))
            {
                // 操作者欄位記錄登入帳號：先以姓名 / 帳號關鍵字找出符合的帳號，再比對（同時保留對操作者欄位與已存姓名的包含比對）
                var keyword = filter.UserName.Trim();
                var names = loginNames.ToArray();
                query = query.Where(x => (x.UserId != null && (names.Contains(x.UserId) || x.UserId.Contains(keyword)))
                                         || (x.UserName != null && x.UserName.Contains(keyword)));
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

            return query;
        }

        private async Task<List<AuditLogListItemDto>> ToListItemsAsync(
            IReadOnlyList<(AuditLogEntity Entity, string? Source)> rows, CancellationToken cancellationToken)
        {
            var names = await ResolveNamesAsync(rows.Select(r => r.Entity.UserId), cancellationToken);
            return rows.Select(r =>
            {
                var e = r.Entity;
                return new AuditLogListItemDto(
                    e.Id, e.Timestamp, e.Schema, e.TableName, e.Action, e.KeyValues, e.ChangedColumns,
                    e.UserId, e.RemoteIp, e.TraceId, e.IsRepaired,
                    // 優先使用寫入時記錄的姓名（歷史異動當下的姓名），舊資料無此欄時再依帳號換算
                    !string.IsNullOrWhiteSpace(e.UserName) ? e.UserName
                        : e.UserId is not null && names.TryGetValue(e.UserId, out var n) ? n : null,
                    r.Source);
            }).ToList();
        }

        public async Task<AuditLogDetailDto?> GetAuditLogByIdAsync(long id, string? source, CancellationToken cancellationToken = default)
        {
            try
            {
                // 主鍵為 (Id, CreatedAt) 複合鍵，Id 本身為 IDENTITY 唯一，直接以 Id 查詢
                var e = await GetQueryableOrThrow(source).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (e is null) return null;

                var names = await ResolveNamesAsync([e.UserId], cancellationToken);
                return new AuditLogDetailDto(
                    e.Id, e.Timestamp, e.CreatedAt, e.Schema, e.TableName, e.Action, e.KeyValues,
                    e.OldValues, e.NewValues, e.ChangedColumns, e.UserId, e.RemoteIp, e.TraceId,
                    e.PreviousHash, e.StoredHash, e.IsRepaired, e.RepairedAt, e.GapReason,
                    !string.IsNullOrWhiteSpace(e.UserName) ? e.UserName
                        : e.UserId is not null && names.TryGetValue(e.UserId, out var n) ? n : null);
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

        /// <summary>登入帳號 → 人員姓名（"System" 等非使用者值查無對應，維持顯示帳號）</summary>
        private async Task<IReadOnlyDictionary<string, string>> ResolveNamesAsync(IEnumerable<string?> userIds, CancellationToken cancellationToken)
        {
            var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (_identityResolver is null || ids.Count == 0) return new Dictionary<string, string>();
            return await _identityResolver.ResolveDisplayNamesAsync(ids, cancellationToken);
        }

        private IQueryable<AuditLogEntity> GetQueryableOrThrow(string? source) =>
            _sources.GetQueryable(source)
            ?? throw new ArgumentException($"找不到資料日誌來源：{source}", nameof(source));
    }
}
