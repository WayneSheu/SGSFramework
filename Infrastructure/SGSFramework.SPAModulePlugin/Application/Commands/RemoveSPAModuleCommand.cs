// Path: src/SGSFramework/Infrastructure/SGSFramework.SPAModulePlugin/Application/Commands/RemoveSPAModuleCommand.cs
namespace SGSFramework.SPAModulePlugin.Application.Commands;

using MediatR;
using Microsoft.Extensions.Logging;
using SGSFramework.SPAModulePlugin.Application.Abstractions;
using SGSFramework.SPAModulePlugin.Domain.Enums;

/// <summary>
/// 卸載並刪除指定 SPA 外掛模組命令
/// </summary>
public sealed record RemoveSPAModuleCommand(
    string ModuleName,
    SPAFrameworkType FrameworkType) : IRequest<bool>;

public sealed class RemoveSPAModuleCommandHandler : IRequestHandler<RemoveSPAModuleCommand, bool>
{
    private readonly ISPAModuleStorageService _storageService;
    private readonly ILogger<RemoveSPAModuleCommandHandler> _logger;

    public RemoveSPAModuleCommandHandler(ISPAModuleStorageService storageService, ILogger<RemoveSPAModuleCommandHandler> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(RemoveSPAModuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. 刪除實體檔案目錄
        var success = await _storageService.DeleteModuleDirectoryAsync(
            request.ModuleName,
            request.FrameworkType,
            cancellationToken).ConfigureAwait(false);

        // 2. TODO: 同步從資料庫中刪除模組註冊元資料
        _logger.LogInformation("SPA 模組卸載 CQRS Command 處理完成：{ModuleName}, Result: {Success}", request.ModuleName, success);

        return success;
    }
}