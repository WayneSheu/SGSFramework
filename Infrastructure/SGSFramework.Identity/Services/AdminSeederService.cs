// ==========================================
// 檔案路徑: src/SGSFramework.Identity/Services/AdminSeederService.cs
// 架構層級: Identity / Services
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Services
{
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using SGSFramework.AuthTokenBucket.Abstractions;
    using SGSFramework.Core.Abstractions.Entities.Identities;
    using SGSFramework.Identity.Abstractions;
    using SGSFramework.Identity.Options;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading;
    using System.Threading.Tasks;

    public class AdminSeederService : IAdminSeederService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ISystemRolePermissionSeedService _permissionSeederService;
        private readonly SeedAdminOptions _options;
        private readonly ILogger<AdminSeederService> _logger;

        public AdminSeederService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ISystemRolePermissionSeedService permissionSeederService,
            IOptions<SeedAdminOptions> options,
            ILogger<AdminSeederService> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _permissionSeederService = permissionSeederService ?? throw new ArgumentNullException(nameof(permissionSeederService));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SeedAdminAsync(CancellationToken cancellationToken = default)
        {
            if (!_options.EnableAutoSeed)
            {
                _logger.LogInformation("[SeedAdmin] 設定已停用自動初始化預設管理員與角色。");
                return;
            }

            try
            {
                // ==========================================
                // 1. 初始化系統預設角色範本 (包含 Code、Description 與職責說明)
                // ==========================================
                var defaultRoles = new Dictionary<string, (string Code, string Description)>
                {
                    { "SuperAdmin", ("ROLE_SUPER_ADMIN", "系統技術最高管理員：僅負責系統部署、資料庫遷移與基礎設施維護，嚴禁介入業務實驗室角色指派。") },
                    { "LabManager", ("ROLE_LAB_MANAGER", "實驗室主管：負責管理該實驗室成員、指派角色與配置模組權限 (具備該實驗室所有管理與操作 Bitmask)。") },
                    { "LabOperator",("ROLE_LAB_OPERATOR", "實驗室操作員：負責日常數據輸入、檢測與實驗資料維護 (具備業務模組的讀寫 Bitmask)。") },
                    { "LabAuditor", ("ROLE_LAB_AUDITOR", "實驗室稽核員：負責檢視實驗室內所有數據與異動軌跡 (具備該實驗室所有模組的唯讀 Bitmask)。") }
                };

                foreach (var rolePair in defaultRoles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var roleName = rolePair.Key;
                    var roleCode = rolePair.Value.Code;
                    var description = rolePair.Value.Description;

                    var role = await _roleManager.FindByNameAsync(roleName).ConfigureAwait(false);
                    if (role == null)
                    {
                        role = new ApplicationRole
                        {
                            Name = roleName,
                            NormalizedName = roleName.ToUpperInvariant(),
                            Code = roleCode,
                            Description = description,
                            IsSystemRole = true
                        };

                        var createRoleResult = await _roleManager.CreateAsync(role).ConfigureAwait(false);
                        if (!createRoleResult.Succeeded)
                        {
                            _logger.LogError("[SeedAdmin] 建立角色失敗 [{RoleName}]: {Errors}",
                                roleName, string.Join(", ", createRoleResult.Errors.Select(e => e.Description)));
                            continue;
                        }
                        _logger.LogInformation("[SeedAdmin] 成功建立角色: {RoleName} (代碼: {Code})，說明: {Description}", roleName, roleCode, description);
                    }
                    else
                    {
                        bool needsUpdate = false;
                        if (role.Code != roleCode)
                        {
                            role.Code = roleCode;
                            needsUpdate = true;
                        }
                        if (role.Description != description)
                        {
                            role.Description = description;
                            needsUpdate = true;
                        }

                        if (needsUpdate)
                        {
                            await _roleManager.UpdateAsync(role).ConfigureAwait(false);
                            _logger.LogInformation("[SeedAdmin] 成功更新角色資訊: {RoleName} (代碼: {Code})", roleName, roleCode);
                        }
                    }
                }

                // ==========================================
                // 2. 建立最高技術管理員帳號 (IsSystemAdmin = true)
                // ==========================================
                if (string.IsNullOrWhiteSpace(_options.Password))
                {
                    _logger.LogError("[SeedAdmin] 未設定預設管理員密碼，停止帳號初始化作業！");
                    return;
                }

                var adminUser = await _userManager.FindByNameAsync(_options.Username).ConfigureAwait(false)
                                ?? await _userManager.FindByEmailAsync(_options.Email).ConfigureAwait(false);

                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        IsSystemAdmin = true, // 標註為系統技術管理員，強制阻斷業務指派權限
                        UserName = _options.Username,
                        Email = _options.Email,
                        EmailConfirmed = true,
                        LockoutEnabled = false
                    };

                    var createResult = await _userManager.CreateAsync(adminUser, _options.Password).ConfigureAwait(false);
                    if (!createResult.Succeeded)
                    {
                        _logger.LogError("[SeedAdmin] 建立預設 SuperAdmin 帳號失敗: {Errors}",
                            string.Join(", ", createResult.Errors.Select(e => e.Description)));
                        return;
                    }

                    _logger.LogInformation("[SeedAdmin] 成功建立預設技術管理員帳號: {Username}", adminUser.UserName);
                }

                // ==========================================
                // 3. 綁定 SuperAdmin 角色與全域 Claim
                // ==========================================
                if (!await _userManager.IsInRoleAsync(adminUser, "SuperAdmin").ConfigureAwait(false))
                {
                    await _userManager.AddToRoleAsync(adminUser, "SuperAdmin").ConfigureAwait(false);
                }

                var claims = await _userManager.GetClaimsAsync(adminUser).ConfigureAwait(false);
                if (!claims.Any(c => c.Type == "IsSuperAdmin" && c.Value == "true"))
                {
                    await _userManager.AddClaimAsync(adminUser, new Claim("IsSuperAdmin", "true")).ConfigureAwait(false);
                }

                // ==========================================
                // 4. 動態調用 PermissionSeederService 派發角色權限
                // ==========================================
                await _permissionSeederService.SeedPermissionsFromExcelAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SeedAdmin] 執行系統初始化作業時發生未預期例外。");
                throw;
            }
        }
    }
}