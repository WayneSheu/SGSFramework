using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Events.Users
{
    /// <summary>
    /// 使用者建立完成領域事件
    /// </summary>
    public sealed record UserCreatedEvent(
        Guid UserId,
        string Username,
        int? InitialLabId,
        Guid? TenantLabId,
        string? RoleName
    ) : INotification;
}
