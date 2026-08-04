using System;
using System.Collections.Generic;
using System.Text;


namespace SGSFramework.Core.Abstractions.Attributes
{
    //[AttributeUsage(AttributeTargets.Property)]
    //public class DecimalPrecisionAttribute : Attribute
    //{
    //    public int Precision { get; }
    //    public int Scale { get; }
    //    public RoundingMode Mode { get; }

    //    /// <summary>
    //    /// 動態設定單一欄位的 Decimal 精度與寫入取捨模式
    //    /// </summary>
    //    /// <param name="precision">總位數 (預設18)</param>
    //    /// <param name="scale">小數點後位數</param>
    //    /// <param name="mode">取捨模式（四捨五入、無條件進位、無條件捨去）</param>
    //    public DecimalPrecisionAttribute(int scale, RoundingMode mode, int precision = 18)
    //    {
    //        Precision = precision;
    //        Scale = scale;
    //        Mode = mode;
    //    }
    //}

    [AttributeUsage(AttributeTargets.Property)]
    public class DecimalPrecisionAttribute : Attribute
    {
        public int Precision { get; }
        public int Scale { get; }
        public RoundingMode Mode { get; }

        /// <summary>
        /// 動態設定單一欄位的 Decimal 精度與寫入取捨模式
        /// </summary>
        /// <param name="precision">總位數 (預設18)</param>
        /// <param name="scale">小數點後位數</param>
        /// <param name="mode">取捨模式（四捨五入、無條件進位、無條件捨去）</param>
        public DecimalPrecisionAttribute(int scale, RoundingMode mode, int precision = 18)
        {
            Precision = precision;
            Scale = scale;
            Mode = mode;
        }
    }

    public enum RoundingMode
    {
        Round,   // 四捨五入
        Ceiling, // 無條件進位
        Floor    // 無條件捨去
    }
}
