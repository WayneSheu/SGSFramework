namespace SGS.Modules.ORG.Application.Features.Laboratories.Command;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;

/// <summary>
/// 停用指定實驗室/組織主體 Command（含子樹聯動停用）
/// </summary>
public sealed record DeactivateLaboratoryCommand(int Id, string Reason) : IRequest<Result<bool>>;

/// <summary>
/// 停用實驗室/組織主體指令處理器
/// </summary>
public sealed class DeactivateLaboratoryCommandHandler : IRequestHandler<DeactivateLaboratoryCommand, Result<bool>>
{
    private readonly ORGDbContext _dbContext;
    private readonly ILogger<DeactivateLaboratoryCommandHandler> _logger;

    public DeactivateLaboratoryCommandHandler(
        ORGDbContext dbContext,
        ILogger<DeactivateLaboratoryCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(DeactivateLaboratoryCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Id <= 0)
        {
            return Result.Failure<bool>(
                Error.Validation("ORG_INVALID_ID", "實驗室識別碼必須大於 0。"));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Failure<bool>(
                Error.Validation("ORG_INVALID_REASON", "停用原因為必填項目。"));
        }

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // 1. 查詢目標主節點
            var targetOrg = await _dbContext.Organizations
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                .ConfigureAwait(false);

            if (targetOrg is null)
            {
                return Result.Failure<bool>(
                    Error.NotFound("ORG_LAB_NOT_FOUND", $"找不到識別碼為 {request.Id} 的實驗室。"));
            }

            if (!targetOrg.IsActive)
            {
                return Result.Failure<bool>(
                    Error.Validation("ORG_LAB_ALREADY_INACTIVE", $"實驗室 '{targetOrg.Name}' 目前已處於停用狀態。"));
            }

            // 2. 透過物化路徑 (NodePath) 抓取該節點及其底下所有「有效」的子樹節點
            var nodePathPrefix = targetOrg.NodePath ?? string.Empty;
            var subTreeNodes = await _dbContext.Organizations
                .Where(x => x.IsActive && !x.IsDeleted && x.NodePath != null && x.NodePath.StartsWith(nodePathPrefix))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // 3. 執行領域邏輯：父節點與所有子樹節點一併停用並記錄原因
            var cascadeReason = $"[聯動停用] 上層組織 (ID: {targetOrg.Id}, 名稱: {targetOrg.Name}) 停用。原因：{request.Reason}";

            foreach (var node in subTreeNodes)
            {
                if (node.Id == targetOrg.Id)
                {
                    // 目標實體直接使用傳入原因
                    node.Deactivate(request.Reason);
                }
                else
                {
                    // 子樹節點記錄聯動停用原因
                    node.Deactivate(cascadeReason);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "成功停用實驗室及其子樹: TargetID={Id}, Reason={Reason}, 共影響 {Count} 個節點",
                request.Id,
                request.Reason,
                subTreeNodes.Count);

            return Result.Success(true);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "停用實驗室及其子樹時發生資料庫寫入錯誤: ID={Id}", request.Id);
            return Result.Failure<bool>(
                Error.Failure("ORG_DEACTIVATE_LAB_DB_ERROR", $"停用實驗室時發生資料庫寫入錯誤：{ex.Message}"));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "停用實驗室時發生未預期例外: ID={Id}", request.Id);
            return Result.Failure<bool>(
                Error.Failure("ORG_DEACTIVATE_LAB_ERROR", $"停用實驗室時發生未預期錯誤：{ex.Message}"));
        }
    }
}