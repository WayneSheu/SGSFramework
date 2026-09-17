// ==========================================
// 檔案路徑: src/Infrastructure/SGSFramework.Identity/Strategies/EventDrivenUserProvisioningStrategy.cs
// 架構層級: Infrastructure / Strategy Implementation Layer
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Strategies;

using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Abstractions.Entities.Base;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Events.Users;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using SGSFramework.Identity.Abstractions.Strategies;
using SGSFramework.Identity.DTOs.Strategies;
using SGSFramework.Identity.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 事件驅動用戶創建策略 (透過 IOptions 動態識別策略，參數依策略業務規範驗證)
/// </summary>
public sealed class EventDrivenUserProvisioningStrategy<TUser, TRole, TKey> : IUserProvisioningStrategy
    where TUser : IdentityUser<TKey>, IBaseUser, new()
    where TRole : IdentityRole<TKey>, IRoleEntity, new()
    where TKey : IEquatable<TKey>
{
    private readonly UserManager<TUser> _userManager;
    private readonly IPublisher _publisher;
    private readonly UserProvisioningOptions _options;
    private readonly ILogger<EventDrivenUserProvisioningStrategy<TUser, TRole, TKey>> _logger;

    public string StrategyName => "EventDriven";

    public EventDrivenUserProvisioningStrategy(
        UserManager<TUser> userManager,
        IPublisher publisher,
        IOptions<UserProvisioningOptions> options,
        ILogger<EventDrivenUserProvisioningStrategy<TUser, TRole, TKey>> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<Guid>> ProvisionUserAsync(UserProvisioningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 確保當前執行的策略與設定相符
            if (!string.Equals(_options.StrategyType, StrategyName, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<Guid>(Error.Validation("Provision.Strategy.Mismatch", $"當前系統啟用的策略為 '{_options.StrategyType}'，與執行個體不符。"));
            }

            var userByName = await _userManager.FindByNameAsync(context.Username).ConfigureAwait(false);
            if (userByName != null)
            {
                return Result.Failure<Guid>(Error.Validation("Provision.EventDriven.UsernameExists", "該帳號名稱已被使用。"));
            }

            if (!string.IsNullOrWhiteSpace(context.Email))
            {
                var userByEmail = await _userManager.FindByEmailAsync(context.Email).ConfigureAwait(false);
                if (userByEmail != null)
                {
                    return Result.Failure<Guid>(Error.Validation("Provision.EventDriven.EmailExists", "該電子郵件已被註冊。"));
                }
            }

            var user = new TUser
            {
                UserName = context.Username,
                Email = context.Email,
                EmailConfirmed = false,
                LockoutEnabled = true
            };

            var createResult = string.IsNullOrEmpty(context.Password)
                ? await _userManager.CreateAsync(user).ConfigureAwait(false)
                : await _userManager.CreateAsync(user, context.Password).ConfigureAwait(false);

            if (!createResult.Succeeded)
            {
                var errorString = string.Join("; ", createResult.Errors.Select(e => e.Description));
                _logger.LogWarning("事件驅動策略建立帳號失敗: {Errors}", errorString);
                return Result.Failure<Guid>(Error.Validation("Provision.EventDriven.CreateFailed", $"事件驅動建立帳號失敗: {errorString}"));
            }

            Guid userId = user.Id is Guid gId ? gId : (Guid.TryParse(user.Id.ToString(), out var parsedId) ? parsedId : Guid.Empty);

            var domainEvent = new UserCreatedEvent(userId, context.Username, context.DefaultLabId, context.TenantLabId, context.RoleName);
            await _publisher.Publish(domainEvent, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("事件驅動策略 (Option: {Strategy}) 成功建立基礎使用者 {Username} (Id: {UserId}) 並發布 UserCreatedEvent", _options.StrategyType, context.Username, userId);

            return Result.Success(userId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("事件驅動策略建立使用者作業已被取消。Username: {Username}", context.Username);
            return Result.Failure<Guid>(Error.Validation("Provision.EventDriven.Cancelled", "建立使用者作業已取消。"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "事件驅動策略執行時發生未預期例外: {Username}", context.Username);
            return Result.Failure<Guid>(Error.Unexpected("Provision.EventDriven.Exception", "執行事件驅動帳號建立時發生內部系統錯誤。"));
        }
    }
}