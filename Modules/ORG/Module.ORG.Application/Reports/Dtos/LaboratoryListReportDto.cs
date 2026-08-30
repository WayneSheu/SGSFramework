using SGSFramework.ReportEngine.Abstractions;
using SGSFramework.ReportEngine.Generators;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Reports.Dtos
{
    /// <summary>
    /// 實驗室列表清單報表 DTO
    /// </summary>
    public class LaboratoryListReportDto : IReportData, ITableReportData<LaboratoryItemDto>
    {
        // --- IReportData 實作 (頁首資訊) ---
        public string ReportTitle => "實驗室清單總覽報表";
        public string QueryDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        public string OperatorName { get; set; } = string.Empty;
        public string QueryContext { get; set; } = "全系統實驗室基本資料與狀態清單";

        // --- ITableReportData 實作 (表格明細資料) ---
        public IEnumerable<LaboratoryItemDto> Details { get; set; } = new List<LaboratoryItemDto>();
    }

    /// <summary>
    /// 實驗室明細項目 DTO
    /// </summary>
    public class LaboratoryItemDto
    {
        public string LabCode { get; set; } = string.Empty;
        public string LabName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Manager { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
