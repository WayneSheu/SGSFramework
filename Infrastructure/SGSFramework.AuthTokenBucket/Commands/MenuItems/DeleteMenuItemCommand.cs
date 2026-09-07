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
    /// 刪除選單項目之 Command
    /// </summary>
    /// <param name="Id">待刪除之選單唯一識別碼 Primary Key</param>
    public record DeleteMenuItemCommand(Guid Id) : IRequest<Result<bool>>;


    /// <summary>
    /// 處理 DeleteMenuItemCommand，負責驗證選單存在性、子節點依附狀態並執行安全刪除
    /// </summary>
    public class DeleteMenuItemCommandHandler : IRequestHandler<DeleteMenuItemCommand, Result<bool>>
    {
        private readonly ITokenDbContext _dbContext;
        private readonly ILogger<DeleteMenuItemCommandHandler> _logger;

        public DeleteMenuItemCommandHandler(
            ITokenDbContext dbContext,
            ILogger<DeleteMenuItemCommandHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<bool>> Handle(DeleteMenuItemCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // 1. 檢查待刪除之選單項目是否存在
                var menuItem = await _dbContext.MenuItems
                    .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (menuItem == null)
                {
                    _logger.LogWarning("刪除選單失敗，找不到識別碼為 {MenuItemId} 的選單項目。", request.Id);
                    return Result.Failure<bool>(
                        Error.NotFound("MENU_ITEM_NOT_FOUND", $"找不到識別碼為 '{request.Id}' 的選單項目。"));
                }

                // 2. 檢查是否存在下屬子節點 (保護樹狀結構完整性，禁止直接孤立子節點)
                bool hasChildren = await _dbContext.MenuItems
                    .AsNoTracking()
                    .AnyAsync(m => m.ParentId == request.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (hasChildren)
                {
                    _logger.LogWarning("刪除選單失敗，選單項目 {MenuItemId} 尚包含子選單節點。", request.Id);
                    return Result.Failure<bool>(
                        Error.Validation("HAS_CHILD_NODES", "該選單項目下尚包含其他子選單，請先移除或轉移子選單後再行刪除。"));
                }

                // 3. 移除 Entity 並持久化異動
                _dbContext.MenuItems.Remove(menuItem);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("成功刪除選單項目，Id: {MenuItemId}, Key: {Key}", menuItem.Id, menuItem.Key);

                return Result.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刪除選單項目時發生未預期例外，MenuItemId: {MenuItemId}", request.Id);
                return Result.Failure<bool>(
                    Error.Failure("DELETE_MENU_ITEM_ERROR", "刪除選單項目時發生內部系統錯誤。"));
            }
        }
    }


}
