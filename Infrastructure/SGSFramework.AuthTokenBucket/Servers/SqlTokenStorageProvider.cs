using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.HttpAuditProviders;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SGSFramework.AuthTokenBucket.Servers
{
    /// <summary>
    /// SQL-based implementation of the token storage provider. This class is responsible for managing user refresh tokens in a SQL database. It provides methods to add, update, and retrieve tokens based on user ID and device ID. The class uses Entity Framework Core for database operations.
    /// 基於 SQL 的令牌儲存提供者的實作。
    /// 此類別負責在 SQL 資料庫中管理使用者刷新令牌。它提供了基於使用者 ID 和裝置 ID 新增、更新和檢索令牌的方法。
    /// 此類別使用 Entity Framework Core 進行資料庫操作。
    /// </summary>
    /// <typeparam name="TDbContext"></typeparam>
    public sealed class SqlTokenStorageProvider<TDbContext> : ITokenStorageProvider
        where TDbContext : DbContext, ITokenDbContext
    {
        private readonly IAuditProvider _auditProvider;        
        private readonly ILogger<SqlTokenStorageProvider<TDbContext>> _logger;
        private readonly ISecurityLogger _securityLogger;
        private readonly TDbContext _context;

        public SqlTokenStorageProvider(TDbContext context,IAuditProvider auditProvider, ILogger<SqlTokenStorageProvider<TDbContext>> logger,ISecurityLogger securityLogger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _auditProvider=auditProvider?? throw new ArgumentNullException(nameof(auditProvider));
            _securityLogger = securityLogger?? throw new ArgumentNullException(nameof(securityLogger));  
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves the active session for a given user ID and device ID. This method returns the first non-dead, expired token that matches the criteria.
        /// 取得給定使用者 ID 和裝置 ID 的活動會話。
        /// 此方法傳回第一個符合條件的非失效過期的UserRefreshToken 物件。
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public async Task<UserRefreshToken?> GetActiveSessionAsync(string userId, string deviceId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(deviceId))
                {
                    return null;
                }

                return await _context.UserRefreshTokens
                    .AsNoTracking()
                    .Where(t => t.UserId == userId
                             && t.DeviceId == deviceId
                             && !t.IsDead 
                             && t.ExpiresAt > DateTime.UtcNow)
                    .FirstOrDefaultAsync();
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 檢查是否已經存在相同 UserId 和 DeviceId 的 UserRefreshToken 物件。如果存在，則更新其屬性；否則，新增一筆新記錄。
        /// </summary>
        /// <param name="tokenEntity"></param>
        /// <returns></returns>
        public async Task SaveInitialSessionAsync(UserRefreshToken tokenEntity)
        {
            ArgumentNullException.ThrowIfNull(tokenEntity);

            //實現復原策略（Resiliency Strategy）與自動重試機制（Automatic Retry）的核心 API。
            //當配置了執行策略（例如 SqlServerRetryingExecutionStrategy），EF Core 在執行內建的 SaveChanges() 時會自動重試。
            //若未配置執行策略，EF Core 不會自動重試。因此，在需要高可用性（HA）或容錯能力的應用程式中，建議使用 SqlServerRetryingExecutionStrategy。
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                //使用 BeginTransactionAsync 方法開始一個新的資料庫交易。
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    //檢查是否已經存在相同 UserId 和 DeviceId 的 UserRefreshToken 物件。如果存在，則更新其屬性；否則，新增一筆新記錄。
                    var existingSession = await _context.UserRefreshTokens
                        .FirstOrDefaultAsync(x => x.UserId == tokenEntity.UserId && x.DeviceId == tokenEntity.DeviceId);
                    //如果已經存在相同 UserId 和 DeviceId 的 UserRefreshToken 物件，則更新其屬性；否則，新增一筆新記錄。
                    if (existingSession != null)
                    {
                        existingSession.RefreshTokenHash = tokenEntity.RefreshTokenHash;
                        existingSession.ExpiresAt = tokenEntity.ExpiresAt;
                        existingSession.CreatedAt = DateTime.UtcNow;
                        existingSession.RotatedAt = null;
                        existingSession.IsDead = false;

                        _context.UserRefreshTokens.Update(existingSession);
                    }
                    else 
                    {
                        //
                        await _context.UserRefreshTokens.AddAsync(tokenEntity);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        /// <summary>
        /// 驗證並輪換令牌。此方法會檢查舊雜湊值是否有效，然後使用新值更新令牌。
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="deviceId"></param>
        /// <param name="oldHash">old RefreshTokenHash</param>
        /// <param name="newHash">new RefreshTokenHash</param>
        /// <param name="expiresAt">到期日</param>
        /// <param name="gracePeriodSeconds"></param>
        /// <returns></returns>
        public async Task<RotationResult?> ValidateAndRotateTokenAsync(
            string userId,
            string deviceId,
            string oldHash,
            string newHash,
            DateTime expiresAt,
            int gracePeriodSeconds)
        {
            
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
              
                    // 
                    var tokenInDb = await _context.UserRefreshTokens
                        .FirstOrDefaultAsync(t => t.UserId == userId && t.DeviceId == deviceId && !t.IsDead);
                    var currentClientIp = _auditProvider.RemoteIp??string.Empty;
                    if (tokenInDb == null)
                    {
                        // If the token is not found, return null.
                        await transaction.RollbackAsync();
                        return null;
                    }
                    var oldJwtId = tokenInDb.Id;
                    //
                    if (tokenInDb.RefreshTokenHash == oldHash)
                    {
                        tokenInDb.RefreshTokenHash = newHash;
                        tokenInDb.RotatedAt = DateTime.UtcNow;
                        tokenInDb.ExpiresAt = expiresAt;

                        var newJwtId = tokenInDb.Id;

                        _context.UserRefreshTokens.Update(tokenInDb);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        //資安審計點記錄
                        _securityLogger.LogSecurity(
                            eventCode: "SEC-200-RotationStatus.Success",
                            eventCategory: "ValidateAndRotateToken",
                            userId: userId,
                            clientIp: currentClientIp, // 帶入上下文 IP
                            messageTemplate: "權杖煥發成功。用戶: {UserId},來源IP: {ClientIp},舊權杖Id{oldJId},新權杖ID{}", currentClientIp,oldJwtId,newJwtId
                        );
                        return new RotationResult
                        {
                            Status = RotationStatus.Success,
                            ExpiresAt = expiresAt
                        };
                    }
                    // 
                    if (tokenInDb.RotatedAt.HasValue &&
                        (DateTime.UtcNow - tokenInDb.RotatedAt.Value).TotalSeconds <= gracePeriodSeconds)
                    {
                        await transaction.RollbackAsync();
                        return new RotationResult
                        {
                            Status = RotationStatus.GracePeriodMatch,
                            ExpiresAt = tokenInDb.ExpiresAt
                        };
                    }

                    tokenInDb.IsDead = true;
                    _context.UserRefreshTokens.Update(tokenInDb);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    //資安審計點記錄
                    _securityLogger.LogSecurity(
                        eventCode: "SEC-403-Forbidden",
                        eventCategory: "ValidateAndRotateToken",
                        userId: userId,
                        clientIp: currentClientIp, // 帶入上下文 IP
                        messageTemplate: "偵測到重播攻擊。用戶: {UserId}, 來源IP: {ClientIp}", currentClientIp
                    );

                    return new RotationResult
                    {
                        Status = RotationStatus.ReplayAttackDetected,
                        ExpiresAt = tokenInDb.ExpiresAt
                    };
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        /// <summary>
        /// 對用戶設備數量設定上限。如果使用者會話數超過允許值，系統將刪除最早的會話。
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="maxDeviceCount"></param>
        /// <returns></returns>
        public async Task EnforceMaxDeviceLimitAsync(string userId, int maxDeviceCount)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);
            if (maxDeviceCount <= 0) return;

            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var activeSessions = await _context.UserRefreshTokens
                        .Where(x => x.UserId == userId && !x.IsDead)
                        .OrderBy(x => x.LastActiveAt)
                        .ToListAsync();

                    int currentCount = activeSessions.Count;
                    if (currentCount >= maxDeviceCount)
                    {
                        int overflowCount = currentCount - maxDeviceCount + 1;
                        var sessionsToRevoke = activeSessions.Take(overflowCount);

                        foreach (var session in sessionsToRevoke)
                        {
                            session.IsDead = true;
                            session.RefreshTokenHash = string.Empty;
                            session.ReusedTokenCache = null;
                            session.RiskReason = $"系統自動執行 LIFO 被動擠出策略：超越最大裝置限制 ({maxDeviceCount})。";
                        }

                        _context.UserRefreshTokens.UpdateRange(sessionsToRevoke);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        /// <summary>
        /// 凍結並撤銷該使用者的所有會話。
        /// Freeze and revoke all sessions for a user due to risk.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="riskReason"></param>
        /// <returns></returns>
        public async Task<bool> FreezeAndRevokeAllSessionsAsync(string userId, string riskReason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                int affectedRows = await _context.UserRefreshTokens
                    .Where(x => x.UserId == userId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.IsFrozen,true)
                        .SetProperty(x => x.IsDead, true)
                        .SetProperty(x => x.RiskReason,riskReason)
                        .SetProperty(x => x.RefreshTokenHash, string.Empty));

                return affectedRows > 0;
            });
        }

        /// <summary>
        /// 修復並清除使用者的所有凍結會話。
        /// Remediate and clear all frozen sessions for a user. This will delete all sessions that are marked as 'dead' but not yet removed from the database.
        /// 這將刪除所有標記為「已失效」但尚未從資料庫中移除的會話。
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<bool> RemediateAndClearFrozenSessionsAsync(string userId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                int affectedRows = await _context.UserRefreshTokens
                    .Where(x => x.UserId == userId && x.IsDead)
                    .ExecuteDeleteAsync();

                return affectedRows > 0;
            });
        }

        /// <summary>
        /// 用戶主動登出特定裝置
        /// 根據用戶ID、裝置 ID 刪除特定使用者會話。這將把該會話標記為「已失效」並清除其關聯資料。
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public async Task<bool> RemoveSessionAsync(string userId, string deviceId)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(deviceId)) return false;

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var session = await _context.UserRefreshTokens
                        .FirstOrDefaultAsync(x => x.UserId == userId && x.DeviceId == deviceId && !x.IsDead);

                    if (session == null)
                    {
                        await transaction.RollbackAsync();
                        return false;
                    }

                    session.IsDead = true;
                    session.RefreshTokenHash = string.Empty;
                    session.ReusedTokenCache = null;
                    session.RiskReason = "用戶主動登出特定裝置。";

                    _context.UserRefreshTokens.Update(session);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        /// <summary>
        /// 用戶主動登出所有裝置
        /// 根據用戶ID，把該使用者所有會話標記為「已失效」並清除其關聯資料。
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<bool> RemoveAllSessionsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return false;

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var activeSessions = await _context.UserRefreshTokens
                        .Where(x => x.UserId == userId && !x.IsDead)
                        .ToListAsync();

                    if (!activeSessions.Any())
                    {
                        await transaction.RollbackAsync();
                        return true;
                    }

                    foreach (var session in activeSessions)
                    {
                        session.IsDead = true;
                        session.RefreshTokenHash = string.Empty;
                        session.ReusedTokenCache = null;
                        session.RiskReason = "用戶主動登出全裝置 (Global Logout)。";
                    }

                    _context.UserRefreshTokens.UpdateRange(activeSessions);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        /// <summary>
        /// 管理者或用戶遠端強制撤銷特定裝置會話
        /// 撤銷使用者在特定裝置上的會話。這將把該會話標記為“已失效”，並清除其關聯資料。
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public async Task RevokeDeviceSessionAsync(string userId, string deviceId)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(deviceId)) return;

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var session = await _context.UserRefreshTokens
                        .FirstOrDefaultAsync(x => x.UserId == userId && x.DeviceId == deviceId && !x.IsDead);

                    if (session != null)
                    {
                        session.IsDead = true;
                        session.RefreshTokenHash = string.Empty;
                        session.RiskReason = "管理者或用戶遠端強制撤銷特定裝置會話。";
                        _context.UserRefreshTokens.Update(session);
                        await _context.SaveChangesAsync();
                    }
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        /// <summary>
        /// 用戶執行身份安全防禦：除當前裝置之外強制強制撤銷。
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="currentDeviceId"></param>
        /// <returns></returns>
        public async Task RevokeAllOtherSessionsAsync(string userId, string currentDeviceId)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(currentDeviceId)) return;

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var otherSessions = await _context.UserRefreshTokens
                        .Where(x => x.UserId == userId && x.DeviceId != currentDeviceId && !x.IsDead)
                        .ToListAsync();

                    if (otherSessions.Any())
                    {
                        foreach (var session in otherSessions)
                        {
                            session.IsDead = true;
                            session.RefreshTokenHash = string.Empty;
                            session.RiskReason = $"用戶執行身份安全防禦：除當前裝置 [{currentDeviceId}] 之外強制撤銷。";
                        }
                        _context.UserRefreshTokens.UpdateRange(otherSessions);
                        await _context.SaveChangesAsync();
                    }
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        /// <summary>
        /// 關鍵稽核點：
        /// SEC-202-SESSION-REVOCATION：用戶在變更密碼或防禦宣告時，強制撤銷非當前裝置以外的所有其他工作階段連線。
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="currentDeviceId"></param>
        /// <param name="clientIp"></param>
        /// <returns></returns>
        public async Task RevokeOtherSessionsAsync(string userId, string currentDeviceId, string clientIp)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(currentDeviceId)) return;

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var otherSessions = await _context.UserRefreshTokens
                        .Where(x => x.UserId == userId && x.DeviceId != currentDeviceId && !x.IsDead)
                        .ToListAsync();

                    if (otherSessions.Any())
                    {
                        foreach (var session in otherSessions)
                        {
                            session.IsDead = true;
                            session.RefreshTokenHash = string.Empty;
                            session.RiskReason = $"用戶執行身份安全防禦：除當前裝置 [{currentDeviceId}] 之外強制撤銷。";
                        }
                        _context.UserRefreshTokens.UpdateRange(otherSessions);
                        await _context.SaveChangesAsync();

                        // 💡 修正：記錄多裝置連線強制撤銷的稽核軌跡
                        using (Serilog.Context.LogContext.PushProperty("LogType", "Security"))
                        using (Serilog.Context.LogContext.PushProperty("UserId", userId))
                        using (Serilog.Context.LogContext.PushProperty("EventCategory", "Auth.SessionPurge"))
                        using (Serilog.Context.LogContext.PushProperty("ClientIp", clientIp))
                        {
                            _logger.LogInformation("[Security-Event:SEC-202-SESSION-REVOCATION] 用戶執行身份防禦，已強制撤銷裝置 [{CurrentDeviceId}] 以外的 {Count} 個作用中工作階段。來源IP: {ClientIp}", currentDeviceId, otherSessions.Count, clientIp);
                        }
                    }
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
    }
}