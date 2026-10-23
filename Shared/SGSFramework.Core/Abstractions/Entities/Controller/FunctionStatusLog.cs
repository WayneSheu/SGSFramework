using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace SGSFramework.Core.Abstractions.Entities.Controller
{
    /// <summary>
    /// 系統功能啟用 / 停用異動歷程（稽核用，僅新增不修改）。
    /// 記錄操作者、時間、來源 IP、內部原因與對使用者公告內容，供資安稽核追溯。
    /// </summary>
    public class FunctionStatusLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ControllerMetadataId { get; set; }

        /// <summary>異動當下的功能識別（中繼資料重新掃描或移除後仍可辨識）</summary>
        public string ControllerName { get; set; } = string.Empty;

        public string ActionName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string PermissionKey { get; set; } = string.Empty;

        /// <summary>異動後狀態：true = 啟用、false = 停用</summary>
        public bool IsActive { get; set; }

        /// <summary>內部變更原因（僅管理者與稽核可見）</summary>
        public string? Reason { get; set; }

        /// <summary>對使用者公告內容（停用時顯示於系統公告與 API 拒絕訊息）</summary>
        public string? Notice { get; set; }

        /// <summary>預計恢復時間（僅供公告，不會自動恢復）</summary>
        public DateTimeOffset? ExpectedRestoreAt { get; set; }

        public Guid? ActorUserId { get; set; }

        public string? ActorName { get; set; }

        public string? ClientIp { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class FunctionStatusLogConfiguration : IEntityTypeConfiguration<FunctionStatusLog>
    {
        public void Configure(EntityTypeBuilder<FunctionStatusLog> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("FunctionStatusLogs", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ControllerName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.ActionName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.PermissionKey).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.Property(x => x.Notice).HasMaxLength(500);
            builder.Property(x => x.ActorName).HasMaxLength(256);
            builder.Property(x => x.ClientIp).HasMaxLength(64);
            builder.HasIndex(x => new { x.ControllerMetadataId, x.CreatedAt });
        }
    }
}
