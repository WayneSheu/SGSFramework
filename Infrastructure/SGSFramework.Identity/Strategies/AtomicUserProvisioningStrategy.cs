

namespace SGSFramework.Identity.Strategies;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.Abstractions.Strategies;
using SGSFramework.Identity.DTOs;
using SGSFramework.Identity.DTOs.Users;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 
/// </summary>
public sealed class AtomicUserProvisioningStrategy : IUserProvisioningStrategy
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IUserLabRepository _userLabRepository;
    private readonly ILogger<AtomicUserProvisioningStrategy> _logger;

    public string StrategyName => "Atomic";

    public AtomicUserProvisioningStrategy(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IUserLabRepository userLabRepository,
        ILogger<AtomicUserProvisioningStrategy> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _userLabRepository = userLabRepository ?? throw new ArgumentNullException(nameof(userLabRepository));
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
                return Result.Failure<Guid>(Error.Failure("Provision.Atomic.CreateFailed", $"原子化建立使用者失敗: {errors}"));
            }

            if (!await _roleManager.RoleExistsAsync(context.RoleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(context.RoleName));
            }
            await _userManager.AddToRoleAsync(user, context.RoleName);

            var userId = Guid.Parse(user.Id);
            // 透過領域模型靜態原廠方法建立實體，避開 private set 存取限制
            var labMapping = UserLabMapping.CreatePrimary(
                userId: userId,
                labId: context.DefaultLabId,
                tenantLabId: context.TenantLabId,
                jobTitle: "預設職位",
                operatorId: context.Username
            );

            await _userLabRepository.AddOrUpdateSecondaryLabAsync(labMapping, cancellationToken);
            _logger.LogInformation("原子化策略(方案一)成功建立使用者 {Username} 及其預設實驗室與角色", context.Username);

            return Result.Success(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "原子化策略(方案一)執行時發生未預期例外: {Username}", context.Username);
            return Result.Failure<Guid>(Error.Failure("Provision.Atomic.Exception", "執行原子化帳號建立時發生內部系統錯誤"));
        }
    }
}