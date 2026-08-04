using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.AuthTokenBucket.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Repositories
{
    public sealed class UserRefreshTokenRepository<TDbContext> : IUserRefreshTokenRepository
     where TDbContext : DbContext, ITokenDbContext
    {
        private readonly TDbContext _context;

        public UserRefreshTokenRepository(TDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<int> GetActiveOnlineUserCountAsync(int activityWindowMinutes)
        {
            try
            {
                var utcNow = DateTime.UtcNow;
                var thresholdTime = utcNow.AddMinutes(-activityWindowMinutes);

                // 支援泛型上下文中，利用強型別 LINQ 進行極速效能評估
                int onlineCount = await _context.UserRefreshTokens
                    .Where(token => token.ExpiresAt > utcNow && token.LastActiveAt >= thresholdTime)
                    .Select(token => token.UserId)
                    .Distinct()
                    .CountAsync();

                return onlineCount;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("執行泛型在線人數統計查詢時發生資料庫核心異常。", ex);
            }
        }
    }
}
