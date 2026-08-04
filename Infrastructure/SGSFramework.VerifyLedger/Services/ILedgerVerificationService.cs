using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Ledgers;
using SGSFramework.VerifyLedger.Dtos;

namespace SGSFramework.VerifyLedger.Services
{
    /// <summary>
    /// 支援多資料庫架構 (Generic DbContext) 的總帳驗證服務介面
    /// </summary>
    /// <typeparam name="TContext">繼承自 DbContext 的資料庫內容控制項</typeparam>
    /// <typeparam name="TEntity">繼承自 ILedgerEntity 的實體型別</typeparam>
    public interface ILedgerVerificationService<TContext, TEntity>
        where TContext : DbContext
        where TEntity : class, ILedgerEntity
    {
        /// <summary>
        /// 驗證指定 DbContext 下特定資料表的總帳完整性
        /// </summary>
        /// <param name="ledgerDigestJson">外部驗證摘要公報 (JSON 格式)</param>
        /// <returns>驗證結果</returns>
        Task<LedgerVerificationResult> VerifyLedgerAsync(string? ledgerDigestJson = null);
    }
}
