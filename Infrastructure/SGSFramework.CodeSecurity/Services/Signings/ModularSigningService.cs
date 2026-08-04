using Microsoft.Extensions.Logging;
using SGSFramework.CodeSecurity.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Services.Signings
{
    public class ModularSigningService : ISigningService
    {
        private readonly ISigningStrategy _strategy;
        private readonly ILogger<ModularSigningService> _logger;

        public ModularSigningService(ISigningStrategy strategy, ILogger<ModularSigningService> logger)
        {
            _strategy = strategy;
            _logger = logger;
        }

        /// <summary>
        /// 針對單一模組檔案進行簽署
        /// </summary>
        public async Task<SigningResult> SignArtifactAsync(string artifactPath, string? timestampUrl = null, CancellationToken ct = default)
        {
            if (!File.Exists(artifactPath))
            {
                return SigningResult.CreateFailure(artifactPath, "Artifact file does not exist.");
            }

            try
            {
                _logger.LogInformation("Starting signature process for: {Path}", artifactPath);

                // 委派給策略模式實作 (TpmSigningStrategy 或 OfflineFileSigningStrategy)
                var result = await _strategy.SignAsync(artifactPath, timestampUrl);

                if (result.Status==SigningStatus.Success)
                    _logger.LogInformation("Successfully signed: {Path}", artifactPath);
                else
                    _logger.LogError("Failed to sign {Path}: {Error}", artifactPath, result.ErrorMessage);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Exception occurred during signing process for {Path}", artifactPath);
                return SigningResult.CreateFailure(artifactPath, "Internal signing system error.");
            }
        }

        /// <summary>
        /// 模組單體架構專用：批次簽署目錄下的所有模組組件
        /// </summary>
        public async Task<IEnumerable<SigningResult>> SignDirectoryAsync(string directoryPath, CancellationToken ct = default)
        {
            var results = new List<SigningResult>();
            var files = Directory.GetFiles(directoryPath, "*.dll"); // 僅簽署 DLL 模組

            foreach (var file in files)
            {
                var result = await SignArtifactAsync(file, ct: ct);
                results.Add(result);
            }

            return results;
        }
    }
}
