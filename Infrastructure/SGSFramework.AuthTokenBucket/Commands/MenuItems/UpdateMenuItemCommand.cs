using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Commands.MenuItems
{
    /// <summary>
    /// 更新選單項目之 Command
    /// </summary>
    /// <param name="Id">待更新之選單唯一識別碼 Primary Key</param>
    /// <param name="ParentId">父級選單識別碼 (Null 代表為頂層 Section 節點)</param>
    /// <param name="Key">節點識別 Key (例如: "ORG.LABORATORY.LIST")</param>
    /// <param name="Title">顯示名稱</param>
    /// <param name="Path">前端路由路徑 (僅 MenuType.Page 必須填寫)</param>
    /// <param name="Component">前端對應 Component 組件路徑 (適用於 Vue/Blazor 動態載入)</param>
    /// <param name="Icon">圖示類別 (如 FontAwesome: "fa-solid fa-gear")</param>
    /// <param name="Order">顯示排序 (同層級數字越小越靠前)</param>
    /// <param name="Type">選單類型 (Section, Group, Page)</param>
    /// <param name="ModuleName">模組識別碼 (例如: "System", "ORG")</param>
    /// <param name="PermissionKey">關聯權限鍵值</param>
    /// <param name="IsActive">是否啟用</param>
    /// <param name="IsVisible">是否顯示於導覽列</param>
    public record UpdateMenuItemCommand(
        Guid Id,
        Guid? ParentId,
        string Key,
        string Title,
        string? Path,
        string? Component,
        string Icon,
        int Order,
        MenuType Type,
        string ModuleName,
        string? PermissionKey,
        bool IsActive,
        bool IsVisible
    ) : IRequest<Result<bool>>;


    /// <summary>
    /// 處理 UpdateMenuItemCommand，負責驗證資料完整性、循環參照防止與選單內容更新
    /// </summary>
    public class UpdateMenuItemCommandHandler : IRequestHandler<UpdateMenuItemCommand, Result<bool>>
    {
        private readonly ITokenDbContext _dbContext;
        private readonly ILogger<UpdateMenuItemCommandHandler> _logger;

        public UpdateMenuItemCommandHandler(
            ITokenDbContext dbContext,
            ILogger<UpdateMenuItemCommandHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<bool>> Handle(UpdateMenuItemCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // 1. 檢查欲更新之選單項目是否存在
                var menuItem = await _dbContext.MenuItems
                    .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (menuItem == null)
                {
                    _logger.LogWarning("更新選單失敗，找不到識別碼為 {MenuItemId} 的選單項目。", request.Id);
                    return Result.Failure<bool>(
                        Error.NotFound("MENU_ITEM_NOT_FOUND", $"找不到識別碼為 '{request.Id}' 的選單項目。"));
                }

                // 2. 防範自引用與循環節點參照 (ParentId 不能指向自己)
                if (request.ParentId.HasValue && request.ParentId.Value == request.Id)
                {
                    _logger.LogWarning("更新選單失敗，不可將父級選單設為自身 ID: {MenuItemId}", request.Id);
                    return Result.Failure<bool>(
                        Error.Validation("INVALID_PARENT_ID", "父級選單不能設定為節點自身。"));
                }

                // 3. 檢查 Key 是否與其他選單重複 (排除自身)
                string trimmedKey = request.Key.Trim();
                bool isKeyDuplicated = await _dbContext.MenuItems
                    .AsNoTracking()
                    .AnyAsync(m => m.Key == trimmedKey && m.Id != request.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (isKeyDuplicated)
                {
                    _logger.LogWarning("更新選單失敗，選單 Key '{Key}' 已被其他項目使用。", trimmedKey);
                    return Result.Failure<bool>(
                        Error.Conflict("MENU_KEY_DUPLICATED", $"選單識別碼 Key '{trimmedKey}' 已被其他選單項目使用。"));
                }

                // 4. 若有指定父級 ParentId，驗證該父級選單是否存在
                if (request.ParentId.HasValue)
                {
                    bool isParentExists = await _dbContext.MenuItems
                        .AsNoTracking()
                        .AnyAsync(m => m.Id == request.ParentId.Value, cancellationToken)
                        .ConfigureAwait(false);

                    if (!isParentExists)
                    {
                        _logger.LogWarning("更新選單失敗，找不到指定的父級選單 Id: {ParentId}", request.ParentId.Value);
                        return Result.Failure<bool>(
                            Error.NotFound("PARENT_MENU_NOT_FOUND", $"找不到識別碼為 '{request.ParentId.Value}' 的父級選單。"));
                    }
                }

                // 5. 更新 Entity 欄位
                menuItem.ParentId = request.ParentId;
                menuItem.Key = trimmedKey;
                menuItem.DisplayName = request.Title.Trim();
                menuItem.Route = request.Path?.Trim();
                menuItem.Component = request.Component?.Trim();
                menuItem.Icon = request.Icon?.Trim() ?? string.Empty;
                menuItem.DisplayOrder = request.Order;
                menuItem.Type = request.Type;
                menuItem.ModuleName = request.ModuleName.Trim();
                menuItem.PermissionKey = string.IsNullOrWhiteSpace(request.PermissionKey) ? null : request.PermissionKey.Trim();
                menuItem.IsActive = request.IsActive;
                menuItem.IsVisible = request.IsVisible;
                //menuItem.LastModifiedUtc = DateTime.UtcNow;

                // 6. 持久化異動至資料庫
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("成功更新選單項目，Id: {MenuItemId}, Key: {Key}", menuItem.Id, menuItem.Key);

                return Result.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新選單項目時發生未預期例外，MenuItemId: {MenuItemId}", request.Id);
                return Result.Failure<bool>(
                    Error.Failure("UPDATE_MENU_ITEM_ERROR", "更新選單項目時發生內部系統錯誤。"));
            }
        }
    }
}
