using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.SystemLog.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>
    /// 數據庫日誌查詢服務實作 (使用 EF Core AsNoTracking 高效讀取 MSSQL Ledger/Log 表)
    /// </summary>
    public class SystemLogQueryService : ISystemLogQueryService
    {
        private readonly DbContext _dbContext;
        private readonly ILogger<SystemLogQueryService> _logger;

        public SystemLogQueryService(DbContext dbContext, ILogger<SystemLogQueryService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PagedResult<SystemLogDto>> GetSystemLogsAsync(SystemLogQueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var query = _dbContext.Set<SGSFramework.Core.Abstractions.Logings.SystemLog>().AsNoTracking();

                // 過濾條件組合
                if (!string.IsNullOrWhiteSpace(request.Level))
                    query = query.Where(x => x.Level == request.Level);

                if (!string.IsNullOrWhiteSpace(request.TenantId))
                    query = query.Where(x => x.TenantId == request.TenantId);

                if (!string.IsNullOrWhiteSpace(request.UserId))
                    query = query.Where(x => x.UserId == request.UserId);

                if (!string.IsNullOrWhiteSpace(request.ModuleName))
                    query = query.Where(x => x.ModuleName == request.ModuleName);

                if (!string.IsNullOrWhiteSpace(request.CorrelationId))
                    query = query.Where(x => x.CorrelationId == request.CorrelationId);

                if (request.StartTime.HasValue)
                    query = query.Where(x => x.TimeStamp >= request.StartTime.Value);

                if (request.EndTime.HasValue)
                    query = query.Where(x => x.TimeStamp <= request.EndTime.Value);

                if (!string.IsNullOrWhiteSpace(request.Keyword))
                {
                    query = query.Where(x => (x.Message != null && x.Message.Contains(request.Keyword))
                                          || (x.Exception != null && x.Exception.Contains(request.Keyword)));
                }

                long totalCount = await query.LongCountAsync(cancellationToken);

                int pageIndex = Math.Max(1, request.PageIndex);
                int pageSize = Math.Clamp(request.PageSize, 1, 500);

                var items = await query
                    .OrderByDescending(x => x.TimeStamp)
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new SystemLogDto(
                        x.Id,
                        x.TimeStamp,
                        x.Level,
                        x.Message,
                        x.Exception,
                        x.TenantId,
                        x.UserId,
                        x.ModuleName,
                        x.Operation,
                        x.CorrelationId,
                        x.IP,
                        x.Url,
                        x.Payload,
                        x.AlertId,
                        x.CreatedAt
                    ))
                    .ToListAsync(cancellationToken);

                return new PagedResult<SystemLogDto>(items.AsReadOnly(), totalCount, pageIndex, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢 core.SystemLogs 發生非預期錯誤");
                throw;
            }
        }

        public async Task<PagedResult<SecurityLogDto>> GetSecurityLogsAsync(SecurityLogQueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var query = _dbContext.Set<SecurityLog>().AsNoTracking();

                if (!string.IsNullOrWhiteSpace(request.Level))
                    query = query.Where(x => x.Level == request.Level);

                if (!string.IsNullOrWhiteSpace(request.EventCategory))
                    query = query.Where(x => x.EventCategory == request.EventCategory);

                if (!string.IsNullOrWhiteSpace(request.UserId))
                    query = query.Where(x => x.UserId == request.UserId);

                if (!string.IsNullOrWhiteSpace(request.CorrelationId))
                    query = query.Where(x => x.CorrelationId == request.CorrelationId);

                if (request.StartTime.HasValue)
                    query = query.Where(x => x.Timestamp >= request.StartTime.Value);

                if (request.EndTime.HasValue)
                    query = query.Where(x => x.Timestamp <= request.EndTime.Value);

                if (!string.IsNullOrWhiteSpace(request.Keyword))
                {
                    query = query.Where(x => (x.Message != null && x.Message.Contains(request.Keyword))
                                          || (x.Properties != null && x.Properties.Contains(request.Keyword)));
                }

                long totalCount = await query.LongCountAsync(cancellationToken);

                int pageIndex = Math.Max(1, request.PageIndex);
                int pageSize = Math.Clamp(request.PageSize, 1, 500);

                var items = await query
                    .OrderByDescending(x => x.Timestamp)
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new SecurityLogDto(
                        x.Id,
                        x.Timestamp,
                        x.Level,
                        x.Message,
                        x.Exception,
                        x.LogType,
                        x.EventCategory,
                        x.UserId,
                        x.ClientIp,
                        x.CorrelationId,
                        x.Properties,
                        x.AlertId
                    ))
                    .ToListAsync(cancellationToken);

                return new PagedResult<SecurityLogDto>(items.AsReadOnly(), totalCount, pageIndex, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢 core.SecurityLog 發生非預期錯誤");
                throw;
            }
        }

        public async Task<SystemLogDto?> GetSystemLogByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<SGSFramework.Core.Abstractions.Logings.SystemLog>()
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new SystemLogDto(
                    x.Id,
                    x.TimeStamp,
                    x.Level,
                    x.Message,
                    x.Exception,
                    x.TenantId,
                    x.UserId,
                    x.ModuleName,
                    x.Operation,
                    x.CorrelationId,
                    x.IP,
                    x.Url,
                    x.Payload,
                    x.AlertId,
                    x.CreatedAt
                ))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<SecurityLogDto?> GetSecurityLogByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<SecurityLog>()
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new SecurityLogDto(
                    x.Id,
                    x.Timestamp,
                    x.Level,
                    x.Message,
                    x.Exception,
                    x.LogType,
                    x.EventCategory,
                    x.UserId,
                    x.ClientIp,
                    x.CorrelationId,
                    x.Properties,
                    x.AlertId
                ))
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
