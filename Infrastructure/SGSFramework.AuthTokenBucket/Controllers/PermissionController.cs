// ==========================================
// 檔案路徑: Presentation/SGSFramework.AuthTokenBucket/Controllers/v1/PermissionController.cs
// 架構層級: Presentation Layer (Controller Implementation)
// ==========================================

#nullable enable

using GSFramework.AuthTokenBucket.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.AuthTokenBucket.DTOs.PermissionTree;
using SGSFramework.AuthTokenBucket.DTOs.PermissionUsers;
using SGSFramework.AuthTokenBucket.DTOs.RolePermissions;
using SGSFramework.AuthTokenBucket.DTOs.UserPermissions;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Controllers.Base;
using System.Diagnostics;
using System.Net.Mime;
using System.Security.Claims;

namespace SGSFramework.AuthTokenBucket.Controllers.v1;

/// <summary>
/// 權限管理控制器
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/permissions")]
[ControllerTitle("權限管理", Icon = "fa-solid fa-shield-halved", Order = 20, Description = "提供系統權限樹狀圖查詢、角色權限矩陣讀取與更新服務")]
[RequiresPermission("SYSTEM.PERMISSION.READ", "權限管理")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public sealed class PermissionController : ApiControllerBase
{
    private readonly IMemoryCache _memoryCache;
    private readonly IPermissionManagementService _permissionService;
    private readonly IPermissionBitmaskService _bitmaskService;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserPermissionRepository _userPermissionRepository;
    private readonly ILogger<PermissionController> _logger;

    public PermissionController(
        IMemoryCache memoryCache,
        IPermissionManagementService permissionService,
        IPermissionBitmaskService bitmaskService,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IUserPermissionRepository userPermissionRepository,
        ILogger<PermissionController> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _bitmaskService = bitmaskService ?? throw new ArgumentNullException(nameof(bitmaskService));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _userPermissionRepository = userPermissionRepository ?? throw new ArgumentNullException(nameof(userPermissionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }


    private const string PermissionTreeCacheKey = "Cache_System_Permission_Tree";

    /// <summary>
    /// 同步權限元數據並回傳 DTO 投影資料清單
    /// </summary>
    [HttpPost("metadata/sync")]
    [Function("SyncPermissionMetadata", "同步權限元數據", Icon = "fa-solid fa-sitemap", Order = 1, Description = "同步權限元數據並回傳 DTO 投影資料清單",IsMenu =true)]
    [RequiresPermission("SYSTEM.PERMISSION.READ", "權限管理")]
    [EndpointSummary("同步權限元數據")]
    [EndpointDescription("同步權限元數據並回傳 DTO 投影資料清單")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncPermissionMetadata(CancellationToken cancellationToken = default)
    {
        try
        {
            var syncedMetadataList = await _permissionService.SyncPermissionMetadataAsync(cancellationToken).ConfigureAwait(false);
            int affectedCount = syncedMetadataList.Count;

            _memoryCache.Remove(PermissionTreeCacheKey);
            _logger.LogInformation("手動觸發同步 PermissionMetadata 成功，影響筆數: {Count}", affectedCount);

            return Ok(new
            {
                success = true,
                message = $"權限元數據同步成功，共更新 {affectedCount} 筆資料。",
                count = affectedCount,
                data = syncedMetadataList
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步 PermissionMetadata 時發生未預期異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "同步權限元數據失敗。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 取得完整系統與動態模組權限清單 (階層式：Module -> Function -> ReadPermission / ActionPermissions)
    /// </summary>
    [HttpGet("tree")]
    [Function("GetPermissionTree", "系統權限清單", Icon = "fa-solid fa-sitemap", Order = 1, Description = "取得完整系統與動態模組權限清單", IsMenu = false)]
    [ProducesResponseType(typeof(List<PermissionModuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.PERMISSION.READ")]
    [EndpointSummary("系統權限清單")]
    [EndpointDescription("取得完整系統與動態模組權限清單 (階層式：Module -> Function -> Actions)")]
    public async Task<IActionResult> GetPermissionTree(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var tree = await _memoryCache.GetOrCreateAsync(PermissionTreeCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.Priority = CacheItemPriority.High;

                _logger.LogInformation("[PermissionController] 重新載入系統權限樹狀結構至記憶體快取。");
                var data = await _permissionService.GetPermissionTreeAsync(cancellationToken).ConfigureAwait(false);
                return data ?? new List<PermissionModuleDto>();
            }).ConfigureAwait(false);

            stopwatch.Stop();
            _logger.LogDebug("[PermissionController] 取得權限樹狀結構耗時: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

            return Ok(tree ?? new List<PermissionModuleDto>());
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PermissionController] 取得權限樹狀結構作業已取消。");
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[PermissionController] 取得權限樹狀結構時發生未預期異常。耗時: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "無法取得權限樹狀結構。",
                Instance = HttpContext.Request.Path
            });
        }
    }


    /// <summary>
    /// 取得指定角色的權限設定清單
    /// </summary>
    [HttpGet("role/{roleId}")]
    [Function("GetRolePermissions", "角色權限清單", Icon = "fa-solid fa-user-shield", Order = 2, Description = "取得指定角色的全域權限設定清單與 Bitmask 映射矩陣", IsMenu = false)]
    [RequiresPermission("SYSTEM.PERMISSION.READ")]
    [EndpointSummary("角色全域權限清單")]
    [EndpointDescription("取得指定角色的全域權限設定清單與 Bitmask 映射矩陣。")]
    [ProducesResponseType(typeof(RolePermissionMatrixDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RolePermissionMatrixDto>> GetRolePermissions(
        [FromRoute] string roleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(roleId);

        try
        {
            var result = await _permissionService.GetRoleGlobalPermissionsAsync(roleId, cancellationToken);
            if (result == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "資源不存在",
                    Detail = $"找不到指定 RoleId: {roleId} 的權限配置資訊。",
                    Instance = HttpContext.Request.Path
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得角色 {RoleId} 的全域權限配置時發生異常。", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "無法取得角色權限資料。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 更新指定角色的全域權限 (Global Permissions)
    /// </summary>
    [HttpPost("role/global/update")]
    [Function("UpdateRoleGlobalPermissions", "更新角色全域權限", Icon = "fa-solid fa-user-gear", Order = 3, Description = "更新指定角色的全域 Bitmask 權限配置", IsMenu = false)]
    [RequiresPermission("SYSTEM.PERMISSION.ASSIGN_ROLE", "角色的全域權限")]
    [EndpointSummary("更新角色全域權限")]
    [EndpointDescription("更新指定角色的全域權限關聯配置與 Bitmask 設定")]
    [ProducesResponseType(typeof(UpdateRoleGlobalPermissionsCommandResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateRoleGlobalPermissions(
        [FromBody] UpdateRoleGlobalPermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        if (string.IsNullOrWhiteSpace(request.RoleId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "請求參數無效",
                Detail = "RoleId 不得為空。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            // 自動從 HttpContext Claims 中安全地讀取當前操作者 UserId
            string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var result = await _permissionService.UpdateRolePermissionsAsync(request, cancellationToken).ConfigureAwait(false); 
            if (!result.Succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "全域權限更新失敗",
                    Detail = result.Message,
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogInformation("成功更新角色 {RoleId} 的全域權限配置，操作者: {OperatorId}。", request.RoleId, currentUserId ?? "System");
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("更新角色 {RoleId} 的全域權限作業已取消。", request.RoleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新角色 {RoleId} 全域權限時發生未預期異常。", request.RoleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "更新角色全域權限程序執行失敗。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 取得指定使用者的所有權限總覽 (包含從 User_Global_Permissions Bitmask 解碼之直接權限)
    /// </summary>
    [HttpGet("user/{userId:guid}/audit-permissions")]
    [Function("GetUserAllPermissions", "檢視使用者權限", Icon = "fa-solid fa-user-shield", Order = 4, Description = "取得指定使用者的直接權限與透過角色繼承的有效權限總覽，供資安稽核使用。", IsMenu = false)]
    [RequiresPermission("SYSTEM.PERMISSION.READ")]
    [EndpointSummary("檢視使用者權限")]
    [EndpointDescription("取得指定使用者的直接權限與透過角色繼承的有效權限總覽，供資安稽核與彈出對話框回顯使用。")]
    [ProducesResponseType(typeof(UserAuditPermissionsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserAllPermissions(
        [FromRoute] Guid userId,
        [FromQuery] Guid? tenantLabId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _permissionService.GetUserAllPermissionsAsync(
                userId.ToString(),
                tenantLabId,
                cancellationToken).ConfigureAwait(false);

            if (response == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "使用者不存在",
                    Detail = $"找不到識別碼為 '{userId}' 的使用者。",
                    Instance = HttpContext.Request.Path
                });
            }

            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("查詢使用者權限稽核資料作業已取消。UserId: {UserId}", userId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢使用者權限稽核資料時發生異常。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "查詢使用者權限稽核資料時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }


    /// <summary>
    /// 取得指定角色的所有成員與權限總覽
    /// </summary>
    [HttpGet("role/{roleId}/audit")]
    [Function("GetRoleMemberPermissions", "檢視角色的成員與權限", Icon = "fa-solid fa-users-gear", Order = 5, Description = "取得指定角色的所屬成員清單與對應權限配置，供資安稽核使用。", IsMenu = false)]
    [RequiresPermission("SYSTEM.PERMISSION.READ")]
    [EndpointSummary("檢視角色的成員與權限")]
    [EndpointDescription("取得指定角色的所屬成員清單與對應權限配置，供資安稽核使用。")]
    [ProducesResponseType(typeof(RoleAuditDetailsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRoleMemberPermissions(
        [FromRoute] string roleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(roleId);

        try
        {
            var rolePermissions = await _permissionService.GetRoleGlobalPermissionsAsync(roleId, cancellationToken);
            if (rolePermissions == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "資源不存在",
                    Detail = $"找不到指定 RoleId: {roleId} 的權限配置資訊。",
                    Instance = HttpContext.Request.Path
                });
            }

            string targetRoleName = rolePermissions.RoleName ?? roleId;
            var members = new List<RoleMemberDto>();

            var users = await _userManager.Users.ToListAsync(cancellationToken);
            foreach (var user in users)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var userRoles = await _userManager.GetRolesAsync(user);

                if (userRoles.Contains(targetRoleName, StringComparer.OrdinalIgnoreCase) || userRoles.Contains(roleId))
                {
                    members.Add(new RoleMemberDto
                    {
                        UserId = user.Id.ToString(),
                        Username = user.UserName ?? string.Empty,
                        Email = user.Email ?? string.Empty
                    });
                }
            }

            var response = new RoleAuditDetailsResponseDto
            {
                RoleId = roleId,
                RoleName = targetRoleName,
                Members = members,
                Permissions = rolePermissions
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢角色 {RoleId} 的稽核成員與權限時發生異常。", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "查詢角色稽核資料時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 指派/更新指定使用者的直接 API 權限清單
    /// </summary>
    [HttpPut("user/{userId:guid}/permissions")]
    [Function("AssignUserPermissions", "指派使用者權限", Icon = "fa-solid fa-key", Order = 6, Description = "更新指定使用者的直接 API 權限", IsMenu = false)]
    [RequiresPermission("SYSTEM.PERMISSION.ASSIGN_USER", "使用者直接權限")]
    [EndpointSummary("指派使用者權限")]
    [EndpointDescription("更新指定使用者的直接 API 權限。")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AssignUserPermissions(
        [FromRoute] Guid userId,
        [FromQuery] Guid? tenantLabId,
        [FromBody] AssignUserPermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await _permissionService.AssignUserPermissionsAsync(
                userId.ToString(),
                tenantLabId,
                request,
                cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "權限指派失敗",
                    Detail = result.Message,
                    Instance = HttpContext.Request.Path
                });
            }

            return Ok(new { message = result.Message });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("指派使用者權限作業已取消。UserId: {UserId}", userId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新使用者直接權限時發生未預期異常。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "更新使用者直接權限時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }


    /// <summary>
    /// 取得具備指定權限代碼 (PermissionKey) 的使用者清單
    /// </summary>
    [HttpGet("users/by-permission")]
    [Function("GetUsersByPermissionKey", "取得具備特定權限的使用者清單", Icon = "fa-solid fa-users", Order = 7, Description = "取得擁有指定 PermissionKey 的所有使用者清單", IsMenu = false)]
    [RequiresPermission("SYSTEM.PERMISSION.Audit", "特定權限的使用者清單")]
    [EndpointSummary("取得具備特定權限的使用者清單")]
    [EndpointDescription("透過 PermissionKey (例如: SYSTEM.AUTH.LOGINDFMS) 查詢擁有該權限的所有使用者資訊。")]
    [ProducesResponseType(typeof(PermissionUsersMasterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUsersByPermissionKey(
        [FromQuery] string permissionKey,
        [FromQuery] Guid? tenantLabId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation.General",
                Detail = "發生一項或多項驗證錯誤。",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["errors"] = new[]
                    {
                        new { field = "permissionKey", message = "The permissionKey field is required." }
                    }
                }
            });
        }

        try
        {
            // 在 Controller 內部組裝 DTO 呼叫 Service 或 MediatR
            var result = await _permissionService.GetUsersByPermissionKeyAsync(
                permissionKey,
                tenantLabId,
                cancellationToken).ConfigureAwait(false);

            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("查詢權限 [{PermissionKey}] 使用者清單作業已取消。", permissionKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢具備權限 [{PermissionKey}] 的使用者清單時發生異常。", permissionKey);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "查詢具備特定權限的使用者清單時發生異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }
}