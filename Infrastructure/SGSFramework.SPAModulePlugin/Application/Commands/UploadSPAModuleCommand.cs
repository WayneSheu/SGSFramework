// Path: src/SGSFramework/Infrastructure/SGSFramework.SPAModulePlugin/Application/Commands/UploadSPAModuleCommand.cs
namespace SGSFramework.SPAModulePlugin.Application.Commands;

using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SGSFramework.SPAModulePlugin.Application.DTOs;

/// <summary>
/// 上傳並部署 SPA 外掛模組命令
/// </summary>
public sealed record UploadSPAModuleCommand(IReadOnlyList<IFormFile> Files) : IRequest<SPAModuleUploadResponseDto>;

public sealed class UploadSPAModuleCommandHandler : IRequestHandler<UploadSPAModuleCommand, SPAModuleUploadResponseDto>
{
    private readonly ILogger<UploadSPAModuleCommandHandler> _logger;

    public UploadSPAModuleCommandHandler(ILogger<UploadSPAModuleCommandHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SPAModuleUploadResponseDto> Handle(UploadSPAModuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Files == null || request.Files.Count == 0)
        {
            throw new ArgumentException("上傳的 SPA 模組檔案不可為空。", nameof(request));
        }

        try
        {
            // TODO: 實作 SPA 靜態資源解壓縮、 Manifest 校驗與 DB 紀錄寫入邏輯
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

            var firstFile = request.Files[0];
            var moduleName = Path.GetFileNameWithoutExtension(firstFile.FileName);

            _logger.LogInformation("SPA 模組 [{ModuleName}] 上傳解壓縮完成，共處理 {Count} 個檔案。", moduleName, request.Files.Count);

            return new SPAModuleUploadResponseDto
            {
                ModuleName = moduleName,
                DisplayName = moduleName,
                Version = "1.0.0",
                TargetPath = $"/wwwroot/spa-modules/{moduleName}",
                ProcessedFilesCount = request.Files.Count,
                UploadedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理 SPA 模組上傳時發生例外。");
            throw;
        }
    }
}
