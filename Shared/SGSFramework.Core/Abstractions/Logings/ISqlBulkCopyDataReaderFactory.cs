using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace SGSFramework.Core.Abstractions.Logings
{
    /// <summary>
    /// 提供一個介面，用於建立 IDataReader 物件，以便在使用 SqlBulkCopy 時，將資料批次寫入資料庫。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ISqlBulkCopyDataReaderFactory<T> where T : class
    {
        IDataReader CreateDataReader(IEnumerable<T> items);
    }
}
