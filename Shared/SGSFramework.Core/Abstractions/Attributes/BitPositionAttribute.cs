// ==========================================
// 檔案路徑: src/SGSFramework/Core/SGSFramework.Core.Abstractions/Attributes/BitPositionAttribute.cs
// 架構層級: Domain / Abstractions
// ==========================================

using System;

namespace SGSFramework.Core.Abstractions.Attributes
{
    /// <summary>
    /// 用於指定 Controller 或 Action 在模組內對應的位元遮罩位置 (0 ~ 63)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class BitPositionAttribute : Attribute
    {
        /// <summary>
        /// 取得或設定位元位置 (0 到 63 之間)
        /// </summary>
        public int Position { get; }

        /// <summary>
        /// 初始化 <see cref="BitPositionAttribute"/> 類別的新執行個體
        /// </summary>
        /// <param name="position">位元位置 (0~63)</param>
        public BitPositionAttribute(int position)
        {
            if (position is < 0 or > 63)
            {
                throw new ArgumentOutOfRangeException(nameof(position), "位元位置必須介於 0 到 63 之間。");
            }
            Position = position;
        }
    }
}