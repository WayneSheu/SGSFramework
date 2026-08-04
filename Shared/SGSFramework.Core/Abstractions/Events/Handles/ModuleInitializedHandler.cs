using MediatR;
using Microsoft.Extensions.Logging;

namespace SGSFramework.Core.Abstractions.Events.Handles
{

    /// <summary>
    /// 模組初始化事件處理器，負責處理模組初始化完成後的相關邏輯，例如記錄日誌、觸發後續流程等。
    /// </summary>
    /// <param name="logService"></param>
    public class ModuleInitializedHandler : INotificationHandler<ModuleInitializedEvent>
    {
        private readonly ILogger<ModuleInitializedHandler> _logger;

        // 透過建構子注入 Logger 和您的日誌服務
        public ModuleInitializedHandler(ILogger<ModuleInitializedHandler> logger)
        {
            _logger = logger;
        }

        public async Task Handle(ModuleInitializedEvent notification, CancellationToken ct)
        {
            if (notification == null) return;

            if (notification.IsSuccess)
            {
                // 1. 本地日誌 (Console/Debug)
                _logger.LogInformation("模組 {ModuleName} 初始化成功。", notification.ModuleName);
            }
            else
            {
                _logger.LogError("模組 {ModuleName} 初始化失敗: {Message}",notification.ModuleName, notification.Message);

                // 觸發告警邏輯...
                // 例如：發送電子郵件通知、觸發監控系統告警等

            }
        }
    }


}
