using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;

namespace SGSFramework.AuthTokenBucket.Queries.Menuitems
{
    /// <summary>
    /// 依據選單識別碼查詢單一選單詳細資料之 Query
    /// </summary>
    /// <param name="Id">選單唯一識別碼</param>
    public record GetMenuItemByIdQuery(Guid Id) : IRequest<Result<MenuItemDetailDto>>;

    /// <summary>
    /// 處理 GetMenuItemByIdQuery，負責讀取指定選單之完整詳細資訊與關聯父節點名稱
    /// </summary>
    public class GetMenuItemByIdQueryHandler : IRequestHandler<GetMenuItemByIdQuery, Result<MenuItemDetailDto>>
    {
        private readonly ITokenDbContext _dbContext;
        private readonly ILogger<GetMenuItemByIdQueryHandler> _logger;

        public GetMenuItemByIdQueryHandler(
            ITokenDbContext dbContext,
            ILogger<GetMenuItemByIdQueryHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<MenuItemDetailDto>> Handle(GetMenuItemByIdQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // 1. 從資料庫讀取該選單項目 Entity
                var menuItem = await _dbContext.MenuItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (menuItem == null)
                {
                    _logger.LogWarning("找不到識別碼為 {MenuItemId} 的選單項目。", request.Id);
                    return Result.Failure<MenuItemDetailDto>(
                        Error.NotFound("MENU_ITEM_NOT_FOUND", $"找不到識別碼為 '{request.Id}' 的選單項目。"));
                }

                // 2. 若存在父節點，查詢父節點名稱 (ParentTitle)
                string? parentTitle = null;
                if (menuItem.ParentId.HasValue)
                {
                    parentTitle = await _dbContext.MenuItems
                        .AsNoTracking()
                        .Where(m => m.Id == menuItem.ParentId.Value)
                        .Select(m => m.DisplayName)
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                // 3. 映射至 MenuItemDetailDto
                var dto = new MenuItemDetailDto
                {
                    Id = menuItem.Id,
                    ParentId = menuItem.ParentId,
                    ParentTitle = parentTitle,
                    Key = menuItem.Key,
                    Title = menuItem.DisplayName,
                    Path = menuItem.Route,
                    Component = menuItem.Component,
                    Icon = menuItem.Icon,
                    Order = menuItem.DisplayOrder,
                    Type = menuItem.Type,
                    ModuleName = menuItem.ModuleName,
                    PermissionKey = menuItem.PermissionKey,
                    IsActive = menuItem.IsActive,
                    IsVisible = menuItem.IsVisible
                    //CreatedAtUtc = menuItem.CreatedAtUtc,
                    //LastModifiedAtUtc = menuItem.LastModifiedUtc
                };

                _logger.LogInformation("成功讀取選單項目詳細資料，Id: {MenuItemId}, Key: {Key}", menuItem.Id, menuItem.Key);

                return Result.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "讀取選單項目詳細資料時發生未預期例外，MenuItemId: {MenuItemId}", request.Id);
                return Result.Failure<MenuItemDetailDto>(
                    Error.Failure("MENU_ITEM_QUERY_ERROR", "讀取選單詳細資料時發生內部系統錯誤。"));
            }
        }
    }
}
