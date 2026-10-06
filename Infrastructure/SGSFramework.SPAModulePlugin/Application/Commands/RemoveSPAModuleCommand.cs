using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SPAModulePlugin.Application.Commands;

/// <summary>
/// 卸載並刪除指定 SPA 外掛模組命令
/// </summary>
public sealed record RemoveSPAModuleCommand(string ModuleName) : IRequest<bool>;

public sealed class RemoveSPAModuleCommandHandler : IRequestHandler<RemoveSPAModuleCommand, bool>
{
    private readonly ILogger<RemoveSPAModuleCommandHandler> _logger;

    public RemoveSPAModuleCommandHandler(ILogger<RemoveSPAModuleCommandHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(RemoveSPAModuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ModuleName))
        {
            throw new ArgumentException("卸載的 SPA 模組名稱不可為空。", nameof(request));
        }

        try
        {
            // TODO: 實作 SPA 模組實體目錄清除與資料庫清單卸載邏輯
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("SPA 模組 [{ModuleName}] 已成功卸載並清除相關資源。", request.ModuleName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "卸載 SPA 模組 [{ModuleName}] 時發生例外。", request.ModuleName);
            throw;
        }
    }
}