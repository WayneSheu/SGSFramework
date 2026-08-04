using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.AuditLogs
{


    /// <summary>
    /// 審計日誌持久化策略介面
    /// </summary>
    /// <typeparam name="TContext">目標模組之 DbContext 類型</typeparam>
    public interface IAuditStorageStrategy<TContext> where TContext : DbContext
    {
        /// <summary>
        /// 寫入單批次審計日誌
        /// </summary>
        Task SaveBatchAsync(TContext dbContext, List<AuditLogEntity> batch, CancellationToken cancellationToken);

        /// <summary>
        /// 復原並寫入備份檔紀錄
        /// </summary>
        Task RecoverLogsAsync(TContext dbContext, List<AuditLogEntity> fallbackLogs, CancellationToken cancellationToken);
    }
}
