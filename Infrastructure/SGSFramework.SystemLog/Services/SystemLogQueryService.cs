// Path: Infrastructure/SGSFramework.SystemLog/Services/SystemLogQueryService.cs
#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Logging;
using SGSFramework.Core.Paginations;
using SGSFramework.Core.Persistence;
using SGSFramework.SystemLog.DTOs;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>
    /// 數據庫日誌查詢服務企業級實作（支援多關鍵字空白分隔 OR 檢索、強型別防禦與高效能映射）
    /// </summary>
    public class SystemLogQueryService : ISystemLogQueryService
    {
        private readonly IQueryRepository<Core.Abstractions.Logings.SystemLog> _systemLogRepo;
        private readonly IQueryRepository<Core.Abstractions.Logings.SecurityLog> _securityLogRepo;
        private readonly ILogger<SystemLogQueryService> _logger;
        private readonly ILogReadSource? _readSource;

        public SystemLogQueryService(
            IQueryRepository<Core.Abstractions.Logings.SystemLog> systemLogRepo,
            IQueryRepository<Core.Abstractions.Logings.SecurityLog> securityLogRepo,
            ILogger<SystemLogQueryService> logger,
            ILogReadSource? readSource = null)
        {
            _readSource = readSource;
            _systemLogRepo = systemLogRepo ?? throw new ArgumentNullException(nameof(systemLogRepo));
            _securityLogRepo = securityLogRepo ?? throw new ArgumentNullException(nameof(securityLogRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PagedResult<SystemLogDto>> GetSystemLogsAsync(SystemLogQueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var query = (!request.Archive && _readSource is not null) ? _readSource.SystemLogs : _systemLogRepo.GetQueryable();

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
                var query = (!request.Archive && _readSource is not null) ? _readSource.SecurityLogs : _securityLogRepo.GetQueryable();

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

        public async Task<SystemLogDto?> GetSystemLogByIdAsync(long id, CancellationToken cancellationToken = default, bool archive = false)
        {
            try
            {
                var entity = (!archive && _readSource is not null)
                    ? await _readSource.SystemLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    : await _systemLogRepo.GetByIdAsync(id, cancellationToken);
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

        public async Task<SecurityLogDto?> GetSecurityLogByIdAsync(int id, CancellationToken cancellationToken = default, bool archive = false)
        {
            try
            {
                var entity = (!archive && _readSource is not null)
                    ? await _readSource.SecurityLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    : await _securityLogRepo.GetByIdAsync(id, cancellationToken);
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