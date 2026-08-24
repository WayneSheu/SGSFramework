using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;

namespace SGS.Modules.ORG.Application.Services;

public class UserLabService : IUserLabService
{
    private readonly IUserLabRepository _userLabRepository;
    private readonly ORGDbContext _orgDbContext;
    private readonly ILogger<UserLabService> _logger;

    public UserLabService(
        IUserLabRepository userLabRepository,
        ORGDbContext orgDbContext,
        ILogger<UserLabService> logger)
    {
        _userLabRepository = userLabRepository ?? throw new ArgumentNullException(nameof(userLabRepository));
        _orgDbContext = orgDbContext ?? throw new ArgumentNullException(nameof(orgDbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result> AssignUserToLabAsync(
        Guid userId,
        int labId,
        bool isPrimary,
        string? jobTitle,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. 驗證 ORG 模組內的 Organization 是否存在且未被軟刪除
            var lab = await _orgDbContext.Organizations.FindAsync([labId], cancellationToken);
            if (lab is null || lab.IsDeleted)
            {
                return Result.Failure(Error.NotFound("ORG_LAB_NOT_FOUND", $"指定的實驗室 (ID: {labId}) 不存在或已停用。"));
            }

            // 2. 取得遞迴繼承生效之 EffectiveTenantLabId (若為空，防禦給予預設或報錯)
            var effectiveTenantLabId = lab.EffectiveTenantLabId ?? Guid.Empty;
            if (effectiveTenantLabId == Guid.Empty)
            {
                return Result.Failure(Error.Validation("ORG_INVALID_TENANT_LAB", $"實驗室 (ID: {labId}) 缺少有效的 TenantLabId 配置。"));
            }

            // 3. 依據 isPrimary 旗標呼叫 Factory Method (LabId 傳入 int 型別)
            var mapping = isPrimary
                ? UserLabMapping.CreatePrimary(userId, lab.Id, effectiveTenantLabId, jobTitle)
                : UserLabMapping.CreateSecondary(userId, lab.Id, effectiveTenantLabId, jobTitle);

            // 4. 呼叫主專案 Repository 處理對應資料寫入
            await _userLabRepository.AddOrUpdateSecondaryLabAsync(mapping, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while assigning user {UserId} to lab {LabId}", userId, labId);
            return Result.Failure(Error.Failure("ORG_USER_LAB_ASSIGN_ERROR", "指派使用者實驗室過程發生錯誤。"));
        }
    }
}