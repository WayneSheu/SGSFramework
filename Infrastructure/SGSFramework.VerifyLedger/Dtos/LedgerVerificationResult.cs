using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.VerifyLedger.Dtos
{
    public record LedgerVerificationResult
    {
        public bool IsSuccess { get; init; }
        public string VerificationMessage { get; init; } = string.Empty;
        public DateTime VerifiedAt { get; init; } = DateTime.UtcNow;
        public string? ExtractedDigest { get; init; }

        public static LedgerVerificationResult Success(string message, string? digest = null) => new()
        {
            IsSuccess = true,
            VerificationMessage = message,
            ExtractedDigest = digest
        };

        public static LedgerVerificationResult Failure(string message, string? digest = null) => new()
        {
            IsSuccess = false,
            VerificationMessage = message,
            ExtractedDigest = digest
        };
    }
}
