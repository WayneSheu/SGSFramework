using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Polly;
using SGSFramework.Core.Abstractions.Entities.SoftDelet;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SGSFramework.AuditLog.DTOs
{ 
    /// <summary>
    /// 「數據快照與緩衝」 的關鍵角色。之所以稱之為 「臨時 DTO (Data Transfer Object)」
    /// ，是因為它在實體資料存入資料庫（SaveChanges）之前的瞬間，將變更內容從 EF Core 的追蹤狀態中提取出來。
    ///  具體功能與設計必要性：
    ///  1.脫離 EF Core 追蹤 (Decoupling)
    ///  2.處理「暫時性性主鍵」(Temporary Key Handling)
    ///  3.結構化變更數據 (Structuring Changes)
    /// </summary>
    public class AuditEntry
    {
        public AuditEntry(EntityEntry entry)
        {
            Entry = entry; // 暫時持有，用於 SavedChanges 階段補填 ID
            Schema = entry.Metadata.GetSchema() ?? "dbo"; // 預設為 dbo
            TableName = entry.Metadata.GetTableName();
            Timestamp = DateTime.UtcNow;

            // Action 偵測邏輯
            if (entry.State == EntityState.Modified && entry.Entity is ISoftDeletable)
            {
                var prop = entry.Property(nameof(ISoftDeletable.IsDeleted));

                if (prop.IsModified)
                {
                    bool oldValue = (bool)prop.OriginalValue;
                    bool newValue = (bool)prop.CurrentValue;

                    if (oldValue == false && newValue == true)
                    {
                        Action = "SoftDeleted"; // 由 SoftDeleteInterceptor 觸發
                        return;
                    }
                    if (oldValue == true && newValue == false)
                    {
                        Action = "Restored";    // 由手動還原邏輯觸發
                        return;
                    }
                }
            }

            // 若非上述情況，則保留原生的 Created, Modified, Deleted 等狀態
            Action = entry.State.ToString();

        }

        // 這裡的 Id 是為了在記錄過程中能夠追蹤這筆變更，並且在 SavedChanges 階段補填到 AuditLog 實體中
        public Guid Id { get; set; }

        // 這些屬性是為了脫離 EF Core 後還能生存的資料
        public string Schema { get; set; }
        public string TableName { get; set; }
        public string Action { get; set; } // Insert, Update, Delete
        public DateTime Timestamp { get; set; }


        public Dictionary<string, object> KeyValues { get; } = new();
        public Dictionary<string, object> OldValues { get; } = new();
        public Dictionary<string, object> NewValues { get; } = new();

        //來自 HTTP Context 的環境資訊
        public string? TraceId { get; set; }
        public string? UserId { get; set; }
        public string? RemoteIp { get; set; }

        /// <summary>
        /// 紀錄所有發生變動的欄位名稱
        /// </summary>
        public List<string> ChangedColumns { get; } = new();

        /// <summary>
        /// 判斷是否包含特定欄位的變更
        /// </summary>
        public bool HasChanged(string propertyName) => ChangedColumns.Contains(propertyName);

        // 內部輔助屬性 (不序列化)
        [JsonIgnore]
        public EntityEntry Entry { get; }

        // 關鍵：用來存放尚未取得資料庫 ID 的屬性參考
        [JsonIgnore]
        public List<PropertyEntry> TemporaryProperties { get; } = new();

        // 辨識這是一般實體還是「關聯變更」
        public AuditEntryType EntryType { get; set; } = AuditEntryType.Entity;

        // 專門用於多對多關聯的資訊
        public RelationMetadata? RelationInfo { get; set; }
        public object EncriptionHelper { get; private set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public enum AuditEntryType { Entity, Relationship }

    public class RelationMetadata
    {
        public string LeftEntityName { get; set; }  // 例如：Student
        public string LeftId { get; set; }          // 例如：StudentId = 1
        public string RightEntityName { get; set; } // 例如：Course
        public string RightId { get; set; }         // 例如：CourseId = 101
    }
}
