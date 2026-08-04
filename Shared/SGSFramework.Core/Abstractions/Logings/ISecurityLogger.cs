using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Logings
{
    /// <summary>
    /// 全域防篡改資安審計日誌管理器
    /// </summary>
    public interface ISecurityLogger
    { 
        /// <summary>
        /// 記錄企業級安全性稽核事件，自動分流至 MSSQL Ledger 總帳
        /// </summary>
        void LogSecurity(
            string eventCode,
            string eventCategory,
            string? userId,
            string clientIp,
            string messageTemplate,
            params object?[] propertyValues);
    }
}
