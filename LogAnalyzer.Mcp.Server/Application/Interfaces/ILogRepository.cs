using LogMcpServer.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace LogMcpServer.Application.Interfaces
{
    public interface ILogRepository
    {
        Task<IEnumerable<LogEntry>> SearchLogsAsync(string? level, string? keyword, int hours, int limit, CancellationToken cancellationToken);
        Task<string> AnalyzeExceptionAsync(string exceptionMessage, CancellationToken cancellationToken);
    }
}
