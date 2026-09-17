// ==========================================
// 檔案路徑: src/Infrastructure/SGSFramework.Identity/Handlers/UserCreatedEventHandler.cs
// 架構層級: Infrastructure / Application Event Handler Layer
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Handlers;

using MediatR;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Events.Users;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 處理使用者建立事件，自動指派預設實驗室與權限維度 (強化結構化情境記錄)
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

        // 利用 Logger BeginScope 注入關聯情境屬性，利於 ELK / Seq 進行集中式檢索與追蹤
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["TargetUserId"] = notification.UserId,
            ["Username"] = notification.Username,
            ["InitialLabId"] = notification.InitialLabId ?? 0,
            ["TenantLabId"] = notification.TenantLabId ?? Guid.Empty
        });

        _logger.LogInformation(
            "開始執行 [UserCreatedEvent] 事件處理：準備為使用者 {Username} (ID: {TargetUserId}) 建立預設實驗室關聯 (LabId: {InitialLabId}, TenantId: {TenantLabId})。",
            notification.Username,
            notification.UserId,
            notification.InitialLabId,
            notification.TenantLabId);

        try
        {
            var labMapping = UserLabMapping.CreatePrimary(
                userId: notification.UserId,
                labId: notification.InitialLabId ?? 0,
                tenantLabId: notification.TenantLabId ?? Guid.Empty,
                jobTitle: "預設職位",
                operatorId: notification.Username
            );

            await _userLabRepository.AddOrUpdateSecondaryLabAsync(labMapping, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "成功完成 [UserCreatedEvent] 事件處理：已為使用者 {Username} (ID: {TargetUserId}) 寫入預設實驗室對應。",
                notification.Username,
                notification.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "處理 [UserCreatedEvent] 事件時發生例外：寫入使用者 {Username} (ID: {TargetUserId}) 的預設實驗室關聯失敗。",
                notification.Username,
                notification.UserId);
            throw;
        }
    }
}