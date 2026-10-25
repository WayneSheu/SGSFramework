using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SGSFramework.Core.Abstractions.Entities.Controller
{
    /// <summary>
    /// 系統功能啟用 / 停用異動歷程（稽核用，僅新增不修改）。
    /// 記錄操作者、時間、來源 IP、內部原因與對使用者公告內容，供資安稽核追溯。
    /// </summary>
    [Comment("系統功能啟用 / 停用異動歷程（稽核用，僅新增不修改）")]
    public class FunctionStatusLog
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("控制器中繼資料識別")]
        [Comment("控制器中繼資料識別")]
        public Guid ControllerMetadataId { get; set; }

        /// <summary>異動當下的功能識別（中繼資料重新掃描或移除後仍可辨識）</summary>
        [DisplayName("控制器名稱")]
        [Comment("異動當下的功能識別（中繼資料重新掃描或移除後仍可辨識）")]
        public string ControllerName { get; set; } = string.Empty;

        [DisplayName("動作名稱")]
        [Comment("動作名稱")]
        public string ActionName { get; set; } = string.Empty;

        [DisplayName("功能顯示名稱")]
        [Comment("功能顯示名稱")]
        public string DisplayName { get; set; } = string.Empty;

        [DisplayName("權限金鑰")]
        [Comment("權限金鑰")]
        public string PermissionKey { get; set; } = string.Empty;

        /// <summary>異動後狀態：true = 啟用、false = 停用</summary>
        [DisplayName("是否啟用")]
        [Comment("異動後狀態：true = 啟用、false = 停用")]
        public bool IsActive { get; set; }

        /// <summary>內部變更原因（僅管理者與稽核可見）</summary>
        [DisplayName("變更原因")]
        [Comment("內部變更原因（僅管理者與稽核可見）")]
        public string? Reason { get; set; }

        /// <summary>對使用者公告內容（停用時顯示於系統公告與 API 拒絕訊息）</summary>
        [DisplayName("公告內容")]
        [Comment("對使用者公告內容（停用時顯示於系統公告與 API 拒絕訊息）")]
        public string? Notice { get; set; }

        /// <summary>預計恢復時間（僅供公告，不會自動恢復）</summary>
        [DisplayName("預計恢復時間")]
        [Comment("預計恢復時間（僅供公告，不會自動恢復）")]
        public DateTimeOffset? ExpectedRestoreAt { get; set; }

        [DisplayName("操作者識別")]
        [Comment("操作者識別")]  
        public Guid? ActorUserId { get; set; }
        
        [DisplayName("操作者名稱")]
        [Comment("操作者名稱")]  
        public string? ActorName { get; set; }

        [DisplayName("來源 IP")]
        [Comment("來源 IP")]  
        public string? ClientIp { get; set; }

        [DisplayName("建立時間")]
        [Comment("建立時間")]
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
