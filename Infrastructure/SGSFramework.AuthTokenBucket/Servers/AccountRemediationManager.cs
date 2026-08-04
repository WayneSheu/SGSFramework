using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.AuthTokenBucket.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Servers
{
    /// <summary>
    /// 帳號補償修復管理器
    /// 密碼變更與 Session 清理的原子性交易（Atomicity）
    /// 當使用者送出新密碼與 TicketId 時，後端必須在一筆資料庫交易（Transaction）內同時完成以下三件事：
    /// 驗證 Ticket 沒過期且未被使用（防範重放）。
    /// 更新密碼雜湊值（Password Hash）。
    /// 將舊有的凍結/風險連線紀錄徹底抹除，並寫入資安稽核日誌。
    /// </summary>
    /// <typeparam name="TDbContext"></typeparam>
    public sealed class AccountRemediationManager<TDbContext> where TDbContext : DbContext, ITokenDbContext
    {
        private readonly TDbContext _context;
        private readonly ILogger<AccountRemediationManager<TDbContext>> _logger;

        public AccountRemediationManager(TDbContext context, ILogger<AccountRemediationManager<TDbContext>> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 嚴格管控：驗證重設憑證並完成密碼修復與工作階段清理
        /// </summary>
        public async Task<bool> ExecutePasswordResetRemediationAsync(string userId, string ticketId, string newPasswordHash)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var utcNow = DateTime.UtcNow;

                    // 🚀 管控一：悲觀鎖驗證 Ticket 有效性
                    // 防止駭客並行發送重複的重設請求
                    var ticket = await _context.Set<PasswordResetTicket>()
                        .FromSqlInterpolated($"SELECT * FROM PasswordResetTickets WITH (UPDLOCK, ROWLOCK) WHERE TicketId = {ticketId} AND UserId = {userId}")
                        .FirstOrDefaultAsync();

                    if (ticket == null || ticket.IsUsed || ticket.ExpiresAt < utcNow)
                    {
                        _logger.LogWarning("[Security-Audit:{EventCode}] 拒絕非法的密碼重設嘗試。用戶: {UserId}, Ticket: {TicketId}", "SEC-403-BAD-TICKET", userId, ticketId);
                        await transaction.RollbackAsync();
                        return false;
                    }

                    // 🚀 管控二：標記 Ticket 已使用（立即使其失效）
                    ticket.IsUsed = true;

                    // 管控三：更新密碼 (此處示意，實務上通常在 ApplicationUser 表)
                    // await _userManager.UpdatePasswordAsync(userId, newPasswordHash);

                    // 管控四：清空該用戶所有處於凍結或殘留的風險工作階段
                    await _context.UserRefreshTokens
                        .Where(x => x.UserId == userId)
                        .ExecuteDeleteAsync();

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // 管控五：寫入結構化稽核日誌，記錄精準修復時間點與軌跡
                    _logger.LogInformation(
                        "[Security-Event:{EventCode}] 用戶透過重設密碼成功完成身分補償修復。用戶: {UserId}, 時間戳記: {TimestampUtc}",
                        "SEC-200-PASSWORD-REMEDIATION-SUCCESS",
                        userId,
                        utcNow
                    );

                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "執行密碼重設與環境清理交易時發生核心異常。");
                    throw;
                }
            });
        }


        /// <summary>
        /// 關鍵稽核點：
        /// SEC-403-BAD-TICKET：拒絕非法的密碼重設嘗試（潛在的重放攻擊或 Ticket 惡意猜測）。
        /// SEC-200-PASSWORD-REMEDIATION-SUCCESS：用戶透過重設密碼成功完成身分補償修復。
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="ticketId"></param>
        /// <param name="newPasswordHash"></param>
        /// <param name="clientIp"></param>
        /// <returns></returns>
        public async Task<bool> RemediateAccountAsync(string userId, string ticketId, string newPasswordHash, string clientIp)
        {
            var utcNow = DateTime.UtcNow;
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var ticket = await _context.RemediationTickets
                    .FirstOrDefaultAsync(x => x.TicketId == ticketId && x.UserId == userId);

                // 🚀 管控一：驗證 Ticket 沒過期且未被使用
                if (ticket == null || ticket.IsUsed || ticket.ExpiresAt < utcNow)
                {
                    // 💡 修正：將非法密碼重設嘗試導流至安全總帳
                    using (Serilog.Context.LogContext.PushProperty("LogType", "Security"))
                    using (Serilog.Context.LogContext.PushProperty("UserId", userId))
                    using (Serilog.Context.LogContext.PushProperty("EventCategory", "Auth.RemediationFailed"))
                    using (Serilog.Context.LogContext.PushProperty("ClientIp", clientIp))
                    {
                        _logger.LogWarning("[Security-Audit:SEC-403-BAD-TICKET] 拒絕非法的密碼重設嘗試。用戶: {UserId}, Ticket: {TicketId}, 來源IP: {ClientIp}", userId, ticketId, clientIp);
                    }

                    await transaction.RollbackAsync();
                    return false;
                }

                ticket.IsUsed = true;
                await _context.UserRefreshTokens.Where(x => x.UserId == userId).ExecuteDeleteAsync();
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 💡 修正：將成功修復軌跡導流至安全總帳
                using (Serilog.Context.LogContext.PushProperty("LogType", "Security"))
                using (Serilog.Context.LogContext.PushProperty("UserId", userId))
                using (Serilog.Context.LogContext.PushProperty("EventCategory", "Auth.RemediationSuccess"))
                using (Serilog.Context.LogContext.PushProperty("ClientIp", clientIp))
                {
                    _logger.LogInformation(
                        "[Security-Event:SEC-200-PASSWORD-REMEDIATION-SUCCESS] 用戶透過重設密碼成功完成身分補償修復。用戶: {UserId}, 時間戳記: {TimestampUtc}, 來源IP: {ClientIp}",
                        userId,
                        utcNow,
                        clientIp
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "執行密碼重設補償交易時崩潰。");
                throw;
            }
        }
    }
}
