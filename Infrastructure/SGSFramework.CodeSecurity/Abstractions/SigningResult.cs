using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Abstractions
{
    /// <summary>
    /// 簽署作業結果 (Immutable Record)
    /// </summary>
    public record SigningResult
    {
        // 強制定義屬性
        public SigningStatus Status { get; init; }
        public string ArtifactPath { get; init; }
        public string? ErrorMessage { get; init; }
        public string? SignatureThumbprint { get; init; }
        public DateTime Timestamp { get; init; }

        // 單一主建構子 (Primary Constructor)
        public SigningResult(
            SigningStatus status,
            string artifactPath,
            string? errorMessage = null,
            string? thumbprint = null,
            DateTime? timestamp = null)
        {
            Status = status;
            ArtifactPath = artifactPath;
            ErrorMessage = errorMessage;
            SignatureThumbprint = thumbprint;
            Timestamp = timestamp ?? DateTime.UtcNow;
        }

        // 靜態工廠方法 (不再與建構子衝突)
        public static SigningResult CreateSuccess(string path, string thumbprint)
            => new(SigningStatus.Success, path, thumbprint: thumbprint);

        public static SigningResult CreateFailure(string path, string error)
            => new(SigningStatus.Failed, path, errorMessage: error);
    }
}
