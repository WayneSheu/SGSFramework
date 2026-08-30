using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Reports.Dtos
{
    /// <summary>
    /// 實驗室列表查詢請求 DTO
    /// </summary>
    public class LaboratoryQueryRequest
    {
        public string? Keyword { get; set; }
        public string? Category { get; set; }
        public string? Status { get; set; }
    }
}
