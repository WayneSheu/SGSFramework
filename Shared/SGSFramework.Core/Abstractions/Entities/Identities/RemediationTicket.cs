using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 身分補償修復憑證實體
    /// 專門用於記錄因資安熔斷（Lockdown）後，允許用戶執行高風險密碼修復或身分重驗的臨時安全 Ticket。
    /// </summary>
    [Table("RemediationTickets", Schema = "dbo")]
    public sealed class RemediationTicket
    {
        /// <summary>
        /// 憑證唯一識別碼 (通常為安全隨機生成的 GUID 或加密記號)
        /// </summary>
        [Key]
        [Required]
        [MaxLength(128)]
        public string TicketId { get; set; } = string.Empty;

        /// <summary>
        /// 關聯的用戶識別碼
        /// </summary>
        [Required]
        [MaxLength(128)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 該憑證是否已被使用
        /// 🚨 關鍵管控：防止 Replay Attack（重放攻擊）
        /// </summary>
        [Required]
        public bool IsUsed { get; set; }

        /// <summary>
        /// 憑證核發時間
        /// </summary>
        [Required]
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 憑證失效截止時間
        /// 🚨 關鍵管控：時效性驗證，逾期即作廢
        /// </summary>
        [Required]
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// 核發此憑證時的資安風險原因說明
        /// </summary>
        [MaxLength(500)]
        public string? RemediationReason { get; set; }

        /// <summary>
        /// 核發此憑證時的客戶端來源 IP (供後續日誌鑑識比對)
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string CreatedByIp { get; set; } = string.Empty;
    }

    public sealed class RemediationTicketConfiguration : IEntityTypeConfiguration<RemediationTicket>
    {
        public void Configure(EntityTypeBuilder<RemediationTicket> builder)
        {
            builder.ToTable("RemediationTickets", schema: "dbo");

            builder.HasKey(x => x.TicketId);
            builder.Property(x => x.UserId).IsRequired().HasMaxLength(128);
            builder.Property(x => x.IsUsed).IsRequired();

            builder.HasIndex(e => new { e.TicketId, e.UserId });

        }
    }


}
