using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Identities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.DbContexts
{
    public interface ITokenDbContext
    {

        // 存放使用者的 Refresh Token 資料集
        DbSet<UserRefreshToken> UserRefreshTokens { get; set; }

        // 存放使用者因安全防禦被熔斷後發給的修復憑證「身分補償修復憑證（Ticket）」（例如允許他重設密碼的臨時安全權杖）。
        DbSet<RemediationTicket> RemediationTickets { get; set; }

        /// <summary>
        /// 動態選單資料集 
        /// </summary>
        DbSet<MenuItem> MenuItems { get; set; }

        /// <summary>
        /// 支援 EF Core 泛型 Set 查詢 
        /// </summary>
        DbSet<TEntity> Set<TEntity>() where TEntity : class;

        /// <summary>
        /// 儲存變更至資料庫，並支援非同步操作
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
  
    }
}
