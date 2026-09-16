// ==========================================
// 檔案路徑: src/Infrastructure/SGSFramework.Identity/Strategies/EventDrivenUserProvisioningStrategy.cs
// 架構層級: Infrastructure / Application Layer (方案二：事件驅動實作)
// ==========================================

namespace SGSFramework.Identity.Strategies;

using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Events.Users;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using SGSFramework.Identity.Abstractions.Strategies;
using SGSFramework.Identity.DTOs.Users;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 事件驅動用戶創建策略
/// </summary>
public sealed class EventDrivenUserProvisioningStrategy : IUserProvisioningStrategy
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IPublisher _publisher;
    private readonly ILogger<EventDrivenUserProvisioningStrategy> _logger;

    public string StrategyName => "EventDriven";

    public EventDrivenUserProvisioningStrategy(
        UserManager<IdentityUser> userManager,
        IPublisher publisher,
        ILogger<EventDrivenUserProvisioningStrategy> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<Guid>> ProvisionUserAsync(UserProvisioningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var user = new IdentityUser
            {
                UserName = context.Username,
                Email = context.Email,
                EmailConfirmed = true
            };

            var createResult = string.IsNullOrEmpty(context.Password)
                ? await _userManager.CreateAsync(user)
                : await _userManager.CreateAsync(user, context.Password);

            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                return Result.Failure<Guid>(Error.Failure("Provision.EventDriven.CreateFailed", $"事件驅動建立帳號失敗: {errors}"));
            }

            var userId = Guid.Parse(user.Id);

            var domainEvent = new UserCreatedEvent(userId, context.Username, context.DefaultLabId,context.TenantLabId, context.RoleName);
            await _publisher.Publish(domainEvent, cancellationToken);

            _logger.LogInformation("事件驅動策略成功建立基礎使用者 {Username} 並發布 UserCreatedEvent", context.Username);

            return Result.Success(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "事件驅動策略(方案二)執行時發生未預期例外: {Username}", context.Username);
            return Result.Failure<Guid>(Error.Failure("Provision.EventDriven.Exception", "執行事件驅動帳號建立時發生內部系統錯誤"));
        }
    }
}