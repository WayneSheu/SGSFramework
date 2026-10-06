// Path: src/SGSFramework/Infrastructure/SGSFramework.SPAModulePlugin/Application/Commands/UploadSPAModuleCommand.cs
namespace SGSFramework.SPAModulePlugin.Application.Commands;

using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SGSFramework.SPAModulePlugin.Application.Abstractions;
using SGSFramework.SPAModulePlugin.Application.DTOs;
using SGSFramework.SPAModulePlugin.Domain.Enums;

/// <summary>
/// 上傳並部署 SPA 外掛模組命令
/// </summary>
public sealed record UploadSPAModuleCommand(
    SPAFrameworkType FrameworkType,
    IReadOnlyList<IFormFile> Files) : IRequest<SPAModuleUploadResponseDto>;


public sealed class UploadSPAModuleCommandHandler : IRequestHandler<UploadSPAModuleCommand, SPAModuleUploadResponseDto>
{
    private readonly ISPAModuleStorageService _storageService;
    private readonly ILogger<UploadSPAModuleCommandHandler> _logger;

    public UploadSPAModuleCommandHandler(ISPAModuleStorageService storageService, ILogger<UploadSPAModuleCommandHandler> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SPAModuleUploadResponseDto> Handle(UploadSPAModuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. 解壓縮與部署實體檔案至對應 wwwroot 目錄
        var response = await _storageService.DeployModulePackageAsync(
            request.FrameworkType,
            request.Files,
            cancellationToken).ConfigureAwait(false);

        // 2. TODO: 寫入資料庫或更新 SPAModuleManifest 狀態
        _logger.LogInformation("SPA 模組 CQRS Command 處理完成：{ModuleName}", response.ModuleName);

        return response;
    }
}