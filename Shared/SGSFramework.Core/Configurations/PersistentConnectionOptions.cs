// Path: src/SGSFramework/Shared/SGSFramework.Core/Configurations/PersistentConnectionOptions.cs
using System.ComponentModel.DataAnnotations;

namespace SGSFramework.Core.Configurations
{
    /// <summary>
    /// 資料庫連線字串組態 (對應 appsettings 的 PersistentSettings:ConnectionStrings)。
    /// 置於 Core 層，讓 SystemLog / Persistent / AuditLog 等模組共用同一份強型別定義，避免各自以字串路徑讀取。
    /// </summary>
    public sealed class PersistentConnectionOptions
    {
        public const string SectionName = "PersistentSettings:ConnectionStrings";

        /// <summary>
        /// 應用程式執行期連線 (最小權限帳號)
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string DefaultConnection { get; set; } = string.Empty;

        /// <summary>
        /// EF Core Migration 使用的部署帳號連線 (選填，未設定時回退至 DefaultConnection)
        /// </summary>
        public string? MigrationConnection { get; set; }

        /// <summary>
        /// 資料庫初始化腳本 (建庫、建立登入帳號等) 使用的高權限連線 (選填，未設定則略過初始化腳本)
        /// </summary>
        public string? BootstrappingConnection { get; set; }

        /// <summary>
        /// 資料庫備份使用的連線 (選填，建議為僅具 db_backupoperator 角色的專用帳號；未設定時回退至 MigrationConnection → DefaultConnection)
        /// </summary>
        public string? BackupConnection { get; set; }
    }
}
