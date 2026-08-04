using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Persistent.Converters
{
    /// <summary>
    /// 
    /// </summary>
    public class RoundingConverter : ValueConverter<decimal, decimal>
    {
        public RoundingConverter(int decimals, RoundingMode mode)
            : base(
                v => v.RoundToPrecision(decimals, mode), // 寫入資料庫時自動取捨
                v => v                                   // 讀出時保持原樣
            )
        { }
    }
}
