using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Helpers
{
    /// <summary>
    /// 提供內部 int 主鍵與跨模組確定性 Guid 之間的雙向高效能轉換 Helper
    /// </summary>
    public static class DeterministicGuidConverter
    {
        /// <summary>
        /// 將內部的 int ID 一致性地轉換為 16 位元的確定性 Guid
        /// </summary>
        public static Guid ToDeterministicGuid(this int id)
        {
            Span<byte> bytes = stackalloc byte[16];
            BitConverter.TryWriteBytes(bytes, id);
            return new Guid(bytes);
        }

        /// <summary>
        /// 將跨模組傳入的確定性 Guid 還原為內部的 int ID
        /// </summary>
        public static int ToInternalIntId(this Guid guid)
        {
            Span<byte> bytes = stackalloc byte[16];
            if (!guid.TryWriteBytes(bytes))
            {
                throw new ArgumentException("無效的 Guid 格式", nameof(guid));
            }

            return BitConverter.ToInt32(bytes);
        }
    }
}
