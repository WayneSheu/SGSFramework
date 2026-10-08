using System;
using System.Collections.Generic;
using System.Text;

namespace LogMcpServer.Domain.Entities
{
    public record LogEntry(
        DateTime Timestamp,
        string Level,
        string Message,
        string? Exception,
        string? SourceContext
    );
}
