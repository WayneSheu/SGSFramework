// ==========================================
// 檔案路徑: src/SGSFramework.Identity/Services/AdminSeederService.cs
// 架構層級: Identity / Services
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Services
{
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
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
        private readonly SeedAdminOptions _adminOptions;
        private readonly SystemRolePermissionSeedOptions _seedOptions;
        private readonly ILogger<AdminSeederService> _logger;

        public AdminSeederService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ISystemRolePermissionSeedService permissionSeederService,
            IOptions<SeedAdminOptions> adminOptions,
            IOptions<SystemRolePermissionSeedOptions> seedOptions,
            ILogger<AdminSeederService> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _permissionSeederService = permissionSeederService ?? throw new ArgumentNullException(nameof(permissionSeederService));
            _adminOptions = adminOptions?.Value ?? throw new ArgumentNullException(nameof(adminOptions));
            _seedOptions = seedOptions?.Value ?? throw new ArgumentNullException(nameof(seedOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SeedAdminAsync(CancellationToken cancellationToken = default)
        {
            if (!_adminOptions.EnableAutoSeed)
            {
                _logger.LogInformation("[SeedAdmin] 設定已停用自動初始化預設管理員與角色。");
                return;
            }

            try
            {
                _logger.LogInformation("[SeedAdmin] 開始初始化系統預設角色，共計偵讀到 {Count} 筆規則配置。", _seedOptions.Roles?.Count ?? 0);

                if (_seedOptions.Roles == null || _seedOptions.Roles.Count == 0)
                {
                    _logger.LogWarning("[SeedAdmin] 警告：_seedOptions.Roles 為空，請檢查 appsettings 中的 SystemRolePermissionSeed 結構。");
                }

                // ==========================================
                // 1. 透過 Options 配置動態初始化系統預設角色範本
                // ==========================================
                foreach (var rule in _seedOptions.Roles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var roleName = rule.RoleName;
                    var roleCode = rule.RoleCode;
                    var description = rule.Description;

                    if (string.IsNullOrWhiteSpace(roleName)) continue;

                    var role = await _roleManager.FindByNameAsync(roleName).ConfigureAwait(false)
                                ?? await _roleManager.Roles.FirstOrDefaultAsync(r => r.Code == roleCode, cancellationToken);

                    string normalizedName = _roleManager.NormalizeKey(roleName);

                    if (role == null)
                    {
                        role = new ApplicationRole
                        {
                            Name = roleName,
                            NormalizedName = normalizedName,
                            Code = roleCode,
                            Description = description,
                            IsSystemRole = true
                        };

                        var createRoleResult = await _roleManager.CreateAsync(role).ConfigureAwait(false);
                        if (!createRoleResult.Succeeded)
                        {
                            string errors = string.Join("; ", createRoleResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                            _logger.LogError("[SeedAdmin] 建立角色失敗 [{RoleName} / Code: {RoleCode}]: {Errors}",
                                roleName, roleCode, errors);
                            continue;
                        }
                        _logger.LogInformation("[SeedAdmin] 成功建立角色: {RoleName} (代碼: {Code})", roleName, roleCode);
                    }
                    else
                    {
                        bool needsUpdate = false;
                        if (role.Name != roleName) { role.Name = roleName; needsUpdate = true; }
                        if (role.NormalizedName != normalizedName) { role.NormalizedName = normalizedName; needsUpdate = true; }
                        if (role.Code != roleCode) { role.Code = roleCode; needsUpdate = true; }
                        if (role.Description != description) { role.Description = description; needsUpdate = true; }
                        if (!role.IsSystemRole) { role.IsSystemRole = true; needsUpdate = true; }

                        if (needsUpdate)
                        {
                            var updateResult = await _roleManager.UpdateAsync(role).ConfigureAwait(false);
                            if (updateResult.Succeeded)
                            {
                                _logger.LogInformation("[SeedAdmin] 成功更新角色資訊: {RoleName} (代碼: {Code})", roleName, roleCode);
                            }
                            else
                            {
                                string errors = string.Join("; ", updateResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                                _logger.LogError("[SeedAdmin] 更新角色失敗 [{RoleName}]: {Errors}", roleName, errors);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("[SeedAdmin] 角色已存在且無需更新: {RoleName} (代碼: {Code})", roleName, roleCode);
                        }
                    }
                }

                // ==========================================
                // 2. 建立最高技術管理員帳號 (IsSystemAdmin = true)
                // ==========================================
                if (string.IsNullOrWhiteSpace(_adminOptions.Password))
                {
                    _logger.LogError("[SeedAdmin] 未設定預設管理員密碼，停止帳號初始化作業！");
                    return;
                }

                var adminUser = await _userManager.FindByNameAsync(_adminOptions.Username).ConfigureAwait(false)
                                ?? await _userManager.FindByEmailAsync(_adminOptions.Email).ConfigureAwait(false);

                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        IsSystemAdmin = true,
                        UserName = _adminOptions.Username,
                        Email = _adminOptions.Email,
                        EmailConfirmed = true,
                        LockoutEnabled = false
                    };

                    var createResult = await _userManager.CreateAsync(adminUser, _adminOptions.Password).ConfigureAwait(false);
                    if (!createResult.Succeeded)
                    {
                        _logger.LogError("[SeedAdmin] 建立預設管理員帳號失敗: {Errors}",
                            string.Join(", ", createResult.Errors.Select(e => e.Description)));
                        return;
                    }

                    _logger.LogInformation("[SeedAdmin] 成功建立預設技術管理員帳號: {Username}", adminUser.UserName);
                }

                // ==========================================
                // 3. 動態取得管理員角色名稱並安全綁定角色與全域 Claim
                // ==========================================
                var adminRoleRule = _seedOptions.Roles.FirstOrDefault(r => r.AssignAll)
                                    ?? _seedOptions.Roles.FirstOrDefault();
                string targetRoleName = adminRoleRule?.RoleName ?? "系統管理員";

                if (!await _roleManager.RoleExistsAsync(targetRoleName).ConfigureAwait(false))
                {
                    var fallbackRole = new ApplicationRole
                    {
                        Name = targetRoleName,
                        NormalizedName = _roleManager.NormalizeKey(targetRoleName),
                        Code = adminRoleRule?.RoleCode ?? "SA",
                        Description = adminRoleRule?.Description ?? "系統自動補建之預設管理員角色",
                        IsSystemRole = true
                    };
                    await _roleManager.CreateAsync(fallbackRole).ConfigureAwait(false);
                }

                if (!await _userManager.IsInRoleAsync(adminUser, targetRoleName).ConfigureAwait(false))
                {
                    var addRoleResult = await _userManager.AddToRoleAsync(adminUser, targetRoleName).ConfigureAwait(false);
                    if (addRoleResult.Succeeded)
                    {
                        _logger.LogInformation("[SeedAdmin] 成功將使用者 {Username} 綁定至角色: {RoleName}", adminUser.UserName, targetRoleName);
                    }
                    else
                    {
                        _logger.LogError("[SeedAdmin] 將使用者綁定至角色 [{RoleName}] 失敗: {Errors}",
                            targetRoleName, string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
                    }
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