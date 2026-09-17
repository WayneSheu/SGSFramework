// ==========================================
// 檔案路徑: src/Infrastructure/SGSFramework.Identity/Strategies/AtomicUserProvisioningStrategy.cs
// 架構層級: Infrastructure / Strategy Implementation Layer
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Strategies;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Base;
using SGSFramework.Core.Abstractions.Entities.Identities;
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
/// 原子化用戶創建策略 (透過 IOptions 動態識別策略，參數依策略業務規範驗證)
/// </summary>
public sealed class AtomicUserProvisioningStrategy<TUser, TRole, TKey> : IUserProvisioningStrategy
    where TUser : IdentityUser<TKey>, IBaseUser, new()
    where TRole : IdentityRole<TKey>, IRoleEntity, new()
    where TKey : IEquatable<TKey>
{
    private readonly UserManager<TUser> _userManager;
    private readonly RoleManager<TRole> _roleManager;
    private readonly IUserLabRepository _userLabRepository;
    private readonly IOrganizationIntegrationService _organizationIntegrationService;
    private readonly UserProvisioningOptions _options;
    private readonly ILogger<AtomicUserProvisioningStrategy<TUser, TRole, TKey>> _logger;

    public string StrategyName => "Atomic";

    public AtomicUserProvisioningStrategy(
        UserManager<TUser> userManager,
        RoleManager<TRole> roleManager,
        IUserLabRepository userLabRepository,
        IOrganizationIntegrationService organizationIntegrationService,
        IOptions<UserProvisioningOptions> options,
        ILogger<AtomicUserProvisioningStrategy<TUser, TRole, TKey>> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _userLabRepository = userLabRepository ?? throw new ArgumentNullException(nameof(userLabRepository));
        _organizationIntegrationService = organizationIntegrationService ?? throw new ArgumentNullException(nameof(organizationIntegrationService));
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
                return Result.Failure<Guid>(Error.Validation("Provision.Atomic.UsernameExists", "該帳號名稱已被使用。"));
            }

            if (!string.IsNullOrWhiteSpace(context.Email))
            {
                var userByEmail = await _userManager.FindByEmailAsync(context.Email).ConfigureAwait(false);
                if (userByEmail != null)
                {
                    return Result.Failure<Guid>(Error.Validation("Provision.Atomic.EmailExists", "該電子郵件已被註冊。"));
                }
            }

            int resolvedLabId = 0;
            if (context.TenantLabId.HasValue && context.TenantLabId.Value != Guid.Empty)
            {
                var orgInfo = await _organizationIntegrationService.GetOrganizationByIdAsync(context.TenantLabId.Value, cancellationToken).ConfigureAwait(false);
                if (orgInfo == null)
                {
                    return Result.Failure<Guid>(Error.NotFound("Provision.Atomic.LabNotFound", $"找不到對應的租戶實驗室識別碼: {context.TenantLabId}"));
                }
                resolvedLabId = orgInfo.Id;
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
                return Result.Failure<Guid>(Error.Validation("Provision.Atomic.CreateFailed", $"原子化建立使用者失敗: {errorString}"));
            }

            if (!string.IsNullOrWhiteSpace(context.RoleName))
            {
                if (!await _roleManager.RoleExistsAsync(context.RoleName).ConfigureAwait(false))
                {
                    var newRole = new TRole { Name = context.RoleName };
                    await _roleManager.CreateAsync(newRole).ConfigureAwait(false);
                }
                await _userManager.AddToRoleAsync(user, context.RoleName).ConfigureAwait(false);
            }

            Guid userId = user.Id is Guid gId ? gId : (Guid.TryParse(user.Id.ToString(), out var parsedId) ? parsedId : Guid.Empty);

            if (resolvedLabId > 0 && context.TenantLabId.HasValue)
            {
                var labMapping = UserLabMapping.CreatePrimary(
                    userId: userId,
                    labId: resolvedLabId,
                    tenantLabId: context.TenantLabId.Value,
                    jobTitle: "預設職位",
                    operatorId: context.Username
                );

                await _userLabRepository.AddOrUpdateSecondaryLabAsync(labMapping, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("原子化策略 (Option: {Strategy}) 成功建立使用者 {Username}", _options.StrategyType, context.Username);

            return Result.Success(userId);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<Guid>(Error.Validation("Provision.Atomic.Cancelled", "建立使用者作業已取消。"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "原子化策略執行時發生未預期例外: {Username}", context.Username);
            return Result.Failure<Guid>(Error.Unexpected("Provision.Atomic.Exception", "執行原子化帳號建立時發生內部系統錯誤。"));
        }
    }
}