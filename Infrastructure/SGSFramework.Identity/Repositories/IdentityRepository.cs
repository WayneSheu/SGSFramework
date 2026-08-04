using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DbContexts;
using SGSFramework.Identity.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Repositories
{
    /// <summary>
    /// Identity 倉儲服務實作
    /// </summary>
    public class IdentityRepository : IIdentityRepository
    {
        private readonly ExtendedIdentityDbContext _context;
        private readonly ILogger<IdentityRepository> _logger;

        public IdentityRepository(ExtendedIdentityDbContext context, ILogger<IdentityRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(email);

            try
            {
                return await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行 {MethodName} 時發生未預期錯誤，Email: {Email}", nameof(GetByEmailAsync), email);
                throw;
            }
        }

        public async Task<bool> UpdateLastLoginAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("使用者識別碼不可為空 Guid", nameof(userId));
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                if (user == null)
                {
                    _logger.LogWarning("找不到指定的使用者，無法更新登入時間。UserId: {UserId}", userId);
                    return false;
                }

                user.LastLoginAt = DateTimeOffset.UtcNow;
                var affectedRows = await _context.SaveChangesAsync(cancellationToken);

                return affectedRows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行 {MethodName} 時發生錯誤，UserId: {UserId}", nameof(UpdateLastLoginAsync), userId);
                return false;
            }
        }
    }
}
