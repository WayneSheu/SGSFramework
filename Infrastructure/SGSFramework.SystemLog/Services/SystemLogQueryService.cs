// Path: Infrastructure/SGSFramework.SystemLog/Services/SystemLogQueryService.cs
#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Logging;
using SGSFramework.Core.Paginations;
using SGSFramework.SystemLog.DTOs;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>
    /// 數據庫日誌查詢服務企業級實作（支援多關鍵字空白分隔 OR 檢索、強型別防禦與高效能映射）
    /// </summary>
    public class SystemLogQueryService : ISystemLogQueryService
    {
        private readonly ILogger<SystemLogQueryService> _logger;
        private readonly ILogReadSource? _readSource;

        public SystemLogQueryService(
            ILogger<SystemLogQueryService> logger,
            ILogReadSource? readSource = null)
        {
            _readSource = readSource;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyList<LogSourceDto> GetSources() =>
            _readSource?.Sources.Select(s => new LogSourceDto(s.Key, s.DisplayName, s.IsHot)).ToList() ?? [];

        /// <summary>系統 / 資安日誌一律讀取獨立日誌庫（SGS.Modules.LOG）；模組未載入或未設定 LogConnection 時無資料來源</summary>
        private ILogReadSource RequireReadSource() =>
            _readSource ?? throw new InvalidOperationException("日誌模組未啟用：請設定 PersistentSettings:ConnectionStrings:LogConnection 並部署 SGS.Modules.LOG。");

        public async Task<PagedResult<SystemLogDto>> GetSystemLogsAsync(SystemLogQueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var source = RequireReadSource();
                var keys = ResolveKeys(request.Sources);
                if (keys.Count > 1) return await GetMergedAsync(request, source, keys, cancellationToken);

                var query = source.SystemLogsOf(keys[0]);

                query = ApplySystemLogFilter(query, request);

                // 3. 註冊排序策略上下文
                var sortContext = new SortStrategyContext<Core.Abstractions.Logings.SystemLog>()
                    .Register("Timestamp", new ExpressionSortStrategy<Core.Abstractions.Logings.SystemLog>(x => x.TimeStamp))
                    .Register("Level", new ExpressionSortStrategy<Core.Abstractions.Logings.SystemLog>(x => x.Level))
                    .Register("UserName", new ExpressionSortStrategy<Core.Abstractions.Logings.SystemLog>(x => x.UserName!))
                    .Register("TenantName", new ExpressionSortStrategy<Core.Abstractions.Logings.SystemLog>(x => x.TenantName!))
                    .Register("ControllerName", new ExpressionSortStrategy<Core.Abstractions.Logings.SystemLog>(x => x.ControllerName!))
                    .Register("Id", new ExpressionSortStrategy<Core.Abstractions.Logings.SystemLog>(x => x.Id));

                var sorts = request.Sorts?.Count > 0
                    ? request.Sorts
                    : new[] { new SortCriteria { SortBy = "Timestamp", Direction = SortDirection.Descending } };

                var queryParameters = new PagedQueryParameters<object>
                {
                    Type = request.Type,
                    PageIndex = request.PageIndex,
                    PageSize = request.PageSize,
                    SearchTerm = request.SearchTerm,
                    Sorts = sorts,
                    LastSortValue = request.LastSortValue,
                    LastIdValue = request.LastIdValue
                };

                // 4. 執行高效能分頁
                var pagedEntities = await query.ToPagedResultAsync(
                    parameters: queryParameters,
                    sortContext: sortContext,
                    idSelector: x => x.Id,
                    cancellationToken: cancellationToken);

                // 5. 轉換為對應的 SystemLogDto 結構
                var dtos = pagedEntities.Items.Select(e => new SystemLogDto(
                    Id: e.Id,
                    TimeStamp: e.TimeStamp,
                    Level: e.Level,
                    Message: e.Message,
                    Exception: e.Exception,
                    TenantId: e.TenantId,
                    TenantName: e.TenantName,
                    UserId: e.UserId,
                    UserName: e.UserName,
                    ModuleName: e.ModuleName,
                    Operation: e.Operation,
                    OperationDesc: e.OperationDesc,
                    ControllerName: e.ControllerName,
                    CorrelationId: e.CorrelationId,
                    IP: e.IP,
                    Url: e.Url,
                    Payload: e.Payload,
                    AlertId: e.AlertId,
                    CreatedAt: e.CreatedAt
                )).ToList();

                return new PagedResult<SystemLogDto>(dtos, pagedEntities.TotalCount, pagedEntities.PageIndex, pagedEntities.PageSize);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "查詢系統日誌清單時發生未預期的內核錯誤。");
                throw new InvalidOperationException("查詢系統日誌清單失敗。", ex);
            }
        }

        public async Task<PagedResult<SecurityLogDto>> GetSecurityLogsAsync(SecurityLogQueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var source = RequireReadSource();
                var keys = ResolveKeys(request.Sources);
                if (keys.Count > 1) return await GetMergedSecurityAsync(request, source, keys, cancellationToken);

                var query = source.SecurityLogsOf(keys[0]);

                query = ApplySecurityLogFilter(query, request);

                // 3. 註冊排序策略上下文
                var sortContext = new SortStrategyContext<Core.Abstractions.Logings.SecurityLog>()
                    .Register("Timestamp", new ExpressionSortStrategy<Core.Abstractions.Logings.SecurityLog>(x => x.Timestamp))
                    .Register("EventCategory", new ExpressionSortStrategy<Core.Abstractions.Logings.SecurityLog>(x => x.EventCategory))
                    .Register("Id", new ExpressionSortStrategy<Core.Abstractions.Logings.SecurityLog>(x => x.Id));

                var sorts = request.Sorts?.Count > 0
                    ? request.Sorts
                    : new[] { new SortCriteria { SortBy = "Timestamp", Direction = SortDirection.Descending } };

                var queryParameters = new PagedQueryParameters<object>
                {
                    Type = request.Type,
                    PageIndex = request.PageIndex,
                    PageSize = request.PageSize,
                    SearchTerm = request.SearchTerm,
                    Sorts = sorts,
                    LastSortValue = request.LastSortValue,
                    LastIdValue = request.LastIdValue
                };

                // 4. 執行高效能分頁
                var pagedEntities = await query.ToPagedResultAsync(
                    parameters: queryParameters,
                    sortContext: sortContext,
                    idSelector: x => x.Id,
                    cancellationToken: cancellationToken);

                // 5. 轉換為對應的 SecurityLogDto 結構
                var dtos = pagedEntities.Items.Select(e => new SecurityLogDto(
                    Id: e.Id,
                    Timestamp: e.Timestamp,
                    Level: e.Level,
                    Message: e.Message,
                    Exception: e.Exception,
                    LogType: e.LogType,
                    EventCategory: e.EventCategory,
                    UserId: e.UserId,
                    ClientIp: e.ClientIp,
                    CorrelationId: e.CorrelationId,
                    Properties: e.Properties,
                    AlertId: e.AlertId
                )).ToList();

                return new PagedResult<SecurityLogDto>(dtos, pagedEntities.TotalCount, pagedEntities.PageIndex, pagedEntities.PageSize);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "查詢資安日誌清單時發生未預期的內核錯誤。");
                throw new InvalidOperationException("查詢資安日誌清單失敗。", ex);
            }
        }

        /// <summary>來源清單：多選的 Sources 優先；空值 = 熱庫。去除空白與重複。</summary>
        private static IReadOnlyList<string?> ResolveKeys(IReadOnlyList<string>? sources)
        {
            var keys = (sources ?? [])
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string?>()
                .ToList();

            return keys.Count > 0 ? keys : [null];
        }

        private static IQueryable<Core.Abstractions.Logings.SystemLog> ApplySystemLogFilter(
            IQueryable<Core.Abstractions.Logings.SystemLog> query, SystemLogQueryRequest request)
        {
                // 1. 篩選條件處理 (Filter)
                if (request.Filter != null)
                {
                    if (!string.IsNullOrWhiteSpace(request.Filter.Level))
                        query = query.Where(x => x.Level == request.Filter.Level);

                    if (!string.IsNullOrWhiteSpace(request.Filter.TenantId))
                    {
                        var tenantId = request.Filter.TenantId.Trim();
                        query = query.Where(x => x.TenantId == tenantId);
                    }

                    if (!string.IsNullOrWhiteSpace(request.Filter.TenantName))
                    {
                        var tenantName = request.Filter.TenantName.Trim();
                        query = query.Where(x => x.TenantName != null && x.TenantName.Contains(tenantName));
                    }

                    if (!string.IsNullOrWhiteSpace(request.Filter.UserId))
                    {
                        var userId = request.Filter.UserId.Trim();
                        query = query.Where(x => x.UserId == userId);
                    }

                    if (!string.IsNullOrWhiteSpace(request.Filter.IP))
                    {
                        var ip = request.Filter.IP.Trim();
                        query = query.Where(x => x.IP == ip);
                    }

                    if (!string.IsNullOrWhiteSpace(request.Filter.UserName))
                    {
                        var userName = request.Filter.UserName.Trim();
                        query = query.Where(x => x.UserName != null && x.UserName.Contains(userName));
                    }

                    if (!string.IsNullOrWhiteSpace(request.Filter.ModuleName))
                    {
                        var moduleName = request.Filter.ModuleName.Trim();
                        query = query.Where(x => x.ModuleName != null && x.ModuleName.Contains(moduleName));
                    }

                    if (!string.IsNullOrWhiteSpace(request.Filter.Operation))
                    {
                        var operation = request.Filter.Operation.Trim();
                        query = query.Where(x => x.Operation != null && x.Operation.Contains(operation));
                    }

                    if (!string.IsNullOrWhiteSpace(request.Filter.OperationDesc))
                    {
                        var operationDesc = request.Filter.OperationDesc.Trim();
                        query = query.Where(x => x.OperationDesc != null && x.OperationDesc.Contains(operationDesc));
                    }

                    if (!string.IsNullOrWhiteSpace(request.Filter.ControllerName))
                    {
                        var controllerName = request.Filter.ControllerName.Trim();
                        query = query.Where(x => x.ControllerName != null && x.ControllerName.Contains(controllerName));
                    }

                    if (!string.IsNullOrWhiteSpace(request.Filter.CorrelationId))
                    {
                        var correlationId = request.Filter.CorrelationId.Trim();
                        query = query.Where(x => x.CorrelationId != null && x.CorrelationId.StartsWith(correlationId));
                    }

                    if (!string.IsNullOrWhiteSpace(request.Filter.AlertId))
                    {
                        var alertId = request.Filter.AlertId.Trim();
                        query = query.Where(x => x.AlertId != null && x.AlertId.StartsWith(alertId));
                    }

                    if (request.Filter.StartTimestamp.HasValue)
                        query = query.Where(x => x.TimeStamp >= request.Filter.StartTimestamp.Value);

                    if (request.Filter.EndTimestamp.HasValue)
                        query = query.Where(x => x.TimeStamp <= request.Filter.EndTimestamp.Value);
                }

                // 2. 多關鍵字檢索 (支援半形/全形空白分隔，改為 OR 邏輯：只要符合其中一個關鍵字即可)
                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    var keywords = request.SearchTerm
                        .Split(new[] { ' ', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Trim())
                        .Where(k => !string.IsNullOrEmpty(k))
                        .ToList();

                    if (keywords.Count > 0)
                    {
                        query = query.Where(x => keywords.Any(keyword =>
                            (x.Message != null && x.Message.Contains(keyword)) ||
                            (x.Exception != null && x.Exception.Contains(keyword)) ||
                            (x.UserName != null && x.UserName.Contains(keyword)) ||
                            (x.TenantName != null && x.TenantName.Contains(keyword)) ||
                            (x.ModuleName != null && x.ModuleName.Contains(keyword)) ||
                            (x.Operation != null && x.Operation.Contains(keyword)) ||
                            (x.ControllerName != null && x.ControllerName.Contains(keyword))
                        ));
                    }
                }

            return query;
        }

        private static IQueryable<Core.Abstractions.Logings.SecurityLog> ApplySecurityLogFilter(
            IQueryable<Core.Abstractions.Logings.SecurityLog> query, SecurityLogQueryRequest request)
        {
                // 1. 篩選條件處理 (Filter)
                if (request.Filter != null)
                {
                    if (!string.IsNullOrWhiteSpace(request.Filter.Level))
                    {
                        query = query.Where(x => x.Level == request.Filter.Level);
                    }

                    if (!string.IsNullOrWhiteSpace(request.Filter.AlertId))
                    {
                        var alertId = request.Filter.AlertId.Trim();
                        query = query.Where(x => x.AlertId != null && x.AlertId.StartsWith(alertId));
                    }

                    if (!string.IsNullOrWhiteSpace(request.Filter.EventCategory))
                    {
                        query = query.Where(x => x.EventCategory == request.Filter.EventCategory);
                    }

                    if (!string.IsNullOrWhiteSpace(request.Filter.UserId))
                    {
                        query = query.Where(x => x.UserId == request.Filter.UserId);
                    }

                    if (request.Filter.StartTimestamp.HasValue)
                    {
                        query = query.Where(x => x.Timestamp >= request.Filter.StartTimestamp.Value);
                    }

                    if (request.Filter.EndTimestamp.HasValue)
                    {
                        query = query.Where(x => x.Timestamp <= request.Filter.EndTimestamp.Value);
                    }
                }

                // 2. 多關鍵字檢索 (支援半形/全形空白分隔，改為 OR 邏輯：只要符合其中一個關鍵字即可)
                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    var keywords = request.SearchTerm
                        .Split(new[] { ' ', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Trim())
                        .Where(k => !string.IsNullOrEmpty(k))
                        .ToList();

                    if (keywords.Count > 0)
                    {
                        query = query.Where(x => keywords.Any(keyword =>
                            (x.Message != null && x.Message.Contains(keyword)) ||
                            (x.UserId != null && x.UserId.Contains(keyword)) ||
                            (x.ClientIp != null && x.ClientIp.Contains(keyword)) ||
                            (x.EventCategory != null && x.EventCategory.Contains(keyword)) ||
                            (x.LogType != null && x.LogType.Contains(keyword))
                        ));
                    }
                }

            return query;
        }

        /// <summary>多來源（熱庫 + 冷庫）合併：各來源取前 N 筆、加總筆數，再依時間合併切頁；僅支援依時間排序。</summary>
        private async Task<PagedResult<SystemLogDto>> GetMergedAsync(
            SystemLogQueryRequest request, ILogReadSource source, IReadOnlyList<string?> keys, CancellationToken cancellationToken)
        {
            var take = LogPageMerger.TakePerSource(request.PageIndex, request.PageSize);
            var ascending = request.Sorts?.FirstOrDefault(x => string.Equals(x.SortBy, "Timestamp", StringComparison.OrdinalIgnoreCase))?.Direction == SortDirection.Ascending;

            var total = 0;
            var rows = new List<(Core.Abstractions.Logings.SystemLog Item, string? Source)>();
            foreach (var key in keys)
            {
                var query = ApplySystemLogFilter(source.SystemLogsOf(key), request);
                total += await query.CountAsync(cancellationToken);

                var ordered = ascending
                    ? query.OrderBy(x => x.TimeStamp).ThenBy(x => x.Id)
                    : query.OrderByDescending(x => x.TimeStamp).ThenByDescending(x => x.Id);
                rows.AddRange((await ordered.Take(take).ToListAsync(cancellationToken)).Select(e => (e, key)));
            }

            var page = LogPageMerger.MergePage(rows, e => e.TimeStamp, e => e.Id, request.PageIndex, request.PageSize, ascending);
            var dtos = page.Select(r => ToDto(r.Item)).ToList();
            return new PagedResult<SystemLogDto>(dtos, total, Math.Max(request.PageIndex, 1), Math.Clamp(request.PageSize, 1, 500));
        }

        private async Task<PagedResult<SecurityLogDto>> GetMergedSecurityAsync(
            SecurityLogQueryRequest request, ILogReadSource source, IReadOnlyList<string?> keys, CancellationToken cancellationToken)
        {
            var take = LogPageMerger.TakePerSource(request.PageIndex, request.PageSize);
            var ascending = request.Sorts?.FirstOrDefault(x => string.Equals(x.SortBy, "Timestamp", StringComparison.OrdinalIgnoreCase))?.Direction == SortDirection.Ascending;

            var total = 0;
            var rows = new List<(Core.Abstractions.Logings.SecurityLog Item, string? Source)>();
            foreach (var key in keys)
            {
                var query = ApplySecurityLogFilter(source.SecurityLogsOf(key), request);
                total += await query.CountAsync(cancellationToken);

                var ordered = ascending
                    ? query.OrderBy(x => x.Timestamp).ThenBy(x => x.Id)
                    : query.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id);
                rows.AddRange((await ordered.Take(take).ToListAsync(cancellationToken)).Select(e => (e, key)));
            }

            var page = LogPageMerger.MergePage(rows, e => e.Timestamp, e => e.Id, request.PageIndex, request.PageSize, ascending);
            var dtos = page.Select(r => ToSecurityDto(r.Item)).ToList();
            return new PagedResult<SecurityLogDto>(dtos, total, Math.Max(request.PageIndex, 1), Math.Clamp(request.PageSize, 1, 500));
        }

        private static SystemLogDto ToDto(Core.Abstractions.Logings.SystemLog e) => new(
            Id: e.Id, TimeStamp: e.TimeStamp, Level: e.Level, Message: e.Message, Exception: e.Exception,
            TenantId: e.TenantId, TenantName: e.TenantName, UserId: e.UserId, UserName: e.UserName,
            ModuleName: e.ModuleName, Operation: e.Operation, OperationDesc: e.OperationDesc, ControllerName: e.ControllerName,
            CorrelationId: e.CorrelationId, IP: e.IP, Url: e.Url, Payload: e.Payload, AlertId: e.AlertId, CreatedAt: e.CreatedAt);

        private static SecurityLogDto ToSecurityDto(Core.Abstractions.Logings.SecurityLog e) => new(
            Id: e.Id, Timestamp: e.Timestamp, Level: e.Level, Message: e.Message, Exception: e.Exception,
            LogType: e.LogType, EventCategory: e.EventCategory, UserId: e.UserId, ClientIp: e.ClientIp,
            CorrelationId: e.CorrelationId, Properties: e.Properties, AlertId: e.AlertId);

        public async Task<SystemLogDto?> GetSystemLogByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await RequireReadSource().SystemLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (entity == null) return null;

                return new SystemLogDto(
                    Id: entity.Id,
                    TimeStamp: entity.TimeStamp,
                    Level: entity.Level,
                    Message: entity.Message,
                    Exception: entity.Exception,
                    TenantId: entity.TenantId,
                    TenantName: entity.TenantName,
                    UserId: entity.UserId,
                    UserName: entity.UserName,
                    ModuleName: entity.ModuleName,
                    Operation: entity.Operation,
                    OperationDesc: entity.OperationDesc,
                    ControllerName: entity.ControllerName,
                    CorrelationId: entity.CorrelationId,
                    IP: entity.IP,
                    Url: entity.Url,
                    Payload: entity.Payload,
                    AlertId: entity.AlertId,
                    CreatedAt: entity.CreatedAt
                );
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "依主鍵識別碼 ({Id}) 檢索系統日誌時發生異常。", id);
                throw new InvalidOperationException("檢索系統日誌失敗。", ex);
            }
        }

        public async Task<SecurityLogDto?> GetSecurityLogByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await RequireReadSource().SecurityLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (entity == null) return null;

                return new SecurityLogDto(
                    Id: entity.Id,
                    Timestamp: entity.Timestamp,
                    Level: entity.Level,
                    Message: entity.Message,
                    Exception: entity.Exception,
                    LogType: entity.LogType,
                    EventCategory: entity.EventCategory,
                    UserId: entity.UserId,
                    ClientIp: entity.ClientIp,
                    CorrelationId: entity.CorrelationId,
                    Properties: entity.Properties,
                    AlertId: entity.AlertId
                );
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "依主鍵識別碼 ({Id}) 檢索資安日誌時發生異常。", id);
                throw new InvalidOperationException("檢索資安日誌失敗。", ex);
            }
        }
    }
}