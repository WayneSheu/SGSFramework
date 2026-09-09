using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Transactions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Transactions
{
    /// <summary>
    /// 基於 Entity Framework Core 的工作單元實作
    /// </summary>
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly DbContext _dbContext;

        /// <summary>
        /// 初始化工作單元
        /// </summary>
        /// <param name="dbContext">資料庫上下文</param>
        public UnitOfWork(DbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc />
        public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            return new EfTransactionScope(transaction);
        }

        /// <inheritdoc />
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
