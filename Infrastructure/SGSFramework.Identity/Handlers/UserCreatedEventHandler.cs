
namespace SGSFramework.Identity.Handlers;

using MediatR;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Events.Users;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 處理使用者建立事件，自動指派預設實驗室與權限維度
/// </summary>
public sealed class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
{
    private readonly IUserLabRepository _userLabRepository;
    private readonly ILogger<UserCreatedEventHandler> _logger;

    public UserCreatedEventHandler(
        IUserLabRepository userLabRepository,
        ILogger<UserCreatedEventHandler> logger)
    {
        _userLabRepository = userLabRepository ?? throw new ArgumentNullException(nameof(userLabRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            // 透過領域模型提供的靜態原廠方法 (Factory Method) 建立實體，徹底解決 CS0272 (private set) 編譯錯誤
            var labMapping = UserLabMapping.CreatePrimary(
                userId: notification.UserId,
                labId: notification.InitialLabId,
                tenantLabId: notification.TenantLabId,
                jobTitle: "預設職位",
                operatorId: notification.Username
            );

            await _userLabRepository.AddOrUpdateSecondaryLabAsync(labMapping, cancellationToken);

            _logger.LogInformation("成功為新使用者 {UserId} 建立預設實驗室關聯 (LabId: {LabId}, TenantLabId: {TenantLabId})",
                notification.UserId, notification.InitialLabId, notification.TenantLabId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理使用者建立事件時寫入預設實驗室關聯失敗。UserId: {UserId}", notification.UserId);
            throw;
        }
    }
}