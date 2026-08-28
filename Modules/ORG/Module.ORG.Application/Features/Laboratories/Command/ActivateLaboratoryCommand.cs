namespace SGS.Modules.ORG.Application.Features.Laboratories.Command;

using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;

/// <summary>
/// 啟用實驗室指令
/// </summary>
public sealed record ActivateLaboratoryCommand(int Id) : IRequest<Result<bool>>;

/// <summary>
/// 啟用實驗室指令處理器
/// </summary>
public sealed class ActivateLaboratoryCommandHandler : IRequestHandler<ActivateLaboratoryCommand, Result<bool>>
{
    private readonly ORGDbContext _dbContext;
    private readonly ILogger<ActivateLaboratoryCommandHandler> _logger;

    public ActivateLaboratoryCommandHandler(
        ORGDbContext dbContext,
        ILogger<ActivateLaboratoryCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(ActivateLaboratoryCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Id <= 0)
        {
            return Result.Failure<bool>(
                Error.Validation("ORG_INVALID_ID", "實驗室識別碼必須大於 0。"));
        }

        try
        {
            // 1. 查詢目標實體及其上層組織 (Parent)
            var org = await _dbContext.Organizations
                .Include(x => x.Parent)
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                .ConfigureAwait(false);

            if (org is null)
            {
                return Result.Failure<bool>(
                    Error.NotFound("ORG_LAB_NOT_FOUND", $"找不到識別碼為 {request.Id} 的實驗室。"));
            }

            // 2. 冪等性檢查
            if (org.IsActive)
            {
                return Result.Failure<bool>(
                    Error.Validation("ORG_LAB_ALREADY_ACTIVE", $"實驗室 '{org.Name}' 目前已處於啟用狀態。"));
            }

            // 3. 業務規則檢查：若存在上層組織，且上層組織處於停用狀態，則禁止啟用
            if (org.Parent is not null && !org.Parent.IsActive)
            {
                return Result.Failure<bool>(
                    Error.Validation(
                        "ORG_PARENT_INACTIVE",
                        $"無法啟用實驗室 '{org.Name}'，因為其上層組織 '{org.Parent.Name}' (ID: {org.Parent.Id}) 目前處於停用狀態。請先啟用上層組織。"));
            }

            // 4. 呼叫 Domain Entity 內部業務邏輯封裝
            org.Activate();

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("成功啟用實驗室: ID={Id}, Name={Name}", request.Id, org.Name);
            return Result.Success(true);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "啟用實驗室時發生資料庫寫入錯誤: ID={Id}", request.Id);
            return Result.Failure<bool>(
                Error.Failure("ORG_ACTIVATE_LAB_DB_ERROR", $"啟用實驗室時發生資料庫寫入錯誤：{ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "啟用實驗室時發生未預期例外: ID={Id}", request.Id);
            return Result.Failure<bool>(
                Error.Failure("ORG_ACTIVATE_LAB_ERROR", $"啟用實驗室時發生未預期錯誤：{ex.Message}"));
        }
    }
}