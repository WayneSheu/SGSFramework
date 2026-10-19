// Path: Infrastructure/SGSFramework.SystemLog/Services/SystemLogQueryService.cs
#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Paginations;
using SGSFramework.Core.Persistence;
using SGSFramework.SystemLog.DTOs;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>
    /// 數據庫日誌查詢服務企業級實作（優化策略模式、強型別防禦與高效能映射）
    /// </summary>
    public class SystemLogQueryService : ISystemLogQueryService
    {
        private readonly IQueryRepository<Core.Abstractions.Logings.SystemLog> _systemLogRepo;
        private readonly IQueryRepository<Core.Abstractions.Logings.SecurityLog> _securityLogRepo;
        private readonly ILogger<SystemLogQueryService> _logger;

        public SystemLogQueryService(
            IQueryRepository<Core.Abstractions.Logings.SystemLog> systemLogRepo,
            IQueryRepository<Core.Abstractions.Logings.SecurityLog> securityLogRepo,
            ILogger<SystemLogQueryService> logger)
        {
            _systemLogRepo = systemLogRepo ?? throw new ArgumentNullException(nameof(systemLogRepo));
            _securityLogRepo = securityLogRepo ?? throw new ArgumentNullException(nameof(securityLogRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PagedResult<SystemLogDto>> GetSystemLogsAsync(SystemLogQueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var query = _systemLogRepo.GetQueryable();

                // 1. 篩選條件處理 (Filter)
                if (request.Filter != null)
                {
                    if (!string.IsNullOrWhiteSpace(request.Filter.Level))
                        query = query.Where(x => x.Level == request.Filter.Level);

                    if (!string.IsNullOrWhiteSpace(request.Filter.TenantId))
                        query = query.Where(x => x.TenantId == request.Filter.TenantId);

                    if (!string.IsNullOrWhiteSpace(request.Filter.UserId))
                        query = query.Where(x => x.UserId == request.Filter.UserId);

                    if (!string.IsNullOrWhiteSpace(request.Filter.ModuleName))
                        query = query.Where(x => x.ModuleName == request.Filter.ModuleName);

                    if (!string.IsNullOrWhiteSpace(request.Filter.AlertId))
                    {
                        // 前綴比對：可貼上完整或部分 AlertId，且仍可利用索引
                        var alertId = request.Filter.AlertId.Trim();
                        query = query.Where(x => x.AlertId != null && x.AlertId.StartsWith(alertId));
                    }

                    if (request.Filter.StartTimestamp.HasValue)
                        query = query.Where(x => x.TimeStamp >= request.Filter.StartTimestamp.Value);

                    if (request.Filter.EndTimestamp.HasValue)
                        query = query.Where(x => x.TimeStamp <= request.Filter.EndTimestamp.Value);
                }

                // 2. 全文檢索 (SearchTerm)
                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    var term = request.SearchTerm.Trim();
                    query = query.Where(x => (x.Message != null && x.Message.Contains(term)) ||
                                             (x.Exception != null && x.Exception.Contains(term)));
                }

                // 3. 註冊排序策略上下文
                var sortContext = new SortStrategyContext<Core.Abstractions.Logings.SystemLog>()
                    .Register("Timestamp", new ExpressionSortStrategy<Core.Abstractions.Logings.SystemLog>(x => x.TimeStamp))
                    .Register("Level", new ExpressionSortStrategy<Core.Abstractions.Logings.SystemLog>(x => x.Level))
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

                // 4. 執行高效能分頁（確保 ToPagedResultAsync 內部包含 Count 查詢以避免回傳 totalCount 為 -1）
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
                    UserId: e.UserId,
                    ModuleName: e.ModuleName,
                    Operation: e.Operation,
                    CorrelationId: e.CorrelationId,
                    IP: e.IP,
                    Url: e.Url,
                    Payload: e.Payload,
                    AlertId: e.AlertId,
                    CreatedAt: e.CreatedAt
                )).ToList();

                return new PagedResult<SystemLogDto>(dtos, pagedEntities.TotalCount, pagedEntities.PageIndex, pagedEntities.PageSize);
            }
            catch (Exception ex)
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
                var query = _securityLogRepo.GetQueryable();

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

                // 2. 全文檢索 (SearchTerm)
                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    var term = request.SearchTerm.Trim();
                    query = query.Where(x => (x.Message != null && x.Message.Contains(term)) ||
                                             (x.UserId != null && x.UserId.Contains(term)) ||
                                             (x.ClientIp != null && x.ClientIp.Contains(term)));
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢資安日誌清單時發生未預期的內核錯誤。");
                throw new InvalidOperationException("查詢資安日誌清單失敗。", ex);
            }
        }

        public async Task<SystemLogDto?> GetSystemLogByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _systemLogRepo.GetByIdAsync(id, cancellationToken);
                if (entity == null) return null;

                return new SystemLogDto(
                    Id: entity.Id,
                    TimeStamp: entity.TimeStamp,
                    Level: entity.Level,
                    Message: entity.Message,
                    Exception: entity.Exception,
                    TenantId: entity.TenantId,
                    UserId: entity.UserId,
                    ModuleName: entity.ModuleName,
                    Operation: entity.Operation,
                    CorrelationId: entity.CorrelationId,
                    IP: entity.IP,
                    Url: entity.Url,
                    Payload: entity.Payload,
                    AlertId: entity.AlertId,
                    CreatedAt: entity.CreatedAt
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "依主鍵識別碼 ({Id}) 檢索系統日誌時發生異常。", id);
                throw new InvalidOperationException("檢索系統日誌失敗。", ex);
            }
        }

        public async Task<SecurityLogDto?> GetSecurityLogByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _securityLogRepo.GetByIdAsync(id, cancellationToken);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "依主鍵識別碼 ({Id}) 檢索資安日誌時發生異常。", id);
                throw new InvalidOperationException("檢索資安日誌失敗。", ex);
            }
        }
    }
}