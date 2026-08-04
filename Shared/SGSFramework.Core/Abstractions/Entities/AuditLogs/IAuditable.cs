using SGSFramework.Core.Abstractions.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SGSFramework.Core.Abstractions.Entities.AuditLogs
{
    /// <summary>
    /// 審計日誌實體標記界面 (Auditable Entity Marker Interface)
    /// 標記介面：只有實作它的 Entity 會被紀錄
    /// </summary>
    public interface IAuditable
    {

        [AuditIgnore]
        [Editable(false)]
        public string CreatedBy { get; set; } 

        [AuditIgnore]
        [Editable(false)]
        public DateTimeOffset CreatedAtUtc { get; set; }

        [AuditIgnore]
        public string? UpdatedBy { get; set; }

        [AuditIgnore]
        public DateTimeOffset? UpdatedAtUtc { get; set; }

    }
}
