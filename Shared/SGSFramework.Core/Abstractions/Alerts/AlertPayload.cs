using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Alerts
{
    public sealed record AlertPayload
    {
        public required string SourceModule { get; init; }
        public required string AlertCode { get; init; }
        public required string Message { get; init; }
        public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

        public string GetFingerprintKey()
        {
            return $"{SourceModule}:{AlertCode}:{Message.GetHashCode()}";
        }
    }
}
