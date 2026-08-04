using System;
using System.Collections.Generic;
using System.Text;
using SGSFramework.Core.Abstractions.Attributes;

namespace SGSFramework.Core.Extensions
{
    public static class DecimalExtensions
    {
        /// <summary>
        /// 依據指定模式與位數處理小數取捨
        /// </summary>
        public static decimal RoundToPrecision(this decimal value, int decimals, RoundingMode mode)
        {
            if (decimals < 0) throw new ArgumentOutOfRangeException(nameof(decimals), "小數位數不可小於 0");

            return mode switch
            {
                // 四捨五入 (使用 AwayFromZero 確保 0.5 向上進位，符合商業習慣)
                RoundingMode.Round => Math.Round(value, decimals, MidpointRounding.AwayFromZero),

                // 無條件進位
                RoundingMode.Ceiling => CeilingWithPrecision(value, decimals),

                // 無條件捨去
                RoundingMode.Floor => FloorWithPrecision(value, decimals),

                _ => throw new ArgumentOutOfRangeException(nameof(mode), $"不支援的取捨模式: {mode}")
            };
        }

        private static decimal CeilingWithPrecision(decimal value, int decimals)
        {
            decimal multiplier = (decimal)Math.Pow(10, decimals);
            return Math.Ceiling(value * multiplier) / multiplier;
        }

        private static decimal FloorWithPrecision(decimal value, int decimals)
        {
            decimal multiplier = (decimal)Math.Pow(10, decimals);
            return Math.Floor(value * multiplier) / multiplier;
        }
    }
}
