using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Commands.MenuItems
{
    /// <summary>
    /// 移動選單節點與調整階層結構之 Command
    /// </summary>
    /// <param name="Id">待移動之選單唯一識別碼 Primary Key</param>
    /// <param name="TargetParentId">目標父級選單識別碼 (Null 代表提昇至頂層 Section)</param>
    /// <param name="NewOrder">新同層級顯示排序</param>
    public record MoveMenuItemCommand(
        Guid Id,
        Guid? TargetParentId,
        int NewOrder
    ) : IRequest<Result<bool>>;

    /// <summary>
    /// 處理 MoveMenuItemCommand，負責驗證節點存在性、防止樹狀結構循環參照 (Cycle Detection) 並更新選單階層與排序
    /// </summary>
    public class MoveMenuItemCommandHandler : IRequestHandler<MoveMenuItemCommand, Result<bool>>
    {
        private readonly ITokenDbContext _dbContext;
        private readonly ILogger<MoveMenuItemCommandHandler> _logger;

        public MoveMenuItemCommandHandler(
            ITokenDbContext dbContext,
            ILogger<MoveMenuItemCommandHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<bool>> Handle(MoveMenuItemCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // 1. 檢查待移動之選單項目是否存在
                var targetItem = await _dbContext.MenuItems
                    .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (targetItem == null)
                {
                    _logger.LogWarning("移動選單失敗，找不到識別碼為 {MenuItemId} 的選單項目。", request.Id);
                    return Result.Failure<bool>(
                        Error.NotFound("MENU_ITEM_NOT_FOUND", $"找不到識別碼為 '{request.Id}' 的選單項目。"));
                }

                // 2. 驗證自引用 (不可將父級設為自身)
                if (request.TargetParentId.HasValue && request.TargetParentId.Value == request.Id)
                {
                    _logger.LogWarning("移動選單失敗，不可將目標父級選單設為自身 ID: {MenuItemId}", request.Id);
                    return Result.Failure<bool>(
                        Error.Validation("INVALID_PARENT_ID", "不可將選單項目的父級設定為自身。"));
                }

                // 3. 若有指定 TargetParentId，驗證目標父級選單是否存在
                if (request.TargetParentId.HasValue)
                {
                    bool isParentExists = await _dbContext.MenuItems
                        .AsNoTracking()
                        .AnyAsync(m => m.Id == request.TargetParentId.Value, cancellationToken)
                        .ConfigureAwait(false);

                    if (!isParentExists)
                    {
                        _logger.LogWarning("移動選單失敗，找不到指定的目標父級選單 Id: {ParentId}", request.TargetParentId.Value);
                        return Result.Failure<bool>(
                            Error.NotFound("TARGET_PARENT_NOT_FOUND", $"找不到識別碼為 '{request.TargetParentId.Value}' 的目標父級選單。"));
                    }

                    // 4. 防止樹狀結構循環參照 (Tree Cycle Detection)：檢查 TargetParentId 是否為當前節點之子孫節點
                    Guid? currentCheckId = request.TargetParentId.Value;

                    while (currentCheckId.HasValue)
                    {
                        if (currentCheckId.Value == request.Id)
                        {
                            _logger.LogWarning("移動選單失敗，檢測到循環參照：不可將選單 {MenuItemId} 移動至其子孫節點 {TargetParentId} 之下。",
                                request.Id, request.TargetParentId.Value);

                            return Result.Failure<bool>(
                                Error.Validation("CIRCULAR_DEPENDENCY_DETECTED", "不可將選單項目移動至其子孫節點之下，此操作會導致樹狀結構無窮迴圈。"));
                        }

                        // 向上追溯父級節點 Id
                        currentCheckId = await _dbContext.MenuItems
                            .AsNoTracking()
                            .Where(m => m.Id == currentCheckId.Value)
                            .Select(m => m.ParentId)
                            .FirstOrDefaultAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                // 5. 更新選單階層與同層級排序
                targetItem.ParentId = request.TargetParentId;
                targetItem.DisplayOrder = request.NewOrder;
                //targetItem.LastModifiedUtc = DateTime.UtcNow;

                // 6. 持久化異動至資料庫
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("成功移動選單項目，Id: {MenuItemId}, 新 ParentId: {ParentId}, 新排序: {Order}",
                    targetItem.Id, targetItem.ParentId, targetItem.DisplayOrder);

                return Result.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "移動選單項目時發生未預期例外，MenuItemId: {MenuItemId}", request.Id);
                return Result.Failure<bool>(
                    Error.Failure("MOVE_MENU_ITEM_ERROR", "移動選單項目時發生內部系統錯誤。"));
            }
        }
    }
}
