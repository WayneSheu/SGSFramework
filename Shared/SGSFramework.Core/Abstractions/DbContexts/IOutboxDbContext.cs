using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Outbox;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.DbContexts
{
    public interface IOutboxDbContext
    {
        /// <summary>
        /// 取得或設定 OutboxMessage 的 DbSet，用於存取和操作 OutboxMessage 實體。
        /// </summary>
        DbSet<OutboxMessage> OutboxMessages { get; }
        /// <summary>
        /// 儲存對資料庫的變更，並返回受影響的實體數量。
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<int> SaveChangesAsync(CancellationToken ct = default);
 
        /// <summary>
        /// 執行一個交易，其中包含指定的動作。如果動作成功完成，則提交交易；如果發生錯誤，則中斷交易。
        /// </summary>
        /// <param name="action"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task ExecuteTransactionAsync(Func<Task> action, CancellationToken ct = default);
    }
}
