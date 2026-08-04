using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuditLog.Configurations
{
    public class AuditOptions
    {
        public const string SectionName = "AuditSettings";

        public bool IsEnabled { get; set; } = true;
        // 預設批次大小（可由設定檔覆寫）
        public int BatchSize { get; set; } = 1000;
        // 指定需要審計的實體名稱清單
        public List<string> AuditableEntityNames { get; set; } = new();
        // 排除特定敏感欄位
        public List<string> IgnoredTables { get; set; }
        // 排除特定敏感欄位
        public List<string> ExcludedProperties { get; set; } = new();
        // 排除特定命名的中間表 (例如以 "Temp" 或 "Internal" 開頭的)
        public List<string> ExcludedTablePrefixes { get; set; } = new() { "Internal", "Temp" };
        // 是否審計多對多關聯
        public bool IncludeRelationships { get; set; } = true;
        // 只有當中間表的「兩端實體」都在此清單，才進行紀錄 (嚴格模式)
        public bool StrictRelationshipAudit { get; set; } = true;
        public bool StoreInSeparateDatabase { get; set; }
        public int RetentionDays { get; set; }
        // 當 buffer 未達批次上限時，最多等待多少秒後強制 flush（可由設定檔覆寫）
        public int FlushPeriodSeconds { get; set; } = 5;

    }
}
