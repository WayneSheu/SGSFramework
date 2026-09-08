namespace SGSFramework.AuthTokenBucket.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Commands.MenuItems;
using SGSFramework.AuthTokenBucket.Models;
using SGSFramework.AuthTokenBucket.Queries.Menuitems;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 選單管理控制器 (提供選單樹狀查詢、動態維護、節點移動與種子手動同步)
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("v1")]
[Route("api/core/menu-items")]
[ControllerTitle("選單管理", Icon = "fa-solid fa-bars-staggered", Order = 90, Description = "維護系統三層動態導覽選單 (Section -> Group -> Page) 與權限綁定")]
[RequiresPermission("SYS.MENU.READ")]
public class MenuItemController : ApiControllerBase
{
    private readonly ILogger<MenuItemController> _logger;
    private readonly IMediator _mediator;
    private readonly IMenuSeedService _menuSeedService;

    public MenuItemController(
        ILogger<MenuItemController> logger,
        IMediator mediator,
        IMenuSeedService menuSeedService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _menuSeedService = menuSeedService ?? throw new ArgumentNullException(nameof(menuSeedService));
    }

    /// <summary>
    /// 取得當前登入使用者權限內之渲染選單樹 (Vue / Blazor 導覽列用)
    /// </summary>
    [HttpGet("user-tree")]
    [Function("GetUserMenuTree", "取得使用者動態選單", Icon = "fa-solid fa-sitemap", Order = 1, Description = "依據當前使用者權限過濾後回傳三層選單樹")]
    [RequiresPermission("SYS.MENU.GETUSERMENUTREE")]
    [ProducesResponseType(typeof(Result<List<MenuItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserMenuTree(CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetUserMenuTreeQuery();
            var result = await _mediator.Send(query, cancellationToken);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得使用者動態選單時發生未預期例外。");
            return StatusCode(StatusCodes.Status500InternalServerError, "內部伺服器發生錯誤。");
        }
    }

    /// <summary>
    /// 取得完整選單樹結構 (後台選單管理維護用)
    /// </summary>
    [HttpGet("tree")]
    [Function("GetFullMenuTree", "取得完整選單管理樹", Icon = "fa-solid fa-tree", Order = 2, Description = "取得包含未啟用/隱藏節點之完整選單階層樹", IsMenu = true, Path = "/system/menu-management")]
    [RequiresPermission("SYS.MENU.GETFULLMENUTREE")]
    [ProducesResponseType(typeof(Result<List<MenuItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFullMenuTree(CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetFullMenuTreeQuery();
            var result = await _mediator.Send(query, cancellationToken);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得完整選單管理樹時發生未預期例外。");
            return StatusCode(StatusCodes.Status500InternalServerError, "內部伺服器發生錯誤。");
        }
    }

    /// <summary>
    /// 依據識別碼取得單一選單節點詳細資料
    /// </summary>
    [HttpGet("{id:guid}")]
    [Function("GetMenuItemById", "取得選單節點詳情", Icon = "fa-solid fa-circle-info", Order = 3)]
    [RequiresPermission("SYS.MENU.GETBYID")]
    [ProducesResponseType(typeof(Result<MenuItemDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMenuItemById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Result.Failure<MenuItemDetailDto>(
                Error.Validation("MENU_INVALID_ID", "選單節點識別碼不可為空。")));
        }

        try
        {
            var query = new GetMenuItemByIdQuery(id);
            var result = await _mediator.Send(query, cancellationToken);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "讀取選單節點時發生例外, ID: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "內部伺服器發生錯誤。");
        }
    }

    /// <summary>
    /// 手動新增自訂選單節點
    /// </summary>
    [HttpPost]
    [Function("CreateMenuItem", "新增選單節點", Icon = "fa-solid fa-plus", Order = 4)]
    [RequiresPermission("SYS.MENU.CREATE")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMenuItem([FromBody] CreateMenuItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            if (result.IsSuccess)
            {
                _logger.LogInformation("成功建立選單節點: Key={Key}, Title={Title}", command.Key, command.Title);
            }
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增選單節點時發生例外, Key: {Key}", command.Key);
            return StatusCode(StatusCodes.Status500InternalServerError, "內部伺服器發生錯誤。");
        }
    }

    /// <summary>
    /// 編輯選單節點屬性
    /// </summary>
    [HttpPut("{id:guid}")]
    [Function("UpdateMenuItem", "更新選單節點", Icon = "fa-solid fa-pen-to-square", Order = 5)]
    [RequiresPermission("SYS.MENU.UPDATE")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMenuItem(
        [FromRoute] Guid id,
        [FromBody] UpdateMenuItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (id == Guid.Empty)
        {
            return BadRequest(Result.Failure<bool>(
                Error.Validation("MENU_INVALID_ID", "選單節點識別碼不可為空。")));
        }

        try
        {
            var command = new UpdateMenuItemCommand(
                id,
                request.ParentId,
                request.Key,
                request.Title,
                request.Path,
                request.Component,
                request.Icon,
                request.Order,
                request.Type,
                request.ModuleName,
                request.PermissionKey,
                request.IsActive,
                request.IsVisible);

            var result = await _mediator.Send(command, cancellationToken);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新選單節點時發生例外, ID: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "內部伺服器發生錯誤。");
        }
    }

    /// <summary>
    /// 調整選單節點階層 (變更 ParentId 與排序)
    /// </summary>
    [HttpPatch("{id:guid}/move")]
    [Function("MoveMenuItem", "移動選單節點", Icon = "fa-solid fa-arrows-up-down-left-right", Order = 6)]
    [RequiresPermission("SYS.MENU.MOVE")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MoveMenuItem(
        [FromRoute] Guid id,
        [FromBody] MoveMenuItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (id == Guid.Empty)
        {
            return BadRequest(Result.Failure<bool>(
                Error.Validation("MENU_INVALID_ID", "選單節點識別碼不可為空。")));
        }

        try
        {
            var command = new MoveMenuItemCommand(id, request.TargetParentId, request.NewOrder);
            var result = await _mediator.Send(command, cancellationToken);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移動選單節點時發生例外, ID: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "內部伺服器發生錯誤。");
        }
    }

    /// <summary>
    /// 刪除選單節點 (若包含子節點則拒絕刪除)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Function("DeleteMenuItem", "刪除選單節點", Icon = "fa-solid fa-trash", Order = 7)]
    [RequiresPermission("SYS.MENU.DELETE")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteMenuItem([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Result.Failure<bool>(
                Error.Validation("MENU_INVALID_ID", "選單節點識別碼不可為空。")));
        }

        try
        {
            var command = new DeleteMenuItemCommand(id);
            var result = await _mediator.Send(command, cancellationToken);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除選單節點時發生例外, ID: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "內部伺服器發生錯誤。");
        }
    }

    /// <summary>
    /// 手動觸發從 API Attributes 自動同步選單種子 (Non-destructive)
    /// </summary>
    [HttpPost("sync-seed")]
    [Function("SyncMenuSeed", "手動同步選單種子", Icon = "fa-solid fa-rotate", Order = 8)]
    [RequiresPermission("SYS.MENU.SYNCSEED")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SyncMenuSeed(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("收到手動觸發選單種子同步請求。");
            await _menuSeedService.SeedAndSyncMenusAsync(cancellationToken);
            return Ok(Result.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行手動選單種子同步時發生例外。");
            return StatusCode(StatusCodes.Status500InternalServerError, "內部伺服器發生錯誤。");
        }
    }
}