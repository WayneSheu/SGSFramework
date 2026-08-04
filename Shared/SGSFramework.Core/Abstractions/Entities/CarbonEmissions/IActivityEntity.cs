using System.ComponentModel.DataAnnotations;

namespace SGSFramework.Core.Abstractions.Entities.CarbonEmissions
{
    /// <summary>
    /// 定義活動數據實體的標準介面，用於支援背景批次寫入 Worker
    /// </summary>
    public interface IActivityEntity
    {
        // 盤查年度
        [Display(Name = "盤查年度")]
        int InventoryYear { get; set; }
        [Display(Name = "數據量值")]
        decimal Amount { get; set; }
        [Display(Name = "單位")]
        string Unit { get; set; }
    }
}
