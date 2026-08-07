using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Base;
using SGSFramework.Identity.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Repositories
{
    /// <summary>
    /// 泛型化 Identity 倉儲服務實作
    /// </summary>
    public class GenericIdentityRepository<TContext, TUser, TRole, TKey> : IGenericIdentityRepository<TUser, TKey>
        where TContext : DbContext
        where TUser : IdentityUser<TKey>, IBaseUser
        where TRole : IdentityRole<TKey>
        where TKey : IEquatable<TKey>
    {
        private readonly TContext _context;
        private readonly ILogger<GenericIdentityRepository<TContext, TUser, TRole, TKey>> _logger;

        public GenericIdentityRepository(TContext context, ILogger<GenericIdentityRepository<TContext, TUser, TRole, TKey>> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 根據電子郵件查詢使用者
        /// </summary>
        /// <param name="email"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<TUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(email);

            try
            {
                return await _context.Set<TUser>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行 {MethodName} 時發生未預期錯誤，Email: {Email}", nameof(GetByEmailAsync), email);
                throw;
            }
        }

        /// <summary>
        /// 更新使用者的最後登入時間
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<bool> UpdateLastLoginAsync(TKey userId, CancellationToken cancellationToken = default)
        {
            if (userId == null || userId.Equals(default))
            {
                throw new ArgumentException("使用者識別碼不可為預設值", nameof(userId));
            }

            try
            {
                var user = await _context.Set<TUser>().FirstOrDefaultAsync(u => u.Id.Equals(userId), cancellationToken);
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
