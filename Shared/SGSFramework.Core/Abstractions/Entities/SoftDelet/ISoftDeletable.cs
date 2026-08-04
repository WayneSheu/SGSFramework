using SGSFramework.Core.Abstractions.Attributes;

namespace SGSFramework.Core.Abstractions.Entities.SoftDelet
{
    /// <summary>
    /// 軟刪除實體標記界面 (Soft Deletable Entity Marker Interface)
    /// </summary>
    public interface ISoftDeletable
    {
        bool IsDeleted { get; set; }

        [AuditIgnore]
        string? DeletedBy { get; set; }

        [AuditIgnore]
        DateTimeOffset? DeletedOnUtc { get; set; }
    }
}
