using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>
/// 月度維運報告索引（SOP 7.2）：每專案每月一份；統計快照與 PDF（FMS 分類 pms-form，加密儲存）。
/// 重新產生會取代舊檔（<see cref="Revision"/> 遞增），舊 PDF 由 FMS 虛擬刪除。
/// </summary>
[DisplayName("月度維運報告")]
[Comment("月度維運報告：統計快照（進線數、SLA 準時達成率、MTTR、逾期數）與 PDF 索引（實體檔案在 FMS）")]
public class MonthlyReport : IAuditable
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    [Comment("統計月份（YYYY-MM，台灣時間）")]
    public string Period { get; set; } = string.Empty;

    [Comment("版次：重新產生時遞增")]
    public int Revision { get; set; } = 1;

    [Comment("FMS 檔案識別碼（pms-form）")]
    public Guid FileId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? Sha256 { get; set; }

    [Comment("總進線單數")]
    public int TotalIncoming { get; set; }

    [Comment("SLA 準時達成率（0~1；無適用單據為空）")]
    public decimal? SlaOnTimeRate { get; set; }

    [Comment("平均修復時間 MTTR（小時；當月無修復完成的異常單為空）")]
    public decimal? MttrHours { get; set; }

    [Comment("統計基準時點已逾期的未結案單數")]
    public int OverdueCount { get; set; }

    public int DueSoonCount { get; set; }

    public Guid GeneratedBy { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Project? Project { get; set; }
}
