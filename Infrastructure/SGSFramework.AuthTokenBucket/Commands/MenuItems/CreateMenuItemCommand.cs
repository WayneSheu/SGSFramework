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
    /// 建立新選單項目之 Command
    /// </summary>
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
    /// <param name="IsActive">是否啟用 (預設為 true)</param>
    /// <param name="IsVisible">是否顯示於導覽列 (預設為 true)</param>
    public record CreateMenuItemCommand(
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
        bool IsActive = true,
        bool IsVisible = true
    ) : IRequest<Result<Guid>>;


    /// <summary>
    /// 處理 CreateMenuItemCommand，負責驗證資料完整性、重複性並將新選單項目寫入資料庫
    /// </summary>
    public class CreateMenuItemCommandHandler : IRequestHandler<CreateMenuItemCommand, Result<Guid>>
    {
        private readonly ITokenDbContext _dbContext;
        private readonly ILogger<CreateMenuItemCommandHandler> _logger;

        public CreateMenuItemCommandHandler(
            ITokenDbContext dbContext,
            ILogger<CreateMenuItemCommandHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<Guid>> Handle(CreateMenuItemCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // 1. 檢查選單唯一 Key 是否重複
                bool isKeyExists = await _dbContext.MenuItems
                    .AsNoTracking()
                    .AnyAsync(m => m.Key == request.Key, cancellationToken)
                    .ConfigureAwait(false);

                if (isKeyExists)
                {
                    _logger.LogWarning("建立選單失敗，選單 Key '{Key}' 已存在。", request.Key);
                    return Result.Failure<Guid>(
                        Error.Conflict("MENU_KEY_DUPLICATED", $"選單識別碼 Key '{request.Key}' 已存在，無法重複建立。"));
                }

                // 2. 若指定父級 ParentId，驗證父級選單是否存在
                if (request.ParentId.HasValue)
                {
                    bool isParentExists = await _dbContext.MenuItems
                        .AsNoTracking()
                        .AnyAsync(m => m.Id == request.ParentId.Value, cancellationToken)
                        .ConfigureAwait(false);

                    if (!isParentExists)
                    {
                        _logger.LogWarning("建立選單失敗，找不到指定的父級選單 Id: {ParentId}", request.ParentId.Value);
                        return Result.Failure<Guid>(
                            Error.NotFound("PARENT_MENU_NOT_FOUND", $"找不到識別碼為 '{request.ParentId.Value}' 的父級選單。"));
                    }
                }

                // 3. 建構 Entity 物件
                var newMenuItem = new MenuItem
                {
                    Id = Guid.NewGuid(),
                    ParentId = request.ParentId,
                    Key = request.Key.Trim(),
                    DisplayName = request.Title.Trim(),
                    Route = request.Path?.Trim(),
                    Component = request.Component?.Trim(),
                    Icon = request.Icon?.Trim() ?? string.Empty,
                    DisplayOrder = request.Order,
                    Type = request.Type,
                    ModuleName = request.ModuleName.Trim(),
                    PermissionKey = string.IsNullOrWhiteSpace(request.PermissionKey) ? null : request.PermissionKey.Trim(),
                    IsActive = request.IsActive,
                    IsVisible = request.IsVisible
                    //CreatedUtc = DateTime.UtcNow
                };

                // 4. 寫入資料庫並持久化
                _dbContext.MenuItems.Add(newMenuItem);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("成功建立選單項目，Id: {MenuItemId}, Key: {Key}", newMenuItem.Id, newMenuItem.Key);

                return Result.Success(newMenuItem.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "建立選單項目時發生未預期例外，Key: {Key}", request.Key);
                return Result.Failure<Guid>(
                    Error.Failure("CREATE_MENU_ITEM_ERROR", "建立選單項目時發生內部系統錯誤。"));
            }
        }
    }
}
