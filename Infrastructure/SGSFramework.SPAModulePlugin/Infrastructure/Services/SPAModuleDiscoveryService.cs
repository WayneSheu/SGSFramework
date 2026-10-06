namespace SGSFramework.SPAModulePlugin.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using SGSFramework.SPAModulePlugin.Application.Abstractions;
using SGSFramework.SPAModulePlugin.Domain.Enums;
using SGSFramework.SPAModulePlugin.Domain.Repositories;
using SGSFramework.SPAModulePlugin.Domain.ValueObjects;

public sealed class SPAModuleDiscoveryService : ISPAModuleDiscoveryService
{
    private readonly ISPAModuleRepository _repository;
    private readonly ILogger<SPAModuleDiscoveryService> _logger;

    public SPAModuleDiscoveryService(ISPAModuleRepository repository, ILogger<SPAModuleDiscoveryService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<SPAModuleManifest>> GetAuthorizedModulesAsync(
        Guid userId,
        SPAFrameworkType frameworkType,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID 不能為空 Guid。", nameof(userId));
        }

        try
        {
            var modules = await _repository.GetValidModulesAsync(frameworkType, cancellationToken).ConfigureAwait(false);

            // TODO: 此處整合 Bitmask 權限或 Role 驗證邏輯，過濾出該 User 有權限存取之模組
            _logger.LogInformation("成功為使用者 [{UserId}] 探測到 {Count} 個受信任且已授權的 [{Framework}] 動態模組",
                userId, modules.Count, frameworkType);

            return modules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "探測 SPA 動態模組時發生不可預期的錯誤，UserId: {UserId}", userId);
            throw;
        }
    }
}