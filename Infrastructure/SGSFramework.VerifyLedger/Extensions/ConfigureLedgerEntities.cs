using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Ledgers;

namespace SGSFramework.VerifyLedger.Extensions
{
    /// <summary>
    /// 提供給所有泛型 DbContext 使用的 Ledger 擴充配置器
    /// </summary>
    public static class LedgerConfigurationExtensions
    {
        /// <summary>
        /// 自動掃描並配置該 DbContext 內所有實作 ILedgerEntity 的實體對應至 MSSQL 2025 Ledger 表
        /// </summary>
        public static void ConfigureLedgerEntities(this ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            var ledgerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(ILedgerEntity).IsAssignableFrom(p) && p.IsClass && !p.IsAbstract);

            //foreach (var type in ledgerTypes)
            //{
            //    modelBuilder.Entity(type, b =>
            //    {
            //        // 宣告為 MSSQL Ledger 資料表
            //        //b.ToTable(name: type.Name, t => t.IsLedger());

            //        // 註冊並映射 MSSQL 2025 內建的總帳系統欄位
            //        b.Property(nameof(ILedgerEntity.LedgerStartTransactionId))
            //         .HasColumnName("ledger_start_transaction_id")
            //         .ValueGeneratedOnAddOrUpdate();

            //        b.Property(nameof(ILedgerEntity.LedgerStartSequenceNumber))
            //         .HasColumnName("ledger_start_sequence_number")
            //         .ValueGeneratedOnAddOrUpdate();
            //    });
            //}
        }
    }
}

