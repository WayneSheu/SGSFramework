using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Entities.Ledgers
{
    /// <summary>
    /// 配合 MSSQL Ledger 實際產生的系統欄位名稱進行修正後的實體介面
    /// 標記介面：用於標示該 Entity 映射至 MSSQL 原生 Append-Only Ledger 資料表
    /// </summary>
    public interface ILedgerEntity
    {
        ///// <summary>
        ///// 對應 MSSQL 內建的 ledger_start_transaction_id
        ///// </summary>
        //long LedgerStartTransactionId { get; set; }

        ///// <summary>
        ///// 對應 MSSQL 內建的 ledger_start_sequence_number
        ///// </summary>
        //long LedgerStartSequenceNumber { get; set; }
    }
}
