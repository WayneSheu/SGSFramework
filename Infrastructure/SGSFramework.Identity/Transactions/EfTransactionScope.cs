using Microsoft.EntityFrameworkCore.Storage;
using SGSFramework.Core.Abstractions.Transactions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Transactions
{
    /// <summary>
    /// 基於 Entity Framework Core IDbContextTransaction 的事務包裝實作
    /// </summary>
    public sealed class EfTransactionScope : ITransactionScope
    {
        private readonly IDbContextTransaction _transaction;
        private bool _disposed;

        /// <summary>
        /// 初始化 EF Core 事務包裝器
        /// </summary>
        /// <param name="transaction">EF Core 原始事務物件</param>
        public EfTransactionScope(IDbContextTransaction transaction)
        {
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        }

        /// <inheritdoc />
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.CommitAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.RollbackAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await _transaction.DisposeAsync();
                _disposed = true;
            }
        }
    }
}
