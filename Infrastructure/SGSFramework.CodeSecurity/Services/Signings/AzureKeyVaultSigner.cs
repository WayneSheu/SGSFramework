using SGSFramework.CodeSecurity.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SGSFramework.CodeSecurity.Services.Signings
{
    public class AzureKeyVaultSigner : ISigningService
    {
        private readonly string _signtoolPath = @"C:\Program Files (x86)\Windows Kits\10\bin\x64\signtool.exe";

        public async Task<SigningResult> SignArtifactAsync(string artifactPath, string? timestampUrl = "http://timestamp.digicert.com", CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(artifactPath) || !File.Exists(artifactPath))
                {
                    return SigningResult.CreateFailure(artifactPath, "Artifact file not found.");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = _signtoolPath,
                    // 加入 /v (Verbose) 參數，方便日後若需解析簽署詳細資訊
                    Arguments = $"sign /tr {timestampUrl} /td sha256 /fd sha256 /a \"{artifactPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true, // 捕捉錯誤輸出
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                await process!.WaitForExitAsync(ct);

                if (process.ExitCode == 0)
                {
                    // 簽署成功，回傳包含狀態與預留的指紋欄位
                    return SigningResult.CreateSuccess(artifactPath, "SIGNED_BY_AKV");
                }
                else
                {
                    // 捕捉 SignTool 的標準錯誤輸出
                    string error = await process.StandardError.ReadToEndAsync(ct);
                    return SigningResult.CreateFailure(artifactPath, $"SignTool exit code {process.ExitCode}: {error}");
                }
            }
            catch (Exception ex)
            {
                // 將系統級異常轉譯為安全且結構化的結果
                return SigningResult.CreateFailure(artifactPath, $"Execution error: {ex.Message}");
            }
        }
    }
}
