using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.Options;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SGSFramework.Identity.Services
{
    public class AdminSeederService : IAdminSeederService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly SeedAdminOptions _options;
        private readonly ILogger<AdminSeederService> _logger;

        public AdminSeederService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            IOptions<SeedAdminOptions> options,
            ILogger<AdminSeederService> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SeedAdminAsync(CancellationToken cancellationToken = default)
        {
            if (!_options.EnableAutoSeed)
            {
                _logger.LogInformation("[SeedAdmin] 設定已停用自動初始化預設管理員。");
                return;
            }

            if (string.IsNullOrWhiteSpace(_options.Password))
            {
                _logger.LogError("[SeedAdmin] 未設定預設管理員密碼，停止初始化作業！");
                return;
            }

            try
            {
                // 1. 確保 SuperAdmin 角色存在
                string roleName = _options.RoleName;
                var roleExists = await _roleManager.RoleExistsAsync(roleName);
                if (!roleExists)
                {
                    var roleResult = await _roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                    if (!roleResult.Succeeded)
                    {
                        _logger.LogError("[SeedAdmin] 建立 SuperAdmin 角色失敗: {Errors}",
                            string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                        return;
                    }
                    _logger.LogInformation("[SeedAdmin] 成功建立核心角色: {RoleName}", roleName);
                }

                // 2. 檢查預設 Admin 使用者是否存在 (優先以 Username 查，再以 Email 查)
                var adminUser = await _userManager.FindByNameAsync(_options.Username)
                                ?? await _userManager.FindByEmailAsync(_options.Email);

                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = _options.Username,
                        Email = _options.Email,
                        EmailConfirmed = true,
                        LockoutEnabled = false // 避免預設 SuperAdmin 被暴力破解鎖定導致無人能救
                    };

                    var createResult = await _userManager.CreateAsync(adminUser, _options.Password);
                    if (!createResult.Succeeded)
                    {
                        _logger.LogError("[SeedAdmin] 建立預設管理員失敗: {Errors}",
                            string.Join(", ", createResult.Errors.Select(e => e.Description)));
                        return;
                    }

                    _logger.LogInformation("[SeedAdmin] 成功建立預設管理員帳號: {Username}", adminUser.UserName);
                }

                // 3. 確保 Admin 使用者綁定 SuperAdmin 角色
                var isInRole = await _userManager.IsInRoleAsync(adminUser, roleName);
                if (!isInRole)
                {
                    var addRoleResult = await _userManager.AddToRoleAsync(adminUser, roleName);
                    if (!addRoleResult.Succeeded)
                    {
                        _logger.LogError("[SeedAdmin] 將預設管理員加入角色 {RoleName} 失敗", roleName);
                    }
                    else
                    {
                        _logger.LogInformation("[SeedAdmin] 成功將預設管理員加入角色 {RoleName}", roleName);
                    }
                }

                // 4. 賦予萬能超級權限特徵 (例如 Claims 或特定身份標記)
                var claims = await _userManager.GetClaimsAsync(adminUser);
                if (!claims.Any(c => c.Type == "IsSuperAdmin" && c.Value == "true"))
                {
                    await _userManager.AddClaimAsync(adminUser, new Claim("IsSuperAdmin", "true"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SeedAdmin] 執行預設管理員初始化時發生未預期例外。");
            }
        }
    }
}
