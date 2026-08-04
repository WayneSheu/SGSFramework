using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Migrations
{
    /// <summary>
    /// 核心網域遷移自訂異常，用於封裝跨模組或系統套用遷移時發生的架構層級錯誤
    /// </summary>
    /// <summary>
    /// 核心網域遷移自訂異常
    /// </summary>
    public sealed class MigrationException : Exception
    {
        public string? ModuleName { get; }
        public string? Step { get; }

        public MigrationException() : base("發生未知的模組動態維護錯誤。") { }

        public MigrationException(string message) : base(message) { }

        public MigrationException(string message, Exception? innerException)
            : base(message, innerException) { }

        public MigrationException(string message, string? moduleName, string? step, Exception? innerException)
            : base(message, innerException)
        {
            ModuleName = moduleName;
            Step = step;
        }
    }
}
