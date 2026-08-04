using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Identities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.DbContexts
{
    public interface ITokenDbContext
    {
        DbSet<UserRefreshToken> UserRefreshTokens { get; set; }
        //存放的是使用者因安全防禦被熔斷後，系統發給他的「身分補償修復憑證（Ticket）」（例如允許他重設密碼的臨時安全權杖）。
        DbSet<RemediationTicket> RemediationTickets { get; set; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
